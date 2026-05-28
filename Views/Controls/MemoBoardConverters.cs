using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 카테고리명 → 배지 배경색 (SolidColorBrush).
/// 헤더 칩 및 카드 배지에 사용.
/// </summary>
public sealed class CategoryColorConverter : IValueConverter
{
    private static readonly (string cat, Color color)[] Map =
    {
        ("수업", Color.Parse("#4A7CC7")),
        ("학급", Color.Parse("#3A9E5F")),
        ("업무", Color.Parse("#C05050")),
        ("개인", Color.Parse("#8B5FC0")),
    };

    public static SolidColorBrush GetBrush(string category)
    {
        foreach (var (cat, color) in Map)
            if (cat == category) return new SolidColorBrush(color);
        return new SolidColorBrush(Color.Parse("#888888"));
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => GetBrush(value as string ?? string.Empty);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 카테고리명 → 포스트잇 카드 배경색 (파스텔).
/// </summary>
public sealed class CategoryBgConverter : IValueConverter
{
    private static readonly (string cat, Color color)[] Map =
    {
        ("수업", Color.Parse("#F0F4FA")),   // 극연 블루
        ("학급", Color.Parse("#F0F7F2")),   // 극연 그린
        ("업무", Color.Parse("#FAF0F0")),   // 극연 핑크
        ("개인", Color.Parse("#F4F0F8")),   // 극연 퍼플
    };

    public static SolidColorBrush GetBrush(string category)
    {
        foreach (var (cat, color) in Map)
            if (cat == category) return new SolidColorBrush(color);
        return new SolidColorBrush(Color.Parse("#F4F5F7")); // 기본 뉴트럴
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => GetBrush(value as string ?? string.Empty);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 카테고리명 → 카드 상단 줄(stripe) 색.
/// CategoryColorConverter 와 동일한 원색 계열, 약간 진하게.
/// </summary>
public sealed class CategoryStripeConverter : IValueConverter
{
    private static readonly (string cat, Color color)[] Map =
    {
        ("수업", Color.Parse("#5B8BD4")),
        ("학급", Color.Parse("#4CAF72")),
        ("업무", Color.Parse("#D06060")),
        ("개인", Color.Parse("#9070C0")),
    };

    public static SolidColorBrush GetBrush(string category)
    {
        foreach (var (cat, color) in Map)
            if (cat == category) return new SolidColorBrush(color);
        return new SolidColorBrush(Color.Parse("#AAAAAA"));
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => GetBrush(value as string ?? string.Empty);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// HTML 본문 → 인라인 미리보기용 plain text (태그 제거 + 공백 정규화).
/// </summary>
public sealed class HtmlToPlainConverter : IValueConverter
{
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WsRegex  = new(@"\s+",    RegexOptions.Compiled);

    public static string Strip(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var s = TagRegex.Replace(html, " ")
                        .Replace("&nbsp;", " ")
                        .Replace("&amp;",  "&")
                        .Replace("&lt;",   "<")
                        .Replace("&gt;",   ">");
        return WsRegex.Replace(s, " ").Trim();
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Strip(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
