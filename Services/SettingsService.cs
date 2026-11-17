using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CdCliWpf;

public sealed class AppSettings
{
    public string ModelPath { get; set; } = "";
    public string LoraPath { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public double Steps { get; set; } = 25;
    public double Cfg { get; set; } = 6.0;
    public double Width { get; set; } = 768;
    public double Height { get; set; } = 768;
    public double Seed { get; set; } = 42;
    public double LoraWeight { get; set; } = 0.65;
    public bool UseLora { get; set; }
    public bool RandomSeed { get; set; }
}

public static class SettingsService
{
    private static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.ini");
    private static string HistoryPath => Path.Combine(AppContext.BaseDirectory, "history.txt");

    public static AppSettings Load()
    {
        var settings = new AppSettings();
        if (!File.Exists(SettingsPath))
            return settings;

        foreach (var raw in File.ReadAllLines(SettingsPath))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            var index = line.IndexOf('=');
            if (index < 0)
            {
                if (Directory.Exists(line))
                    settings.OutputDirectory = line;
                continue;
            }

            var key = line[..index].Trim();
            var value = line[(index + 1)..].Trim();
            switch (key)
            {
                case nameof(AppSettings.ModelPath): settings.ModelPath = value; break;
                case nameof(AppSettings.LoraPath): settings.LoraPath = value; break;
                case nameof(AppSettings.OutputDirectory): settings.OutputDirectory = value; break;
                case nameof(AppSettings.Steps): settings.Steps = ParseDouble(value, settings.Steps); break;
                case nameof(AppSettings.Cfg): settings.Cfg = ParseDouble(value, settings.Cfg); break;
                case nameof(AppSettings.Width): settings.Width = ParseDouble(value, settings.Width); break;
                case nameof(AppSettings.Height): settings.Height = ParseDouble(value, settings.Height); break;
                case nameof(AppSettings.Seed): settings.Seed = ParseDouble(value, settings.Seed); break;
                case nameof(AppSettings.LoraWeight): settings.LoraWeight = ParseDouble(value, settings.LoraWeight); break;
                case nameof(AppSettings.UseLora): settings.UseLora = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case nameof(AppSettings.RandomSeed): settings.RandomSeed = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
            }
        }

        return settings;
    }

    public static void Save(AppSettings settings)
    {
        var lines = new[]
        {
            nameof(AppSettings.ModelPath) + "=" + settings.ModelPath,
            nameof(AppSettings.LoraPath) + "=" + settings.LoraPath,
            nameof(AppSettings.OutputDirectory) + "=" + settings.OutputDirectory,
            nameof(AppSettings.Steps) + "=" + settings.Steps.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.Cfg) + "=" + settings.Cfg.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.Width) + "=" + settings.Width.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.Height) + "=" + settings.Height.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.Seed) + "=" + settings.Seed.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.LoraWeight) + "=" + settings.LoraWeight.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.UseLora) + "=" + settings.UseLora,
            nameof(AppSettings.RandomSeed) + "=" + settings.RandomSeed
        };
        File.WriteAllLines(SettingsPath, lines);
    }

    public static List<string> LoadHistory()
    {
        if (!File.Exists(HistoryPath))
            return new List<string>();

        var result = new List<string>();
        foreach (var path in File.ReadAllLines(HistoryPath))
        {
            var trimmed = path.Trim();
            if (trimmed.Length > 0 && File.Exists(trimmed))
                result.Add(trimmed);
        }
        return result;
    }

    public static void SaveHistory(IEnumerable<string> paths)
        => File.WriteAllLines(HistoryPath, paths);

    private static double ParseDouble(string value, double fallback)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
}
