using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SaemDesk.Views.Controls;

/// <summary>HEX 문자열(예: "#FF6B9BD1") → <see cref="IBrush"/>.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && Color.TryParse(hex, out var c))
            return new SolidColorBrush(c);
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
