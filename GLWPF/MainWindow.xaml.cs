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
    private TimeSpan trackedTime = TimeSpan.Zero;
    private string processName = "notepad";
    public MainWindow()
    {
        InitializeComponent();
        appTimer = new System.Timers.Timer(1000); // 1 sec
        appTimer.Elapsed += OnTimedEvent;
        appTimer.Start();
    }
    private void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length > 0)
        {
            trackedTime = trackedTime.Add(TimeSpan.FromSeconds(1));

            // Updating UI from timer thread needs dispatcher
            Dispatcher.Invoke(() =>
            {
                TimerText.Text = $"{processName} time: {trackedTime:hh\\:mm\\:ss}";
            });
        }
    }
}