using System;
using System.Linq;
using System.Windows;             // For Window
using System.Windows.Controls;   // For TextBlock
using System.Diagnostics;
using System.Collections.Generic;
using System.Timers;             // For ElapsedEventArgs
using GLWPF.Logic;               // For your modular logic

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
        private readonly System.Timers.Timer statsUploadTimer = new(1 * 5 * 1000); // every 5 seconds
        private string StatsUploadUrl;


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

                        if (procName is "explorer" or "system" or "idle" or "svchost" or "textinputhost" or "syntpenh")
                            continue;

                        if (ignoredGames.Contains(procName))
                            continue;

                        if (!knownGames.ContainsKey(procName))
                        {
                            if (!pendingPrompts.Contains(procName))
                            {
                                pendingPrompts.Add(procName);

                                var result = MessageBox.Show($"Track '{proc.ProcessName}' as a game?", "Track Game?", MessageBoxButton.YesNo);
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
                                Text = $"{displayName} ({pid}): {previousTime:hh\\:mm\\:ss}"
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

                // Cleanup closed apps
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
            appTimer.Stop();
            appTimer.Dispose();
            FileManager.SaveKnownGames(GamesFile, knownGames);
            FileManager.SaveIgnoredGames(IgnoredFile, ignoredGames);
            FileManager.SaveStats(StatsFile, gameTracker.GetSerializableStats());
            statsUploadTimer.Stop();
            statsUploadTimer.Dispose();
            base.OnClosed(e);
        }
    }
}
