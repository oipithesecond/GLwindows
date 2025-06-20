using System.Windows;
using System.Diagnostics;
using System.Timers;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace GLWPF
{
    public partial class MainWindow : Window
    {
        private System.Timers.Timer appTimer;
        private Dictionary<int, TimeSpan> trackedTimes = new();
        private Dictionary<int, string> trackedNames = new();
        private Dictionary<int, TextBlock> textBlocks = new();

        private Dictionary<string, string> knownGames = new();
        private HashSet<string> ignoredGames = new();
        private HashSet<string> pendingPrompts = new();

        private Dictionary<string, TimeSpan> totalStats = new(); // <-- NEW
        private string gamesFilePath = "games.json";
        private string ignoredFilePath = "ignored.json";
        private string statsFilePath = "stats.json"; // <-- NEW

        public MainWindow()
        {
            InitializeComponent();
            LoadKnownGames();
            LoadIgnoredGames();
            LoadStats(); // <-- NEW

            appTimer = new System.Timers.Timer(1000);
            appTimer.Elapsed += OnTimedEvent;
            appTimer.Start();
        }

        private void OnTimedEvent(object? sender, ElapsedEventArgs e)
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

                            Dispatcher.Invoke(() =>
                            {
                                var result = MessageBox.Show(
                                    $"Do you want to track '{proc.ProcessName}' as a game?",
                                    "Track Game?",
                                    MessageBoxButton.YesNo);

                                if (result == MessageBoxResult.Yes)
                                {
                                    knownGames[procName] = char.ToUpper(procName[0]) + procName.Substring(1);
                                    SaveKnownGames();
                                }
                                else
                                {
                                    ignoredGames.Add(procName);
                                    SaveIgnoredGames();
                                }

                                pendingPrompts.Remove(procName);
                            });
                        }

                        continue;
                    }

                    int pid = proc.Id;
                    string displayName = knownGames[procName];
                    activeGamePids.Add(pid);

                    if (!trackedTimes.ContainsKey(pid))
                    {
                        TimeSpan previousTime = totalStats.ContainsKey(procName) ? totalStats[procName] : TimeSpan.Zero;

                        trackedTimes[pid] = previousTime;
                        trackedNames[pid] = displayName;

                        Dispatcher.Invoke(() =>
                        {
                            var tb = new TextBlock
                            {
                                FontSize = 14,
                                Text = $"{displayName} ({pid}): {previousTime:hh\\:mm\\:ss}"
                            };
                            textBlocks[pid] = tb;
                            AppList.Children.Add(tb);
                        });
                    }
                    else
                    {
                        trackedTimes[pid] = trackedTimes[pid].Add(TimeSpan.FromSeconds(1));
                        totalStats[procName] = trackedTimes[pid]; // <-- Update global stats
                        Dispatcher.Invoke(() =>
                        {
                            textBlocks[pid].Text = $"{trackedNames[pid]} ({pid}): {trackedTimes[pid]:hh\\:mm\\:ss}";
                        });
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Remove closed processes
            var closedPids = trackedTimes.Keys.Except(activeGamePids).ToList();
            foreach (var pid in closedPids)
            {
                string procName = trackedNames[pid].ToLower();
                if (knownGames.ContainsValue(trackedNames[pid]))
                {
                    totalStats[procName] = trackedTimes[pid];
                }

                trackedTimes.Remove(pid);
                trackedNames.Remove(pid);

                Dispatcher.Invoke(() =>
                {
                    AppList.Children.Remove(textBlocks[pid]);
                    textBlocks.Remove(pid);
                });
            }

            SaveStats(); // <-- Save every tick
        }

        private void LoadKnownGames()
        {
            try
            {
                if (File.Exists(gamesFilePath))
                {
                    string json = File.ReadAllText(gamesFilePath);
                    knownGames = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch
            {
                knownGames = new();
            }
        }

        private void SaveKnownGames()
        {
            try
            {
                string json = JsonSerializer.Serialize(knownGames, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(gamesFilePath, json);
            }
            catch { }
        }

        private void LoadIgnoredGames()
        {
            try
            {
                if (File.Exists(ignoredFilePath))
                {
                    string json = File.ReadAllText(ignoredFilePath);
                    ignoredGames = JsonSerializer.Deserialize<HashSet<string>>(json) ?? new();
                }
            }
            catch
            {
                ignoredGames = new();
            }
        }

        private void SaveIgnoredGames()
        {
            try
            {
                string json = JsonSerializer.Serialize(ignoredGames, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ignoredFilePath, json);
            }
            catch { }
        }

        private void LoadStats()
        {
            try
            {
                if (File.Exists(statsFilePath))
                {
                    string json = File.ReadAllText(statsFilePath);
                    Dictionary<string, double>? loaded = JsonSerializer.Deserialize<Dictionary<string, double>>(json);
                    if (loaded != null)
                    {
                        totalStats = loaded.ToDictionary(kv => kv.Key, kv => TimeSpan.FromSeconds(kv.Value));
                    }
                }
            }
            catch
            {
                totalStats = new();
            }
        }

        private void SaveStats()
        {
            try
            {
                var saveable = totalStats.ToDictionary(kv => kv.Key, kv => kv.Value.TotalSeconds);
                string json = JsonSerializer.Serialize(saveable, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(statsFilePath, json);
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            appTimer.Stop();
            appTimer.Dispose();
            SaveKnownGames();
            SaveIgnoredGames();
            SaveStats(); // <-- Save on close
            base.OnClosed(e);
        }
    }
}
