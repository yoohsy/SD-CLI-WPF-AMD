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
    private GenerationMode _mode = GenerationMode.Photo;
    private bool _isModeSelectionVisible = true;
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
    private string _initImagePath = "";
    private double _initImageStrength = 0.75;
    private bool _useLora;
    private bool _randomSeed;
    private bool _isGenerating;
    private string _status = "Готов к работе";
    private string _lastLog = "Ожидание команды...";
    private ImageSource? _previewImage;

    private string _videoModelPath = "";
    private string _videoTextEncoderPath = "";
    private string _videoVaePath = "";
    private string _videoInitImagePath = "";
    private string _videoEndImagePath = "";
    private string _videoClipVisionPath = "";
    private string _videoOutputDirectory = "";
    private string _videoPrompt = "";
    private string _videoNegativePrompt = "";
    private double _videoSteps = 25;
    private double _videoCfg = 6.0;
    private double _videoWidth = 768;
    private double _videoHeight = 768;
    private double _videoFrames = 49;
    private double _videoFps = 24;
    private double _videoSeed = 42;
    private bool _videoRandomSeed;

    public MainViewModel()
    {
        var settings = SettingsService.Load();
        _mode = settings.Mode;
        _modelPath = settings.ModelPath;
        _loraPath = settings.LoraPath;
        _outputDirectory = settings.OutputDirectory;
        _prompt = settings.Prompt;
        _negativePrompt = settings.NegativePrompt;
        _pose = settings.Pose;
        _steps = settings.Steps;
        _cfg = settings.Cfg;
        _width = settings.Width;
        _height = settings.Height;
        _seed = settings.Seed;
        _loraWeight = settings.LoraWeight;
        _initImagePath = settings.InitImagePath;
        _initImageStrength = Math.Clamp(settings.InitImageStrength, 0.05, 1.0);
        _useLora = settings.UseLora;
        _randomSeed = settings.RandomSeed;
        _videoModelPath = settings.VideoModelPath;
        _videoTextEncoderPath = settings.VideoTextEncoderPath;
        _videoVaePath = settings.VideoVaePath;
        _videoInitImagePath = settings.VideoInitImagePath;
        _videoEndImagePath = settings.VideoEndImagePath;
        _videoClipVisionPath = settings.VideoClipVisionPath;
        _videoOutputDirectory = settings.VideoOutputDirectory;
        _videoPrompt = settings.VideoPrompt;
        _videoNegativePrompt = settings.VideoNegativePrompt;
        _videoSteps = settings.VideoSteps;
        _videoCfg = settings.VideoCfg;
        _videoWidth = settings.VideoWidth;
        _videoHeight = settings.VideoHeight;
        _videoFrames = settings.VideoFrames;
        _videoFps = settings.VideoFps;
        _videoSeed = settings.VideoSeed;
        _videoRandomSeed = settings.VideoRandomSeed;

        SelectPhotoModeCommand = new RelayCommand(() => SelectMode(GenerationMode.Photo), () => !IsGenerating);
        SelectVideoModeCommand = new RelayCommand(() => SelectMode(GenerationMode.Video), () => !IsGenerating);
        ContinueCommand = new RelayCommand(() => SelectMode(Mode), () => !IsGenerating);
        BrowseModelCommand = new RelayCommand(BrowseModel);
        BrowseLoraCommand = new RelayCommand(BrowseLora);
        BrowseInitImageCommand = new RelayCommand(BrowseInitImage);
        ClearInitImageCommand = new RelayCommand(ClearInitImage, () => !string.IsNullOrWhiteSpace(InitImagePath));
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        BrowseVideoModelCommand = new RelayCommand(BrowseVideoModel);
        BrowseVideoEncoderCommand = new RelayCommand(BrowseVideoEncoder);
        BrowseVideoVaeCommand = new RelayCommand(BrowseVideoVae);
        BrowseVideoInitImageCommand = new RelayCommand(BrowseVideoInitImage);
        BrowseVideoEndImageCommand = new RelayCommand(BrowseVideoEndImage);
        BrowseVideoClipVisionCommand = new RelayCommand(BrowseVideoClipVision);
        BrowseVideoOutputCommand = new RelayCommand(BrowseVideoOutput);
        OpenVideoOutputCommand = new RelayCommand(OpenVideoOutput);
        GenerateCommand = new RelayCommand(Generate, () => CanGenerate);
        GenerateVideoCommand = new RelayCommand(GenerateVideo, () => CanGenerateVideo);
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

    public RelayCommand SelectPhotoModeCommand { get; }
    public RelayCommand SelectVideoModeCommand { get; }
    public RelayCommand ContinueCommand { get; }
    public RelayCommand BrowseModelCommand { get; }
    public RelayCommand BrowseLoraCommand { get; }
    public RelayCommand BrowseInitImageCommand { get; }
    public RelayCommand ClearInitImageCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand BrowseVideoModelCommand { get; }
    public RelayCommand BrowseVideoEncoderCommand { get; }
    public RelayCommand BrowseVideoVaeCommand { get; }
    public RelayCommand BrowseVideoInitImageCommand { get; }
    public RelayCommand BrowseVideoEndImageCommand { get; }
    public RelayCommand BrowseVideoClipVisionCommand { get; }
    public RelayCommand BrowseVideoOutputCommand { get; }
    public RelayCommand OpenVideoOutputCommand { get; }
    public RelayCommand GenerateCommand { get; }
    public RelayCommand GenerateVideoCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenOutputCommand { get; }
    public RelayCommand<GenerationItem> OpenHistoryCommand { get; }
    public RelayCommand<GenerationItem> RemoveHistoryCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    public string EngineText => _engine.EngineExists ? "sd-cli.exe · подключён" : "sd-cli.exe · не найден";
    public bool EngineReady => _engine.EngineExists;

    public GenerationMode Mode
    {
        get => _mode;
        private set
        {
            if (_mode == value)
                return;
            _mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPhotoMode));
            OnPropertyChanged(nameof(IsVideoMode));
            SaveSettings();
        }
    }

    public bool IsModeSelectionVisible
    {
        get => _isModeSelectionVisible;
        private set => SetFlag(ref _isModeSelectionVisible, value);
    }

    public bool IsPhotoMode => Mode == GenerationMode.Photo;
    public bool IsVideoMode => Mode == GenerationMode.Video;

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
    public string InitImagePath { get => _initImagePath; set => SetText(ref _initImagePath, value); }
    public double InitImageStrength { get => _initImageStrength; set => SetNumber(ref _initImageStrength, value, 0.75, 0.05, 1.0); }

    public string VideoModelPath
 { get => _videoModelPath; set => SetText(ref _videoModelPath, value); }
    public string VideoTextEncoderPath { get => _videoTextEncoderPath; set => SetText(ref _videoTextEncoderPath, value); }
    public string VideoVaePath { get => _videoVaePath; set => SetText(ref _videoVaePath, value); }
    public string VideoInitImagePath { get => _videoInitImagePath; set => SetText(ref _videoInitImagePath, value); }
    public string VideoEndImagePath { get => _videoEndImagePath; set => SetText(ref _videoEndImagePath, value); }
    public string VideoClipVisionPath { get => _videoClipVisionPath; set => SetText(ref _videoClipVisionPath, value); }
    public string VideoOutputDirectory { get => _videoOutputDirectory; set => SetText(ref _videoOutputDirectory, value); }
    public string VideoPrompt { get => _videoPrompt; set => SetText(ref _videoPrompt, value); }
    public string VideoNegativePrompt { get => _videoNegativePrompt; set => SetText(ref _videoNegativePrompt, value); }

    public double VideoSteps { get => _videoSteps; set => SetNumber(ref _videoSteps, value, 25, 1, 50); }
    public double VideoCfg { get => _videoCfg; set => SetNumber(ref _videoCfg, value, 6, 1, 20); }
    public double VideoWidth { get => _videoWidth; set => SetNumber(ref _videoWidth, value, 768, 256, 2048); }
    public double VideoHeight { get => _videoHeight; set => SetNumber(ref _videoHeight, value, 768, 256, 2048); }
    public double VideoFrames { get => _videoFrames; set => SetNumber(ref _videoFrames, value, 49, 1, 300); }
    public double VideoFps { get => _videoFps; set => SetNumber(ref _videoFps, value, 24, 1, 60); }
    public double VideoSeed { get => _videoSeed; set => SetNumber(ref _videoSeed, value, 42, 1, 1000000000); }
    public bool VideoRandomSeed { get => _videoRandomSeed; set => SetFlag(ref _videoRandomSeed, value); }

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
        && (!UseLora || !string.IsNullOrWhiteSpace(LoraPath))
        && (string.IsNullOrWhiteSpace(InitImagePath) || File.Exists(InitImagePath));

    public bool CanGenerateVideo => !IsGenerating && EngineReady
        && !string.IsNullOrWhiteSpace(VideoModelPath)
        && File.Exists(VideoModelPath)
        && !string.IsNullOrWhiteSpace(VideoOutputDirectory)
        && (string.IsNullOrWhiteSpace(VideoTextEncoderPath) || File.Exists(VideoTextEncoderPath))
        && (string.IsNullOrWhiteSpace(VideoVaePath) || File.Exists(VideoVaePath))
        && (string.IsNullOrWhiteSpace(VideoInitImagePath) || File.Exists(VideoInitImagePath))
        && (string.IsNullOrWhiteSpace(VideoEndImagePath) || File.Exists(VideoEndImagePath))
        && (string.IsNullOrWhiteSpace(VideoClipVisionPath) || File.Exists(VideoClipVisionPath));

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

    public void SetInitImagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        InitImagePath = path;
        Status = "Исходник: " + Path.GetFileName(path);
        SaveSettings();
    }

    private void ClearInitImage()
    {
        InitImagePath = "";
        Status = "Исходник убран";
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

    public void SetVideoModelPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        VideoModelPath = path;
        Status = "Видеомодель: " + Path.GetFileName(path);
        SaveSettings();
    }

    public void SetVideoTextEncoderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        VideoTextEncoderPath = path;
        Status = "Текст енкодер: " + Path.GetFileName(path);
        SaveSettings();
    }

    public void SetVideoVaePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        VideoVaePath = path;
        Status = "VAE: " + Path.GetFileName(path);
        SaveSettings();
    }

    public void SetVideoOutputDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;
        VideoOutputDirectory = path;
        Status = "Папка видео: " + path;
        SaveSettings();
    }

    public void SetVideoInitImagePath(string path)
    {
        SetOptionalVideoFile(path, value => VideoInitImagePath = value, "Начальный кадр");
    }

    public void SetVideoEndImagePath(string path)
    {
        SetOptionalVideoFile(path, value => VideoEndImagePath = value, "Конечный кадр");
    }

    public void SetVideoClipVisionPath(string path)
    {
        SetOptionalVideoFile(path, value => VideoClipVisionPath = value, "Clip Vision");
    }

    private void SetOptionalVideoFile(string path, Action<string> apply, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        apply(path);
        Status = label + ": " + Path.GetFileName(path);
        SaveSettings();
    }

    private void SelectMode(GenerationMode mode)
    {
        if (IsGenerating)
            return;

        Mode = mode;
        IsModeSelectionVisible = false;
        PreviewImage = null;
        Status = mode == GenerationMode.Photo ? "Режим: генерация фото" : "Режим: генерация видео";
        AppendLog(Status);
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

    private void BrowseInitImage()
        => BrowseImageFile("Выберите исходное изображение", InitImagePath, SetInitImagePath);

    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog { FolderName = OutputDirectory };
        if (dialog.ShowDialog() == true)
            SetOutputDirectory(dialog.FolderName);
    }

    private void BrowseVideoModel() => BrowseModelFile("Выберите видеомодель", VideoModelPath, SetVideoModelPath);

    private void BrowseVideoEncoder() => BrowseModelFile("Выберите текст енкодер", VideoTextEncoderPath, SetVideoTextEncoderPath);

    private void BrowseVideoVae() => BrowseModelFile("Выберите VAE", VideoVaePath, SetVideoVaePath);

    private static void BrowseModelFile(string title, string currentPath, Action<string> apply)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Модели (*.gguf;*.safetensors)|*.gguf;*.safetensors|Все файлы|*.*",
            FileName = currentPath
        };
        if (dialog.ShowDialog() == true)
            apply(dialog.FileName);
    }

    private void BrowseVideoOutput()
    {
        var dialog = new OpenFolderDialog { FolderName = VideoOutputDirectory };
        if (dialog.ShowDialog() == true)
            SetVideoOutputDirectory(dialog.FolderName);
    }

    private void BrowseVideoInitImage()
        => BrowseImageFile("Выберите начальный кадр", VideoInitImagePath, SetVideoInitImagePath);

    private void BrowseVideoEndImage()
        => BrowseImageFile("Выберите конечный кадр", VideoEndImagePath, SetVideoEndImagePath);

    private void BrowseVideoClipVision()
        => BrowseModelFile("Выберите Clip Vision", VideoClipVisionPath, SetVideoClipVisionPath);

    private static void BrowseImageFile(string title, string currentPath, Action<string> apply)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Изображения (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|Все файлы|*.*",
            FileName = currentPath
        };
        if (dialog.ShowDialog() == true)
            apply(dialog.FileName);
    }

    private static int ResolveSeed(bool random, double configured)
        => random ? Random.Shared.Next(1, int.MaxValue) : (int)configured;

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

        if (!string.IsNullOrWhiteSpace(InitImagePath) && !File.Exists(InitImagePath))
        {
            Status = "Файл исходника не найден";
            AppendLog("Исходник не найден: " + InitImagePath);
            return;
        }

        var finalPrompt = JoinPrompt(Prompt, Pose);
        var loraName = Path.GetFileNameWithoutExtension(LoraPath);
        if (UseLora)
            finalPrompt += " <lora:" + loraName + ":" + LoraWeight.ToString("0.00", CultureInfo.InvariantCulture) + ">";

        var seed = ResolveSeed(RandomSeed, Seed);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var outputPath = Path.Combine(OutputDirectory, "gen_" + stamp + "_s" + seed.ToString(CultureInfo.InvariantCulture) + ".png");
        var request = new GenerationRequest(
            ModelPath,
            finalPrompt,
            NegativePrompt,
            Width,
            Height,
            Steps,
            Cfg,
            seed,
            UseLora,
            LoraPath,
            LoraWeight,
            InitImagePath,
            InitImageStrength,
            outputPath);

        IsGenerating = true;
        Status = "Генерация...";
        AppendLog("Запуск: " + Path.GetFileName(outputPath));
        AppendLog("Промпт: " + finalPrompt);
        AppendLog("Сид: " + seed.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(InitImagePath))
            AppendLog("Исходник: " + Path.GetFileName(InitImagePath) + " · сила " + InitImageStrength.ToString("0.00", CultureInfo.InvariantCulture));

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

    private async void GenerateVideo()
    {
        if (!CanGenerateVideo)
            return;

        if (!File.Exists(VideoModelPath))
        {
            Status = "Файл видеомодели не найден";
            AppendLog("Видеомодель не найдена: " + VideoModelPath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(VideoTextEncoderPath) && !File.Exists(VideoTextEncoderPath))
        {
            Status = "Файл текст енкодера не найден";
            AppendLog("Текст енкодер не найден: " + VideoTextEncoderPath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(VideoVaePath) && !File.Exists(VideoVaePath))
        {
            Status = "Файл VAE не найден";
            AppendLog("VAE не найден: " + VideoVaePath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(VideoInitImagePath) && !File.Exists(VideoInitImagePath))
        {
            Status = "Начальный кадр не найден";
            AppendLog("Начальный кадр не найден: " + VideoInitImagePath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(VideoEndImagePath) && !File.Exists(VideoEndImagePath))
        {
            Status = "Конечный кадр не найден";
            AppendLog("Конечный кадр не найден: " + VideoEndImagePath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(VideoClipVisionPath) && !File.Exists(VideoClipVisionPath))
        {
            Status = "Файл Clip Vision не найден";
            AppendLog("Clip Vision не найден: " + VideoClipVisionPath);
            return;
        }

        var finalPrompt = VideoPrompt.Trim();
        var seed = ResolveSeed(VideoRandomSeed, VideoSeed);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var seedSuffix = "_s" + seed.ToString(CultureInfo.InvariantCulture);
        var outputPath = Path.Combine(VideoOutputDirectory, "vid_" + stamp + seedSuffix + ".webm");
        var previewPath = Path.Combine(VideoOutputDirectory, "vid_" + stamp + seedSuffix + "_preview.png");
        var request = new VideoGenerationRequest(
            VideoModelPath,
            VideoTextEncoderPath,
            VideoVaePath,
            VideoInitImagePath,
            VideoEndImagePath,
            VideoClipVisionPath,
            finalPrompt,
            VideoNegativePrompt,
            VideoWidth,
            VideoHeight,
            VideoSteps,
            VideoCfg,
            seed,
            (int)VideoFrames,
            (int)VideoFps,
            outputPath,
            previewPath);

        IsGenerating = true;
        Status = "Генерация видео...";
        AppendLog("Запуск видео: " + Path.GetFileName(outputPath));
        AppendLog("Промпт: " + finalPrompt);
        AppendLog("Сид: " + seed.ToString(CultureInfo.InvariantCulture));

        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        var progress = new Progress<string>(AppendLog);

        var result = await _engine.RunVideoAsync(request, progress, _cancellation.Token);

        if (result.Success)
        {
            var item = CreateHistoryItem(result.OutputPath, finalPrompt);
            _history.Insert(0, item);
            while (_history.Count > 30)
                _history.RemoveAt(_history.Count - 1);
            SaveHistory();
            PreviewImage = LoadPreview(previewPath);
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

    private void OpenOutput() => OpenFolder(OutputDirectory);

    private void OpenVideoOutput() => OpenFolder(VideoOutputDirectory);

    private void OpenFolder(string folder)
    {
        var target = string.IsNullOrWhiteSpace(folder) ? AppContext.BaseDirectory : folder;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
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
        var isVideo = IsVideoFile(path);
        item.IsVideo = isVideo;
        if (name.Length > 11 && (name.StartsWith("gen_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("vid_", StringComparison.OrdinalIgnoreCase)))
        {
            var stamp = name[4..];
            var seedIndex = stamp.IndexOf("_s", StringComparison.Ordinal);
            if (seedIndex > 0)
                stamp = stamp[..seedIndex];
            if (DateTime.TryParseExact(stamp, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                item.CreatedAt = parsed;
        }
        var thumbnailSource = isVideo ? VideoPreviewPath(path) : path;
        if (File.Exists(thumbnailSource))
        {
            try { item.Thumbnail = GenerationItem.CreateThumbnail(thumbnailSource); } catch { }
        }
        return item;
    }

    private static bool IsVideoFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".avi", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase);
    }

    private static string VideoPreviewPath(string path)
        => Path.ChangeExtension(path, null) + "_preview.png";

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
        Mode = Mode,
        ModelPath = ModelPath,
        LoraPath = LoraPath,
        OutputDirectory = OutputDirectory,
        Prompt = Prompt,
        NegativePrompt = NegativePrompt,
        Pose = Pose,
        Steps = Steps,
        Cfg = Cfg,
        Width = Width,
        Height = Height,
        Seed = Seed,
        LoraWeight = LoraWeight,
        InitImagePath = InitImagePath,
        InitImageStrength = InitImageStrength,
        UseLora = UseLora,
        RandomSeed = RandomSeed,
        VideoModelPath = VideoModelPath,
        VideoTextEncoderPath = VideoTextEncoderPath,
        VideoVaePath = VideoVaePath,
        VideoInitImagePath = VideoInitImagePath,
        VideoEndImagePath = VideoEndImagePath,
        VideoClipVisionPath = VideoClipVisionPath,
        VideoOutputDirectory = VideoOutputDirectory,
        VideoPrompt = VideoPrompt,
        VideoNegativePrompt = VideoNegativePrompt,
        VideoSteps = VideoSteps,
        VideoCfg = VideoCfg,
        VideoWidth = VideoWidth,
        VideoHeight = VideoHeight,
        VideoFrames = VideoFrames,
        VideoFps = VideoFps,
        VideoSeed = VideoSeed,
        VideoRandomSeed = VideoRandomSeed
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
        ClearInitImageCommand.RaiseCanExecuteChanged();
        GenerateCommand.RaiseCanExecuteChanged();
        GenerateVideoCommand.RaiseCanExecuteChanged();
        SelectPhotoModeCommand.RaiseCanExecuteChanged();
        SelectVideoModeCommand.RaiseCanExecuteChanged();
        ContinueCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanGenerate));
        OnPropertyChanged(nameof(CanGenerateVideo));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
