using System;
using System.IO;

namespace GLWPF.Logic;

public static class EnvLoader
{
    public static void Load(string path = ".env")
    {
        if (!File.Exists(path)) return;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2)
            {
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
        }
    }

    public static string? Get(string key) => Environment.GetEnvironmentVariable(key);
}
