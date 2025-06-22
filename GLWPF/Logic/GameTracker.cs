namespace GLWPF.Logic;

public class GameTracker
{
    public Dictionary<int, TimeSpan> TrackedTimes { get; } = new();
    public Dictionary<int, string> TrackedNames { get; } = new();
    public Dictionary<string, TimeSpan> TotalStats { get; private set; } = new();

    public void LoadStats(Dictionary<string, double>? loaded)
    {
        TotalStats = loaded?.ToDictionary(kv => kv.Key, kv => TimeSpan.FromSeconds(kv.Value)) ?? new();
    }

    public Dictionary<string, double> GetSerializableStats() =>
        TotalStats.ToDictionary(kv => kv.Key, kv => kv.Value.TotalSeconds);

    public void UpdateStat(string procName, int pid)
    {
        if (TrackedTimes.ContainsKey(pid))
        {
            TrackedTimes[pid] = TrackedTimes[pid].Add(TimeSpan.FromSeconds(1));
            TotalStats[procName] = TrackedTimes[pid];
        }
    }
}
