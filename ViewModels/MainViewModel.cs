using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace CdCliWpf.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly EngineRunner _engine = new();
    private readonly ObservableCollection<string> _logLines = new();
    private readonly ObservableCollection<GenerationItem> _history = new();

    private CancellationTokenSource? _cancellation;
    private string _modelPath = "";
    private string _loraPath = "";
    private string _outputDirectory = "";
    private string _prompt = "";
    private string _negativePrompt = "";
    private string _pose = "";
    private double _steps = 25;
    private double _cfg = 6.0;
    private double _width = 768;
    private double _height = 768;
    private double _seed = 42;
    private double _loraWeight = 0.65;
    private bool _useLora;
    private bool _randomSeed;
    private bool _isGenerating;
    private string _status = "Готов к работе";
    private string _lastLog = "Ожидание команды...";
    private ImageSource? _previewImage;

    public MainViewModel()
    {
        var settings = SettingsService.Load();
        _modelPath = settings.ModelPath;
        _loraPath = settings.LoraPath;
        _outputDirectory = settings.OutputDirectory;
        _steps = settings.Steps;
        _cfg = settings.Cfg;
        _width = settings.Width;
        _height = settings.Height;
        _seed = settings.Seed;
        _loraWeight = settings.LoraWeight;
        _useLora = settings.UseLora;
        _randomSeed = settings.RandomSeed;

        BrowseModelCommand = new RelayCommand(BrowseModel);
        BrowseLoraCommand = new RelayCommand(BrowseLora);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        GenerateCommand = new RelayCommand(Generate, () => CanGenerate);
        CancelCommand = new RelayCommand(Cancel, () => IsGenerating);
        OpenOutputCommand = new RelayCommand(OpenOutput);
        OpenHistoryCommand = new RelayCommand<GenerationItem>(OpenHistory);
        RemoveHistoryCommand = new RelayCommand<GenerationItem>(RemoveHistory);
        ClearLogCommand = new RelayCommand(() => { _logLines.Clear(); _lastLog = "Лог очищен"; OnPropertyChanged(nameof(LastLog)); });

        foreach (var path in SettingsService.LoadHistory())
            _history.Insert(0, CreateHistoryItem(path));

        if (!_engine.EngineExists)
        {
            _status = "Не найден engine\\sd-cli.exe";
            AppendLog("Положите sd-cli.exe и DLL в папку engine рядом с программой.");
        }
        else
        {
            AppendLog("Движок готов: " + _engine.EnginePath);
        }
    }

    public ObservableCollection<string> LogLines => _logLines;
    public ObservableCollection<GenerationItem> History => _history;

    public RelayCommand BrowseModelCommand { get; }
    public RelayCommand BrowseLoraCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand GenerateCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenOutputCommand { get; }
    public RelayCommand<GenerationItem> OpenHistoryCommand { get; }
    public RelayCommand<GenerationItem> RemoveHistoryCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    public string EngineText => _engine.EngineExists ? "sd-cli.exe · подключён" : "sd-cli.exe · не найден";
    public bool EngineReady => _engine.EngineExists;

    public string ModelPath { get => _modelPath; set => SetText(ref _modelPath, value); }
    public string LoraPath { get => _loraPath; set => SetText(ref _loraPath, value); }
    public string OutputDirectory { get => _outputDirectory; set => SetText(ref _outputDirectory, value); }
    public string Prompt { get => _prompt; set => SetText(ref _prompt, value); }
    public string NegativePrompt { get => _negativePrompt; set => SetText(ref _negativePrompt, value); }
    public string Pose { get => _pose; set => SetText(ref _pose, value); }

    public double Steps { get => _steps; set => SetNumber(ref _steps, value, 25, 1, 50); }
    public double Cfg { get => _cfg; set => SetNumber(ref _cfg, value, 6, 1, 20); }
    public double Width { get => _width; set => SetNumber(ref _width, value, 768, 512, 2048); }
    public double Height { get => _height; set => SetNumber(ref _height, value, 768, 512, 2048); }
    public double Seed { get => _seed; set => SetNumber(ref _seed, value, 42, 1, 1000000000); }
    public double LoraWeight { get => _loraWeight; set => SetNumber(ref _loraWeight, value, 0.65, 0.05, 1.5); }

    public bool UseLora { get => _useLora; set => SetFlag(ref _useLora, value); }
    public bool RandomSeed { get => _randomSeed; set => SetFlag(ref _randomSeed, value); }
    public bool IsGenerating { get => _isGenerating; set => SetFlag(ref _isGenerating, value); }

    public string Status { get => _status; set => SetText(ref _status, value); }
    public string LastLog { get => _lastLog; set => SetText(ref _lastLog, value); }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        set
        {
            if (ReferenceEquals(_previewImage, value))
                return;
            _previewImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreview));
        }
    }

    public bool HasPreview => _previewImage is not null;

    public bool CanGenerate => !IsGenerating && EngineReady
        && !string.IsNullOrWhiteSpace(ModelPath)
        && File.Exists(ModelPath)
        && !string.IsNullOrWhiteSpace(OutputDirectory)
        && (!UseLora || !string.IsNullOrWhiteSpace(LoraPath));

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetModelPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        ModelPath = path;
        Status = "Модель: " + Path.GetFileName(path);
        SaveSettings();
    }

    public void SetLoraPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        LoraPath = path;
        UseLora = true;
        Status = "LoRA: " + Path.GetFileName(path);
        SaveSettings();
    }

    public void SetOutputDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;
        OutputDirectory = path;
        Status = "Папка: " + path;
        SaveSettings();
    }

    private void BrowseModel()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите модель",
            Filter = "Модели (*.gguf;*.safetensors)|*.gguf;*.safetensors|Все файлы|*.*",
            FileName = ModelPath
        };
        if (dialog.ShowDialog() == true)
            SetModelPath(dialog.FileName);
    }

    private void BrowseLora()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите LoRA",
            Filter = "LoRA (*.safetensors)|*.safetensors|Все файлы|*.*",
            FileName = LoraPath
        };
        if (dialog.ShowDialog() == true)
            SetLoraPath(dialog.FileName);
    }

    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog { FolderName = OutputDirectory };
        if (dialog.ShowDialog() == true)
            SetOutputDirectory(dialog.FolderName);
    }

    private async void Generate()
    {
        if (!CanGenerate)
            return;

        if (!File.Exists(ModelPath))
        {
            Status = "Файл модели не найден";
            AppendLog("Модель не найдена: " + ModelPath);
            return;
        }

        if (UseLora && !File.Exists(LoraPath))
        {
            Status = "Файл LoRA не найден";
            AppendLog("LoRA не найдена: " + LoraPath);
            return;
        }

        var finalPrompt = JoinPrompt(Prompt, Pose);
        var loraName = Path.GetFileNameWithoutExtension(LoraPath);
        if (UseLora)
            finalPrompt += " <lora:" + loraName + ":" + LoraWeight.ToString("0.00", CultureInfo.InvariantCulture) + ">";

        var outputPath = Path.Combine(OutputDirectory, "gen_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".png");
        var request = new GenerationRequest(
            ModelPath,
            finalPrompt,
            NegativePrompt,
            Width,
            Height,
            Steps,
            Cfg,
            RandomSeed ? -1 : Seed,
            UseLora,
            LoraPath,
            LoraWeight,
            outputPath);

        IsGenerating = true;
        Status = "Генерация...";
        AppendLog("Запуск: " + Path.GetFileName(outputPath));
        AppendLog("Промпт: " + finalPrompt);

        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        var progress = new Progress<string>(AppendLog);

        var result = await _engine.RunAsync(request, progress, _cancellation.Token);

        if (result.Success)
        {
            var item = CreateHistoryItem(result.OutputPath, finalPrompt);
            _history.Insert(0, item);
            while (_history.Count > 30)
                _history.RemoveAt(_history.Count - 1);
            SaveHistory();
            PreviewImage = LoadPreview(result.OutputPath);
            Status = "Готово · " + Path.GetFileName(result.OutputPath);
        }
        else
        {
            Status = result.Message;
        }

        AppendLog(result.Message);
        IsGenerating = false;
    }

    private void Cancel()
    {
        _cancellation?.Cancel();
        _engine.Cancel();
        Status = "Отмена...";
    }

    private void OpenOutput()
    {
        var folder = string.IsNullOrWhiteSpace(OutputDirectory) ? AppContext.BaseDirectory : OutputDirectory;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog("Не удалось открыть папку: " + ex.Message);
        }
    }

    private void OpenHistory(GenerationItem? item)
    {
        if (item is null || !File.Exists(item.Path))
            return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = item.Path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog("Не удалось открыть файл: " + ex.Message);
        }
    }

    private void RemoveHistory(GenerationItem? item)
    {
        if (item is null)
            return;
        _history.Remove(item);
        SaveHistory();
    }

    private static GenerationItem CreateHistoryItem(string path, string prompt = "")
    {
        var item = new GenerationItem { Path = path, Prompt = prompt };
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.Length > 11 && name.StartsWith("gen_", StringComparison.OrdinalIgnoreCase))
        {
            var stamp = name[4..];
            if (DateTime.TryParseExact(stamp, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                item.CreatedAt = parsed;
        }
        try { item.Thumbnail = GenerationItem.CreateThumbnail(path); } catch { }
        return item;
    }

    private static ImageSource? LoadPreview(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string JoinPrompt(string first, string second)
    {
        var a = first.Trim();
        var b = second.Trim();
        if (a.Length == 0) return b;
        if (b.Length == 0) return a;
        return a + ", " + b;
    }

    private void AppendLog(string line)
    {
        var text = "[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + line;
        _logLines.Add(text);
        while (_logLines.Count > 200)
            _logLines.RemoveAt(0);
        LastLog = line;
    }

    public void Persist() => SaveSettings();

    private void SaveHistory() => SettingsService.SaveHistory(_history.Select(h => h.Path));

    private void SaveSettings() => SettingsService.Save(new AppSettings
    {
        ModelPath = ModelPath,
        LoraPath = LoraPath,
        OutputDirectory = OutputDirectory,
        Steps = Steps,
        Cfg = Cfg,
        Width = Width,
        Height = Height,
        Seed = Seed,
        LoraWeight = LoraWeight,
        UseLora = UseLora,
        RandomSeed = RandomSeed
    });

    private void SetText(ref string field, string value, [CallerMemberName] string? name = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
            return;
        field = value;
        OnPropertyChanged(name);
        Refresh();
    }

    private void SetNumber(ref double field, double value, double fallback, double min, double max, [CallerMemberName] string? name = null)
    {
        if (double.IsNaN(value))
            value = fallback;
        value = Math.Min(max, Math.Max(min, value));
        if (Math.Abs(field - value) < 0.0001)
            return;
        field = value;
        OnPropertyChanged(name);
        Refresh();
    }

    private void SetFlag(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (field == value)
            return;
        field = value;
        OnPropertyChanged(name);
        Refresh();
    }

    private void Refresh()
    {
        GenerateCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanGenerate));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
