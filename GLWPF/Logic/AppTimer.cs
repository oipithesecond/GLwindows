using System;
using System.Timers;


namespace GLWPF.Logic;

public class AppTimer
{
    private readonly System.Timers.Timer timer;

    public event ElapsedEventHandler? OnTick;

    public AppTimer()
    {
        timer = new System.Timers.Timer(1000);
        timer.Elapsed += (s, e) => OnTick?.Invoke(s, e);
    }

    public void Start() => timer.Start();
    public void Stop() => timer.Stop();
    public void Dispose() => timer.Dispose();
}
