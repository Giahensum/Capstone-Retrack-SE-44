namespace Retrack.API.Helpers;

/// <summary>Nạp KEY=value khi phát triển; không ghi đè biến môi trường đã có của tiến trình.</summary>
public static class LocalEnvironment
{
    public static void Load(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadLines(path))
        {
            var value = line.Trim();
            if (value.Length == 0 || value.StartsWith('#')) continue;
            var separator = value.IndexOf('=');
            if (separator <= 0) throw new InvalidOperationException("Invalid local environment entry.");
            var name = value[..separator].Trim();
            var setting = value[(separator + 1)..].Trim();
            if (setting.Length >= 2 && ((setting[0] == '"' && setting[^1] == '"') ||
                (setting[0] == '\'' && setting[^1] == '\''))) setting = setting[1..^1];
            if (Environment.GetEnvironmentVariable(name) is null)
                Environment.SetEnvironmentVariable(name, setting);
        }
    }
}
