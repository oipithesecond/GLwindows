using System.Windows;
using System.Diagnostics;
using System.Timers;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace GLWPF;

public partial class MainWindow : Window
{
    private System.Timers.Timer appTimer;
    private Dictionary<int, TimeSpan> trackedTimes = new();
    private Dictionary<int, string> trackedNames = new();
    private Dictionary<int, TextBlock> textBlocks = new();

    private Dictionary<string, string?> knownGames = new(); // value may be null for ignored apps
    private Dictionary<string, TimeSpan> totalTimePerGame = new();
    private HashSet<string> pendingPrompts = new();

    private readonly string gamesFilePath = "games.json";
    private readonly string statsFilePath = "stats.json";

    public MainWindow()
    {
        InitializeComponent();
        LoadKnownGames();
        LoadTrackedStats();

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

                // Skip common system/background processes
                if (procName is "explorer" or "system" or "idle" or "svchost" or "textinputhost" or "syntpenh")
                    continue;

                // Already marked as not to be tracked
                if (knownGames.TryGetValue(procName, out string? value) && value == "__ignored__")
                    continue;

                // Ask user if this process should be tracked
                if (!knownGames.ContainsKey(procName))
                {
                    if (!pendingPrompts.Contains(procName))
                    {
                        pendingPrompts.Add(procName);

                        Dispatcher.Invoke(() =>
                        {
                            var result = MessageBox.Show(
                                $"Do you want to track '{procName}' as a game?",
                                "Track Game?",
                                MessageBoxButton.YesNo);

                            if (result == MessageBoxResult.Yes)
                            {
                                knownGames[procName] = char.ToUpper(procName[0]) + procName.Substring(1);
                                SaveKnownGames();
                            }
                            else
                            {
                                knownGames[procName] = "__ignored__";
                                SaveKnownGames();
                            }

                            pendingPrompts.Remove(procName);
                        });
                    }

                    continue;
                }

                // Skip ignored ones
                string? displayName = knownGames[procName];
                if (displayName == "__ignored__" || displayName == null)
                    continue;

                int pid = proc.Id;
                activeGamePids.Add(pid);

                if (!trackedTimes.ContainsKey(pid))
                {
                    TimeSpan existingTime = totalTimePerGame.ContainsKey(procName) ? totalTimePerGame[procName] : TimeSpan.Zero;

                    trackedTimes[pid] = existingTime;
                    trackedNames[pid] = displayName;

                    Dispatcher.Invoke(() =>
                    {
                        var tb = new TextBlock
                        {
                            FontSize = 14,
                            Text = $"{displayName} ({pid}): {existingTime:hh\\:mm\\:ss}"
                        };
                        textBlocks[pid] = tb;
                        AppList.Children.Add(tb);
                    });
                }

                trackedTimes[pid] = trackedTimes[pid].Add(TimeSpan.FromSeconds(1));
                totalTimePerGame[procName] = trackedTimes[pid];

                Dispatcher.Invoke(() =>
                {
                    textBlocks[pid].Text = $"{trackedNames[pid]} ({pid}): {trackedTimes[pid]:hh\\:mm\\:ss}";
                });
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
            trackedTimes.Remove(pid);
            trackedNames.Remove(pid);
            Dispatcher.Invoke(() =>
            {
                AppList.Children.Remove(textBlocks[pid]);
                textBlocks.Remove(pid);
            });
        }

        SaveTrackedStats();
    }

    private void LoadKnownGames()
    {
        try
        {
            if (File.Exists(gamesFilePath))
            {
                string json = File.ReadAllText(gamesFilePath);
                knownGames = JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new();
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

    private void LoadTrackedStats()
    {
        try
        {
            if (File.Exists(statsFilePath))
            {
                string json = File.ReadAllText(statsFilePath);
                var saved = JsonSerializer.Deserialize<Dictionary<string, double>>(json); // seconds

                if (saved != null)
                {
                    foreach (var pair in saved)
                        totalTimePerGame[pair.Key] = TimeSpan.FromSeconds(pair.Value);
                }
            }
        }
        catch
        {
            totalTimePerGame = new();
        }
    }

    private void SaveTrackedStats()
    {
        try
        {
            var data = totalTimePerGame.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.TotalSeconds);
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(statsFilePath, json);
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        appTimer.Stop();
        appTimer.Dispose();
        SaveKnownGames();
        SaveTrackedStats();
        base.OnClosed(e);
    }
}
