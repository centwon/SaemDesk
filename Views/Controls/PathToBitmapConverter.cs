using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 파일 경로(string) → <see cref="Bitmap"/>. 빈 경로/없는 파일이면 null.
/// </summary>
public sealed class PathToBitmapConverter : IValueConverter
{
    public static readonly PathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;

        string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        if (!File.Exists(fullPath)) return null;

        try
        {
            using var fs = File.OpenRead(fullPath);
            return Bitmap.DecodeToWidth(fs, 400);
        }
        catch { return null; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
