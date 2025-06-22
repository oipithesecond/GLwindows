using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Collections.Generic;
using System.Timers;
using GLWPF.Logic;
using System.Windows.Forms; // For NotifyIcon
using System.Drawing;      // For Icon

using WpfApp = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using FormsApp = System.Windows.Forms.Application;
using FormsMessageBox = System.Windows.Forms.MessageBox;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;

namespace GLWPF
{
    public partial class MainWindow : Window
    {
        private readonly AppTimer appTimer = new();
        private readonly GameTracker gameTracker = new();

        private Dictionary<int, TextBlock> textBlocks = new();
        private HashSet<string> pendingPrompts = new();

        private Dictionary<string, string> knownGames;
        private HashSet<string> ignoredGames;

        private const string GamesFile = "games.json";
        private const string IgnoredFile = "ignored.json";
        private const string StatsFile = "stats.json";
        private readonly System.Timers.Timer statsUploadTimer = new(1 * 5 * 1000);
        private string StatsUploadUrl;

        private NotifyIcon? trayIcon;

        public MainWindow()
        {
            InitializeComponent();

            EnvLoader.Load();
            StatsUploadUrl = EnvLoader.Get("STATS_UPLOAD_URL") ?? "";

            knownGames = FileManager.LoadKnownGames(GamesFile);
            ignoredGames = FileManager.LoadIgnoredGames(IgnoredFile);
            gameTracker.LoadStats(FileManager.LoadStats(StatsFile));

            appTimer.OnTick += OnTimedEvent;
            appTimer.Start();

            statsUploadTimer.Elapsed += async (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(StatsUploadUrl))
                    await HttpClientUploader.UploadStatsAsync(StatsFile, StatsUploadUrl);
            };
            statsUploadTimer.Start();

            trayIcon = new NotifyIcon
            {
                Icon = new Icon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "appicon.ico")),
                Visible = true,
                Text = "GameLogger"
            };

            trayIcon.DoubleClick += (_, _) => ShowFromTray();

            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add("Show", null, (_, _) => ShowFromTray());
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitApp());
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApp()
        {
            trayIcon?.Dispose();
            trayIcon = null;
            WpfApp.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void OnTimedEvent(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var currentProcesses = Process.GetProcesses();
                var activeGamePids = new HashSet<int>();

                foreach (var proc in currentProcesses)
                {
                    try
                    {
                        if (proc.MainWindowHandle == IntPtr.Zero)
                            continue;

                        string procName = proc.ProcessName.ToLower();

                        if (procName is "explorer" or "system" or "idle" or "svchost" or "textinputhost" or "syntpenh" or "GLWPF")
                            continue;

                        if (ignoredGames.Contains(procName))
                            continue;

                        if (!knownGames.ContainsKey(procName))
                        {
                            if (!pendingPrompts.Contains(procName))
                            {
                                pendingPrompts.Add(procName);

                                var result = WpfMessageBox.Show($"Track '{proc.ProcessName}' as a game?", "Track Game?", MessageBoxButton.YesNo);
                                if (result == MessageBoxResult.Yes)
                                {
                                    knownGames[procName] = char.ToUpper(procName[0]) + procName.Substring(1);
                                    FileManager.SaveKnownGames(GamesFile, knownGames);
                                }
                                else
                                {
                                    ignoredGames.Add(procName);
                                    FileManager.SaveIgnoredGames(IgnoredFile, ignoredGames);
                                }

                                pendingPrompts.Remove(procName);
                            }

                            continue;
                        }

                        int pid = proc.Id;
                        string displayName = knownGames[procName];
                        activeGamePids.Add(pid);

                        if (!gameTracker.TrackedTimes.ContainsKey(pid))
                        {
                            TimeSpan previousTime = gameTracker.TotalStats.ContainsKey(procName) ? gameTracker.TotalStats[procName] : TimeSpan.Zero;

                            gameTracker.TrackedTimes[pid] = previousTime;
                            gameTracker.TrackedNames[pid] = displayName;

                            var tb = new TextBlock
                            {
                                FontSize = 14,
                                Text = $"{displayName} : {previousTime:hh\\:mm\\:ss}"
                            };
                            textBlocks[pid] = tb;
                            AppList.Children.Add(tb);
                        }
                        else
                        {
                            gameTracker.UpdateStat(procName, pid);
                            textBlocks[pid].Text = $"{gameTracker.TrackedNames[pid]} ({pid}): {gameTracker.TrackedTimes[pid]:hh\\:mm\\:ss}";
                        }
                    }
                    catch { }
                }

                var closed = gameTracker.TrackedTimes.Keys.Except(activeGamePids).ToList();
                foreach (var pid in closed)
                {
                    string procName = gameTracker.TrackedNames[pid].ToLower();
                    gameTracker.TotalStats[procName] = gameTracker.TrackedTimes[pid];

                    gameTracker.TrackedTimes.Remove(pid);
                    gameTracker.TrackedNames.Remove(pid);
                    AppList.Children.Remove(textBlocks[pid]);
                    textBlocks.Remove(pid);
                }

                FileManager.SaveStats(StatsFile, gameTracker.GetSerializableStats());
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            trayIcon?.Dispose();
            statsUploadTimer.Stop();
            statsUploadTimer.Dispose();
            appTimer.Stop();
            appTimer.Dispose();
        }
    }
}
