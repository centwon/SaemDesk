using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SaemDesk.ViewModels;

/// <summary>학기(1/2) ↔ ComboBox 인덱스(0/1) 변환.</summary>
public sealed class SemesterIndexConverter : IValueConverter
{
    public static readonly SemesterIndexConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? Math.Max(0, i - 1) : 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? i + 1 : 1;
}
