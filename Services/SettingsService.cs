using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CdCliWpf;

public enum GenerationMode
{
    Photo,
    Video
}

public sealed class AppSettings
{
    public GenerationMode Mode { get; set; } = GenerationMode.Photo;
    public string ModelPath { get; set; } = "";
    public string LoraPath { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string NegativePrompt { get; set; } = "";
    public string Pose { get; set; } = "";
    public double Steps { get; set; } = 25;
    public double Cfg { get; set; } = 6.0;
    public double Width { get; set; } = 768;
    public double Height { get; set; } = 768;
    public double Seed { get; set; } = 42;
    public double LoraWeight { get; set; } = 0.65;
    public string InitImagePath { get; set; } = "";
    public double InitImageStrength { get; set; } = 0.75;
    public bool UseLora { get; set; }
    public bool RandomSeed { get; set; }

    public string VideoModelPath { get; set; } = "";
    public string VideoTextEncoderPath { get; set; } = "";
    public string VideoVaePath { get; set; } = "";
    public string VideoInitImagePath { get; set; } = "";
    public string VideoEndImagePath { get; set; } = "";
    public string VideoClipVisionPath { get; set; } = "";
    public string VideoOutputDirectory { get; set; } = "";
    public string VideoPrompt { get; set; } = "";
    public string VideoNegativePrompt { get; set; } = "";
    public double VideoSteps { get; set; } = 25;
    public double VideoCfg { get; set; } = 6.0;
    public double VideoWidth { get; set; } = 768;
    public double VideoHeight { get; set; } = 768;
    public double VideoFrames { get; set; } = 49;
    public double VideoFps { get; set; } = 24;
    public double VideoSeed { get; set; } = 42;
    public bool VideoRandomSeed { get; set; }
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
                case nameof(AppSettings.Mode): settings.Mode = ParseMode(value); break;
                case nameof(AppSettings.ModelPath): settings.ModelPath = value; break;
                case nameof(AppSettings.LoraPath): settings.LoraPath = value; break;
                case nameof(AppSettings.OutputDirectory): settings.OutputDirectory = value; break;
                case nameof(AppSettings.Prompt): settings.Prompt = DecodeText(value); break;
                case nameof(AppSettings.NegativePrompt): settings.NegativePrompt = DecodeText(value); break;
                case nameof(AppSettings.Pose): settings.Pose = DecodeText(value); break;
                case nameof(AppSettings.Steps): settings.Steps = ParseDouble(value, settings.Steps); break;
                case nameof(AppSettings.Cfg): settings.Cfg = ParseDouble(value, settings.Cfg); break;
                case nameof(AppSettings.Width): settings.Width = ParseDouble(value, settings.Width); break;
                case nameof(AppSettings.Height): settings.Height = ParseDouble(value, settings.Height); break;
                case nameof(AppSettings.Seed): settings.Seed = ParseDouble(value, settings.Seed); break;
                case nameof(AppSettings.LoraWeight): settings.LoraWeight = ParseDouble(value, settings.LoraWeight); break;
                case nameof(AppSettings.InitImagePath): settings.InitImagePath = value; break;
                case nameof(AppSettings.InitImageStrength): settings.InitImageStrength = ParseDouble(value, settings.InitImageStrength); break;
                case nameof(AppSettings.UseLora): settings.UseLora = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case nameof(AppSettings.RandomSeed): settings.RandomSeed = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case nameof(AppSettings.VideoModelPath): settings.VideoModelPath = value; break;
                case nameof(AppSettings.VideoTextEncoderPath): settings.VideoTextEncoderPath = value; break;
                case nameof(AppSettings.VideoVaePath): settings.VideoVaePath = value; break;
                case nameof(AppSettings.VideoInitImagePath): settings.VideoInitImagePath = value; break;
                case nameof(AppSettings.VideoEndImagePath): settings.VideoEndImagePath = value; break;
                case nameof(AppSettings.VideoClipVisionPath): settings.VideoClipVisionPath = value; break;
                case nameof(AppSettings.VideoOutputDirectory): settings.VideoOutputDirectory = value; break;
                case nameof(AppSettings.VideoPrompt): settings.VideoPrompt = DecodeText(value); break;
                case nameof(AppSettings.VideoNegativePrompt): settings.VideoNegativePrompt = DecodeText(value); break;
                case nameof(AppSettings.VideoSteps): settings.VideoSteps = ParseDouble(value, settings.VideoSteps); break;
                case nameof(AppSettings.VideoCfg): settings.VideoCfg = ParseDouble(value, settings.VideoCfg); break;
                case nameof(AppSettings.VideoWidth): settings.VideoWidth = ParseDouble(value, settings.VideoWidth); break;
                case nameof(AppSettings.VideoHeight): settings.VideoHeight = ParseDouble(value, settings.VideoHeight); break;
                case nameof(AppSettings.VideoFrames): settings.VideoFrames = ParseDouble(value, settings.VideoFrames); break;
                case nameof(AppSettings.VideoFps): settings.VideoFps = ParseDouble(value, settings.VideoFps); break;
                case nameof(AppSettings.VideoSeed): settings.VideoSeed = ParseDouble(value, settings.VideoSeed); break;
                case nameof(AppSettings.VideoRandomSeed): settings.VideoRandomSeed = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
            }
        }

        return settings;
    }

    public static void Save(AppSettings settings)
    {
        var lines = new[]
        {
            nameof(AppSettings.Mode) + "=" + settings.Mode,
            nameof(AppSettings.ModelPath) + "=" + settings.ModelPath,
            nameof(AppSettings.LoraPath) + "=" + settings.LoraPath,
            nameof(AppSettings.OutputDirectory) + "=" + settings.OutputDirectory,
            nameof(AppSettings.Prompt) + "=" + EncodeText(settings.Prompt),
            nameof(AppSettings.NegativePrompt) + "=" + EncodeText(settings.NegativePrompt),
            nameof(AppSettings.Pose) + "=" + EncodeText(settings.Pose),
            nameof(AppSettings.Steps) + "=" + settings.Steps.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.Cfg) + "=" + settings.Cfg.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.Width) + "=" + settings.Width.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.Height) + "=" + settings.Height.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.Seed) + "=" + settings.Seed.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.LoraWeight) + "=" + settings.LoraWeight.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.InitImagePath) + "=" + settings.InitImagePath,
            nameof(AppSettings.InitImageStrength) + "=" + settings.InitImageStrength.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.UseLora) + "=" + settings.UseLora,
            nameof(AppSettings.RandomSeed) + "=" + settings.RandomSeed,
            nameof(AppSettings.VideoModelPath) + "=" + settings.VideoModelPath,
            nameof(AppSettings.VideoTextEncoderPath) + "=" + settings.VideoTextEncoderPath,
            nameof(AppSettings.VideoVaePath) + "=" + settings.VideoVaePath,
            nameof(AppSettings.VideoInitImagePath) + "=" + settings.VideoInitImagePath,
            nameof(AppSettings.VideoEndImagePath) + "=" + settings.VideoEndImagePath,
            nameof(AppSettings.VideoClipVisionPath) + "=" + settings.VideoClipVisionPath,
            nameof(AppSettings.VideoOutputDirectory) + "=" + settings.VideoOutputDirectory,
            nameof(AppSettings.VideoPrompt) + "=" + EncodeText(settings.VideoPrompt),
            nameof(AppSettings.VideoNegativePrompt) + "=" + EncodeText(settings.VideoNegativePrompt),
            nameof(AppSettings.VideoSteps) + "=" + settings.VideoSteps.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.VideoCfg) + "=" + settings.VideoCfg.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.VideoWidth) + "=" + settings.VideoWidth.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.VideoHeight) + "=" + settings.VideoHeight.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.VideoFrames) + "=" + settings.VideoFrames.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.VideoFps) + "=" + settings.VideoFps.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.VideoSeed) + "=" + settings.VideoSeed.ToString(CultureInfo.InvariantCulture),
            nameof(AppSettings.VideoRandomSeed) + "=" + settings.VideoRandomSeed
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

    private static GenerationMode ParseMode(string value)
        => Enum.TryParse(value, true, out GenerationMode mode) ? mode : GenerationMode.Photo;

    private static string EncodeText(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\r\n", "\\n", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                .Replace("\r", "\\n", StringComparison.Ordinal);

    private static string DecodeText(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                if (value[i + 1] == 'n')
                {
                    result.Append('\n');
                    i++;
                    continue;
                }
                if (value[i + 1] == '\\')
                {
                    result.Append('\\');
                    i++;
                    continue;
                }
            }
            result.Append(value[i]);
        }
        return result.ToString();
    }
}
