using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SaemDesk.Views.Controls;

/// <summary>
/// MemoBoard 카테고리명을 칩 배경색(SolidColorBrush)으로 매핑.
/// </summary>
public sealed class CategoryColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string ?? string.Empty;
        var color = key switch
        {
            "수업" => Color.Parse("#5B7FB8"),
            "학급" => Color.Parse("#4CAF50"),
            "업무" => Color.Parse("#FF9800"),
            "개인" => Color.Parse("#9C27B0"),
            _ => Color.Parse("#888888"),
        };
        return new SolidColorBrush(color);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// HTML 본문을 인라인 미리보기용 단순 텍스트로 변환 (태그 제거 + 공백 정규화).
/// </summary>
public sealed class HtmlToPlainConverter : IValueConverter
{
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WsRegex = new(@"\s+", RegexOptions.Compiled);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrEmpty(s)) return string.Empty;
        var stripped = TagRegex.Replace(s, " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">");
        return WsRegex.Replace(stripped, " ").Trim();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
