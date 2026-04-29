using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace SaemDesk.Converters;

/// <summary>
/// double → GridLength 변환.
///  0  → GridLength(0)        숨김
/// -1  → GridLength(1, Star)  나머지 전체
///  N  → GridLength(N)        고정 픽셀
/// </summary>
public sealed class DoubleToGridLengthConverter : IValueConverter
{
    public static readonly DoubleToGridLengthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double d = value is double v ? v : 0;
        if (d < 0) return new GridLength(1, GridUnitType.Star);
        if (d == 0) return new GridLength(0);
        return new GridLength(d);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
