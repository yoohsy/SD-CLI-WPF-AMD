using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CdCliWpf.ViewModels;

namespace CdCliWpf;

public partial class MainWindow : Window
{
    private const string MaximizePath = "M3,3 L17,3 L17,17 L3,17 Z M5,5 L5,15 L15,15 L15,5 Z";
    private const string RestorePath = "M6,3 L17,3 L17,14 L15,14 L15,5 L6,5 Z M3,6 L14,6 L14,17 L3,17 Z";

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        StateChanged += (_, _) => UpdateMaximizeIcon();
        UpdateMaximizeIcon();
    }

    public MainViewModel ViewModel { get; }

    protected override void OnClosing(CancelEventArgs e)
    {
        ViewModel.Persist();
        base.OnClosing(e);
    }

    private void UpdateMaximizeIcon()
        => MaxIcon.Data = Geometry.Parse(WindowState == WindowState.Maximized ? RestorePath : MaximizePath);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;

        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void DropTarget_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void ModelDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetModelPath, ".gguf", ".safetensors");

    private void LoraDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetLoraPath, ".safetensors");

    private void InitImageDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetInitImagePath, ".png", ".jpg", ".jpeg", ".webp", ".bmp");

    private void OutputDrop_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
            return;

        var path = files[0];
        if (Directory.Exists(path))
            ViewModel.SetOutputDirectory(path);
        else if (File.Exists(path))
            ViewModel.SetOutputDirectory(Path.GetDirectoryName(path) ?? "");

        e.Handled = true;
    }

    private void VideoModelDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetVideoModelPath, ".gguf", ".safetensors");

    private void VideoEncoderDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetVideoTextEncoderPath, ".gguf", ".safetensors");

    private void VideoVaeDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetVideoVaePath, ".gguf", ".safetensors");

    private void VideoInitImageDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetVideoInitImagePath, ".png", ".jpg", ".jpeg", ".webp", ".bmp");

    private void VideoEndImageDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetVideoEndImagePath, ".png", ".jpg", ".jpeg", ".webp", ".bmp");

    private void VideoClipVisionDrop_PreviewDrop(object sender, DragEventArgs e)
        => HandleFileDrop(e, ViewModel.SetVideoClipVisionPath, ".gguf", ".safetensors");

    private void VideoOutputDrop_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
            return;

        var path = files[0];
        if (Directory.Exists(path))
            ViewModel.SetVideoOutputDirectory(path);
        else if (File.Exists(path))
            ViewModel.SetVideoOutputDirectory(Path.GetDirectoryName(path) ?? "");

        e.Handled = true;
    }

    private void ModeToggle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => e.Handled = true;

    private static void HandleFileDrop(DragEventArgs e, Action<string> apply, params string[] extensions)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
            return;

        var path = files[0];
        if (extensions.Length > 0 && !extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            return;

        apply(path);
        e.Handled = true;
    }

    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is GenerationItem item)
            ViewModel.OpenHistoryCommand.Execute(item);
    }
}
