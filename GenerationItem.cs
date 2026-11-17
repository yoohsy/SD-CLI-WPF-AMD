using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace CdCliWpf;

public sealed class GenerationItem
{
    public string Path { get; set; } = "";
    public string Prompt { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public BitmapImage? Thumbnail { get; set; }

    public string Title => "gen_" + CreatedAt.ToString("HHmmss");
    public string TimeText => CreatedAt.ToString("dd.MM  HH:mm");

    public static BitmapImage CreateThumbnail(string path, int decodeWidth = 110)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = decodeWidth;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
