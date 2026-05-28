using System.Text;
using Avalonia.Media;

namespace SaemDesk.HtmlEditor;

public static class HtmlSerializer
{
    public static string Serialize(HtmlDocument doc)
    {
        var sb = new StringBuilder();
        foreach (var block in doc.Blocks)
            WriteBlock(sb, block);
        return sb.ToString();
    }

    // ── Block ────────────────────────────────────────

    private static void WriteBlock(StringBuilder sb, BlockNode block)
    {
        switch (block)
        {
            case ParagraphBlock p:
                sb.Append('<').Append(p.Tag);
                if (p.Alignment != TextAlignment.Left)
                    sb.Append($" style=\"text-align: {ToCss(p.Alignment)}\"");
                sb.Append('>');
                foreach (var n in p.Inlines) WriteInline(sb, n);
                sb.Append("</").Append(p.Tag).Append('>');
                break;

            case ImageBlock img:
                sb.Append("<img");
                sb.Append($" src=\"{Esc(img.Source)}\"");
                if (img.Alt != null) sb.Append($" alt=\"{Esc(img.Alt)}\"");
                if (img.Width.HasValue) sb.Append($" width=\"{img.Width.Value}\"");
                if (img.Height.HasValue) sb.Append($" height=\"{img.Height.Value}\"");
                sb.Append(" />");
                break;

            case TableBlock tbl:
                sb.Append("<table>");
                foreach (var row in tbl.Rows)
                {
                    sb.Append("<tr>");
                    foreach (var cell in row.Cells)
                    {
                        sb.Append("<td");
                        if (cell.ColSpan > 1) sb.Append($" colspan=\"{cell.ColSpan}\"");
                        if (cell.RowSpan > 1) sb.Append($" rowspan=\"{cell.RowSpan}\"");
                        sb.Append('>');
                        foreach (var cb in cell.Content) WriteBlock(sb, cb);
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
                break;
        }
    }

    // ── Inline ───────────────────────────────────────

    private static void WriteInline(StringBuilder sb, InlineNode node)
    {
        switch (node)
        {
            case TextRun r:
                sb.Append(Esc(r.Text));
                break;

            case LineBreakNode:
                sb.Append("<br />");
                break;

            case FormattedSpan s:
                var (open, close) = BuildTags(s);
                sb.Append(open);
                foreach (var child in s.Children) WriteInline(sb, child);
                sb.Append(close);
                break;
        }
    }

    private static (string open, string close) BuildTags(FormattedSpan s)
    {
        // href → <a>
        if (s.Href != null)
            return ($"<a href=\"{Esc(s.Href)}\">", "</a>");

        // 시맨틱 태그 누적 (복합 서식 지원: Bold+Italic 등)
        var openParts = new List<string>();
        var closeParts = new List<string>();

        if (s.Bold == true) { openParts.Add("<strong>"); closeParts.Add("</strong>"); }
        if (s.Italic == true) { openParts.Add("<em>"); closeParts.Add("</em>"); }
        if (s.Underline == true) { openParts.Add("<u>"); closeParts.Add("</u>"); }
        if (s.Strikethrough == true) { openParts.Add("<s>"); closeParts.Add("</s>"); }

        // CSS style (color, font-size)
        var styles = new List<string>();
        if (s.Color != null) styles.Add($"color: {s.Color}");
        if (s.FontSize.HasValue) styles.Add($"font-size: {s.FontSize.Value}px");

        if (styles.Count > 0)
        {
            openParts.Add($"<span style=\"{string.Join("; ", styles)}\">");
            closeParts.Add("</span>");
        }

        if (openParts.Count == 0) return ("", "");

        // close 태그는 역순 (올바른 중첩)
        closeParts.Reverse();
        return (string.Join("", openParts), string.Join("", closeParts));
    }

    // ── Helpers ──────────────────────────────────────

    private static string ToCss(TextAlignment a) => a switch
    {
        TextAlignment.Center => "center",
        TextAlignment.Right => "right",
        TextAlignment.Justify => "justify",
        _ => "left"
    };

    private static string Esc(string t) => t
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
