using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SaemDesk.HtmlEditor;

/// <summary>
/// HtmlDocument 블록 트리를 Avalonia 컨트롤 트리로 변환한다.
/// </summary>
public static class HtmlRenderer
{
    // ── Block → Control ──────────────────────────────

    public static Control RenderBlock(BlockNode block) => block switch
    {
        ParagraphBlock p => RenderParagraph(p),
        ImageBlock img => RenderImage(img),
        TableBlock tbl => RenderTable(tbl),
        _ => new TextBlock { Text = "[unsupported]" }
    };

    private static Control RenderParagraph(ParagraphBlock p)
    {
        var tb = new SelectableTextBlock
        {
            TextAlignment = p.Alignment,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2)
        };

        (tb.FontSize, tb.FontWeight) = p.Tag switch
        {
            "h1" => (24d, FontWeight.Bold),
            "h2" => (20d, FontWeight.Bold),
            "h3" => (17d, FontWeight.Bold),
            _ => (14d, FontWeight.Normal)
        };

        foreach (var node in p.Inlines)
        {
            var inline = RenderInline(node);
            if (inline != null) tb.Inlines!.Add(inline);
        }

        return tb;
    }

    private static Control RenderImage(ImageBlock img)
    {
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            MaxWidth = img.Width ?? 600,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4)
        };

        if (img.Height.HasValue) image.MaxHeight = img.Height.Value;

        if (img.Source.StartsWith("data:"))
        {
            try
            {
                string base64 = img.Source[(img.Source.IndexOf(',') + 1)..];
                byte[] bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                image.Source = new Bitmap(ms);
            }
            catch { /* 로드 실패 시 빈 이미지 */ }
        }

        return image;
    }

    private static Control RenderTable(TableBlock tbl)
    {
        int maxCols = tbl.Rows.Count > 0
            ? tbl.Rows.Max(r => r.Cells.Sum(c => c.ColSpan))
            : 0;

        var grid = new Grid();
        for (int c = 0; c < maxCols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        for (int r = 0; r < tbl.Rows.Count; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int r = 0; r < tbl.Rows.Count; r++)
        {
            int col = 0;
            foreach (var cell in tbl.Rows[r].Cells)
            {
                var panel = new StackPanel { Margin = new Thickness(4) };
                foreach (var b in cell.Content)
                    panel.Children.Add(RenderBlock(b));

                var border = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0.5),
                    Child = panel
                };

                Grid.SetRow(border, r);
                Grid.SetColumn(border, col);
                if (cell.ColSpan > 1) Grid.SetColumnSpan(border, cell.ColSpan);
                if (cell.RowSpan > 1) Grid.SetRowSpan(border, cell.RowSpan);

                grid.Children.Add(border);
                col += cell.ColSpan;
            }
        }

        return new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4),
            Child = grid
        };
    }

    // ── Inline → Avalonia Inline ─────────────────────

    public static Inline? RenderInline(InlineNode node) => node switch
    {
        TextRun r => new Run(r.Text),
        LineBreakNode => new LineBreak(),
        FormattedSpan s => RenderSpan(s),
        _ => null
    };

    private static Inline? RenderSpan(FormattedSpan s)
    {
        if (s.Children.Count == 0) return null;

        // 링크 → InlineUIContainer (클릭 가능)
        if (s.Href != null)
            return RenderLink(s);

        // 단일 자식 → 직접 스타일 적용
        if (s.Children.Count == 1)
        {
            var inner = RenderInline(s.Children[0]);
            if (inner is Run run) ApplyStyle(run, s);
            return inner;
        }

        // 복수 자식 → Span 컨테이너
        var span = new Span();
        foreach (var child in s.Children)
        {
            var inner = RenderInline(child);
            if (inner != null) span.Inlines.Add(inner);
        }
        ApplySpanStyle(span, s);
        return span;
    }

    // ── 링크 렌더링 ─────────────────────────────────

    private static Inline RenderLink(FormattedSpan s)
    {
        string text = ExtractPlainText(s);
        string? href = s.Href;

        var tb = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse("#336BCC")),
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = href
        };

        tb.PointerPressed += (sender, e) =>
        {
            if (sender is TextBlock t && t.Tag is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch { /* 브라우저 실행 실패 무시 */ }
                e.Handled = true; // 블록 클릭 이벤트 차단
            }
        };

        return new InlineUIContainer { Child = tb };
    }

    private static string ExtractPlainText(FormattedSpan s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var child in s.Children)
        {
            if (child is TextRun tr) sb.Append(tr.Text);
            else if (child is FormattedSpan fs) sb.Append(ExtractPlainText(fs));
        }
        return sb.ToString();
    }

    // ── 스타일 적용 ─────────────────────────────────

    private static void ApplyStyle(Run run, FormattedSpan s)
    {
        if (s.Bold == true) run.FontWeight = FontWeight.Bold;
        if (s.Italic == true) run.FontStyle = FontStyle.Italic;
        if (s.Underline == true) run.TextDecorations = TextDecorations.Underline;
        if (s.Strikethrough == true) run.TextDecorations = TextDecorations.Strikethrough;
        if (s.FontSize.HasValue) run.FontSize = s.FontSize.Value;
        TrySetForeground(c => run.Foreground = c, s.Color);
    }

    private static void ApplySpanStyle(Span span, FormattedSpan s)
    {
        if (s.Bold == true) span.FontWeight = FontWeight.Bold;
        if (s.Italic == true) span.FontStyle = FontStyle.Italic;
        if (s.Underline == true) span.TextDecorations = TextDecorations.Underline;
        if (s.Strikethrough == true) span.TextDecorations = TextDecorations.Strikethrough;
        if (s.FontSize.HasValue) span.FontSize = s.FontSize.Value;
        TrySetForeground(c => span.Foreground = c, s.Color);
    }

    private static void TrySetForeground(Action<IBrush> setter, string? color)
    {
        if (color == null) return;
        try { setter(new SolidColorBrush(Color.Parse(color))); }
        catch { /* 파싱 실패 무시 */ }
    }
}
