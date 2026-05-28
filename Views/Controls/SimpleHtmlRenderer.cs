using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 간이 HTML → Avalonia 컨트롤 변환기.
/// 메모 카드 미리보기용: 텍스트 + 이미지 썸네일 + 하이퍼링크.
/// </summary>
public static class SimpleHtmlRenderer
{
    private static readonly Regex LinkRegex = new(
        @"<a\s[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);

    private static readonly SolidColorBrush TextBrush =
        new(Color.FromArgb(180, 0, 0, 0));

    private static readonly SolidColorBrush LinkBrush =
        new(Color.FromRgb(0x33, 0x6B, 0xCC));

    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.Add("User-Agent", "SaemDesk/1.0");
        return c;
    });

    /// <summary>
    /// HTML을 파싱해 텍스트 + 이미지 + 링크를 포함한 Panel을 반환.
    /// </summary>
    public static Control Render(string? html, int maxImages = 2, int maxLinks = 3)
    {
        var panel = new StackPanel { Spacing = 4 };
        if (string.IsNullOrWhiteSpace(html)) return panel;

        // 1) 텍스트 미리보기
        string plain = HtmlToPlainConverter.Strip(html);
        if (!string.IsNullOrEmpty(plain))
        {
            panel.Children.Add(new TextBlock
            {
                Text         = plain,
                FontSize     = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxLines     = 4,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground   = TextBrush,
            });
        }

        // 2) 이미지 썸네일
        var imgSrcs = ExtractImgSrcs(html);
        int imgCount = 0;
        foreach (string src in imgSrcs)
        {
            if (imgCount >= maxImages) break;

            // 동기 로드 (data URI, 로컬 파일)
            var img = TryLoadImageSync(src);
            if (img is not null)
            {
                panel.Children.Add(img);
                imgCount++;
            }
            else if (src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // HTTP → 플레이스홀더 + 비동기 로드
                var placeholder = new Image
                {
                    MaxHeight           = 120,
                    MaxWidth            = 200,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Stretch             = Stretch.Uniform,
                    Margin              = new Thickness(0, 4, 0, 0),
                };
                panel.Children.Add(placeholder);
                _ = LoadHttpImageAsync(placeholder, src);
                imgCount++;
            }
        }

        // 3) 하이퍼링크
        int linkCount = 0;
        foreach (Match m in LinkRegex.Matches(html))
        {
            if (linkCount >= maxLinks) break;
            string url  = m.Groups[1].Value;
            string text = TagRegex.Replace(m.Groups[2].Value, "").Trim();
            if (string.IsNullOrEmpty(text)) text = url;

            var link = new TextBlock
            {
                Text            = $"🔗 {text}",
                FontSize        = 11,
                Foreground      = LinkBrush,
                TextDecorations = TextDecorations.Underline,
                Cursor          = new Cursor(StandardCursorType.Hand),
                TextTrimming    = TextTrimming.CharacterEllipsis,
                MaxLines        = 1,
                Margin          = new Thickness(0, 2, 0, 0),
            };
            string capturedUrl = url;
            link.PointerPressed += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(capturedUrl) { UseShellExecute = true }); }
                catch { /* 무시 */ }
            };
            panel.Children.Add(link);
            linkCount++;
        }

        return panel;
    }

    // ────────────────────────────────────────────────────
    //  <img src="..."> 수동 파싱 — base64 data URI 안전 처리
    // ────────────────────────────────────────────────────

    internal static List<string> ExtractImgSrcs(string html)
    {
        var results = new List<string>();
        int pos = 0;
        while (pos < html.Length)
        {
            int imgStart = html.IndexOf("<img ", pos, StringComparison.OrdinalIgnoreCase);
            if (imgStart < 0)
                imgStart = html.IndexOf("<img\t", pos, StringComparison.OrdinalIgnoreCase);
            if (imgStart < 0) break;

            int tagEnd = html.IndexOf('>', imgStart);
            if (tagEnd < 0) break;

            int srcIdx = html.IndexOf("src=", imgStart, StringComparison.OrdinalIgnoreCase);
            if (srcIdx < 0 || srcIdx > tagEnd)
            {
                pos = tagEnd + 1;
                continue;
            }

            int eqPos = srcIdx + 4;
            if (eqPos >= html.Length) break;

            char quote = html[eqPos];
            if (quote != '"' && quote != '\'')
            {
                pos = tagEnd + 1;
                continue;
            }

            int srcStart = eqPos + 1;
            int srcEnd = html.IndexOf(quote, srcStart);
            if (srcEnd < 0) break;

            results.Add(html[srcStart..srcEnd]);
            pos = srcEnd + 1;
        }
        return results;
    }

    // ────────────────────────────────────────────────────
    //  동기 이미지 로딩 (data URI, 로컬 파일)
    // ────────────────────────────────────────────────────

    private static Image? TryLoadImageSync(string src)
    {
        try
        {
            Bitmap? bitmap = null;

            if (src.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                int comma = src.IndexOf(',');
                if (comma > 0)
                {
                    byte[] bytes = Convert.FromBase64String(src[(comma + 1)..]);
                    using var ms = new MemoryStream(bytes);
                    bitmap = new Bitmap(ms);
                }
            }
            else if (src.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                string path = new Uri(src).LocalPath;
                if (File.Exists(path)) bitmap = new Bitmap(path);
            }
            else if (!src.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                     && File.Exists(src))
            {
                bitmap = new Bitmap(src);
            }

            if (bitmap is null) return null;

            return new Image
            {
                Source              = bitmap,
                MaxHeight           = 120,
                MaxWidth            = 200,
                HorizontalAlignment = HorizontalAlignment.Left,
                Stretch             = Stretch.Uniform,
                Margin              = new Thickness(0, 4, 0, 0),
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SimpleHtmlRenderer] 이미지 로드 실패: {ex.Message}");
            return null;
        }
    }

    // ────────────────────────────────────────────────────
    //  비동기 HTTP 이미지 로딩
    // ────────────────────────────────────────────────────

    private static async Task LoadHttpImageAsync(Image target, string url)
    {
        try
        {
            byte[] data = await Http.Value.GetByteArrayAsync(url);
            using var ms = new MemoryStream(data);
            var bitmap = new Bitmap(ms);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                target.Source = bitmap;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SimpleHtmlRenderer] HTTP 이미지 실패: {ex.Message}");
        }
    }
}
