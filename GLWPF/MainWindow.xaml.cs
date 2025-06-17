using System.Windows;
using System.Diagnostics;
using System.Timers;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Linq;

namespace GLWPF;

public partial class MainWindow : Window
{
    private System.Timers.Timer appTimer;

    private Dictionary<int, TimeSpan> processTimes = new();
    private Dictionary<int, string> processNames = new();
    private Dictionary<int, TextBlock> processTextBlocks = new();

    private HashSet<string> systemProcesses = new()
    {
        "explorer", "svchost", "TextInputHost", "Idle", "System", "SynTPEnh", "GLWPF"
    };

    public MainWindow()
    {
        InitializeComponent();

        appTimer = new System.Timers.Timer(1000); // 1 second
        appTimer.Elapsed += OnTimedEvent;
        appTimer.Start();
    }

    private void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        var currentProcesses = Process.GetProcesses();
        var activeProcessIds = new HashSet<int>();

        foreach (var proc in currentProcesses)
        {
            try
            {
                // ❌ Skip if no visible window
                if (proc.MainWindowHandle == IntPtr.Zero)
                    continue;

                // ❌ Skip if window title is empty
                if (string.IsNullOrWhiteSpace(proc.MainWindowTitle))
                    continue;

                // ❌ Skip known system-related processes
                if (systemProcesses.Contains(proc.ProcessName))
                    continue;

                int pid = proc.Id;
                string name = proc.ProcessName;
                activeProcessIds.Add(pid);

                if (!processTimes.ContainsKey(pid))
                {
                    processTimes[pid] = TimeSpan.Zero;
                    processNames[pid] = name;

                    Dispatcher.Invoke(() =>
                    {
                        var tb = new TextBlock
                        {
                            FontSize = 14,
                            Text = $"{name} ({pid}): 00:00:00"
                        };
                        processTextBlocks[pid] = tb;
                        AppList.Children.Add(tb);
                    });
                }
                else
                {
                    processTimes[pid] = processTimes[pid].Add(TimeSpan.FromSeconds(1));
                    Dispatcher.Invoke(() =>
                    {
                        processTextBlocks[pid].Text = $"{processNames[pid]} ({pid}): {processTimes[pid]:hh\\:mm\\:ss}";
                    });
                }
            }
            catch
            {
                // Access denied processes are ignored
                continue;
            }
        }

        // Clean up closed processes
        var closedPids = new List<int>();
        foreach (var pid in processTimes.Keys)
        {
            if (!activeProcessIds.Contains(pid))
                closedPids.Add(pid);
        }

        foreach (var pid in closedPids)
        {
            processTimes.Remove(pid);
            processNames.Remove(pid);
            Dispatcher.Invoke(() =>
            {
                AppList.Children.Remove(processTextBlocks[pid]);
                processTextBlocks.Remove(pid);
            });
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        appTimer.Stop();
        appTimer.Dispose();
        base.OnClosed(e);
    }
}
