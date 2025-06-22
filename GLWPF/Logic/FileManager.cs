using System;
using System.IO;                  // For File
using System.Text.Json;           // For JsonSerializer
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace GLWPF.Logic;

public static class FileManager
{
    public static Dictionary<string, string> LoadKnownGames(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    public static void SaveKnownGames(string path, Dictionary<string, string> knownGames)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(knownGames, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static HashSet<string> LoadIgnoredGames(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    public static void SaveIgnoredGames(string path, HashSet<string> ignored)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(ignored, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static Dictionary<string, double>? LoadStats(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path))
                : null;
        }
        catch { return null; }
    }

    public static void SaveStats(string path, Dictionary<string, double> stats)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
