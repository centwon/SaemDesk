using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SaemDesk.Views.Controls;

/// <summary>방학 여부에 따라 텍스트 색상 결정 — 방학:OrangeRed, 일반:Theme 기본.</summary>
public sealed class BoolToVacationColorConverter : IValueConverter
{
    public static readonly BoolToVacationColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b
            ? new SolidColorBrush(Color.Parse("#E74C3C"))
            : Brushes.Black;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
