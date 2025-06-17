using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using System.Diagnostics;

namespace GLWPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private System.Timers.Timer appTimer;

    // Tracks time each process ID has been alive
    private Dictionary<int, TimeSpan> processTimes = new();

    // Maps each process ID to its name
    private Dictionary<int, string> processNames = new();

    // UI elements per process ID
    private Dictionary<int, TextBlock> processTextBlocks = new();
    public MainWindow()
    {
        InitializeComponent();

        appTimer = new System.Timers.Timer(1000); // 1 second
        appTimer.Elapsed += OnTimedEvent;
        appTimer.Start(); ;
    }
    private void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        var currentProcesses = Process.GetProcesses();

        var activeProcessIds = new HashSet<int>();

        foreach (var proc in currentProcesses)
        {
            int pid = proc.Id;
            string name = proc.ProcessName;

            activeProcessIds.Add(pid);

            if (!processTimes.ContainsKey(pid))
            {
                // New process detected
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