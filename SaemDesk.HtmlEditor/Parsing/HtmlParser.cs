using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Media;

namespace SaemDesk.HtmlEditor;

public static class HtmlParser
{
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "h1", "h2", "h3", "h4", "h5", "h6",
        "table", "thead", "tbody", "tfoot", "tr", "td", "th",
        "img", "blockquote", "hr"
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "br", "hr", "img"
    };

    // ── Public API ───────────────────────────────────

    public static HtmlDocument Parse(string html)
    {
        var doc = new HtmlDocument();
        if (string.IsNullOrEmpty(html)) return doc;

        var tokens = Tokenize(html);
        int pos = 0;
        ParseBlockContent(tokens, ref pos, doc.Blocks, stopTag: null);
        return doc;
    }

    // ── Tokenizer ────────────────────────────────────

    private enum TokenType { Text, Open, Close, SelfClose }

    private sealed record Token(TokenType Type, string Tag, string Attrs, string Text);

    private static List<Token> Tokenize(string html)
    {
        var tokens = new List<Token>();
        var buf = new StringBuilder();
        int i = 0;

        while (i < html.Length)
        {
            if (html[i] == '<')
            {
                if (buf.Length > 0)
                {
                    tokens.Add(new Token(TokenType.Text, "", "", DecodeEntities(buf.ToString())));
                    buf.Clear();
                }

                int gt = html.IndexOf('>', i);
                if (gt < 0) { buf.Append(html[i++]); continue; }

                string inside = html[(i + 1)..gt].Trim();
                i = gt + 1;

                if (inside.StartsWith('/'))
                {
                    string tag = inside[1..].Trim().Split(' ', '/', '>')[0].ToLowerInvariant();
                    tokens.Add(new Token(TokenType.Close, tag, "", ""));
                }
                else
                {
                    bool selfClose = inside.EndsWith('/');
                    string content = selfClose ? inside[..^1].Trim() : inside;
                    SplitTagAttrs(content, out var tag, out var attrs);

                    if (selfClose || VoidTags.Contains(tag))
                        tokens.Add(new Token(TokenType.SelfClose, tag, attrs, ""));
                    else
                        tokens.Add(new Token(TokenType.Open, tag, attrs, ""));
                }
            }
            else
            {
                buf.Append(html[i++]);
            }
        }

        if (buf.Length > 0)
            tokens.Add(new Token(TokenType.Text, "", "", DecodeEntities(buf.ToString())));

        return tokens;
    }

    private static void SplitTagAttrs(string content, out string tag, out string attrs)
    {
        int sp = content.IndexOfAny([' ', '\t', '\n', '\r']);
        if (sp < 0) { tag = content.ToLowerInvariant(); attrs = ""; }
        else { tag = content[..sp].ToLowerInvariant(); attrs = content[sp..].Trim(); }
    }

    // ── Block-level Parser ───────────────────────────

    private static void ParseBlockContent(List<Token> tokens, ref int pos,
        List<BlockNode> blocks, string? stopTag)
    {
        var pending = new List<InlineNode>();

        while (pos < tokens.Count)
        {
            var tok = tokens[pos];

            // 닫는 태그
            if (tok.Type == TokenType.Close)
            {
                if (tok.Tag == stopTag) { FlushInlines(pending, blocks); pos++; return; }
                pos++; continue;
            }

            // 셀프 클로즈 태그
            if (tok.Type == TokenType.SelfClose)
            {
                if (tok.Tag == "img")
                {
                    FlushInlines(pending, blocks);
                    blocks.Add(ParseImage(tok.Attrs));
                }
                else if (tok.Tag == "br")
                    pending.Add(new LineBreakNode());

                pos++; continue;
            }

            // 텍스트
            if (tok.Type == TokenType.Text)
            {
                string text = CollapseWhitespace(tok.Text);
                if (!string.IsNullOrEmpty(text) || pending.Count > 0)
                    if (!string.IsNullOrEmpty(text))
                        pending.Add(new TextRun(text));
                pos++; continue;
            }

            // 여는 태그
            if (tok.Type == TokenType.Open)
            {
                if (tok.Tag == "table")
                {
                    FlushInlines(pending, blocks);
                    pos++;
                    blocks.Add(ParseTable(tokens, ref pos));
                }
                else if (tok.Tag is "p" or "div" or "h1" or "h2" or "h3"
                         or "h4" or "h5" or "h6" or "blockquote")
                {
                    FlushInlines(pending, blocks);
                    pos++;
                    var para = new ParagraphBlock
                    {
                        Tag = tok.Tag,
                        Alignment = ParseAlignment(tok.Attrs)
                    };
                    ParseInlineContent(tokens, ref pos, para.Inlines, tok.Tag);
                    blocks.Add(para);
                }
                else if (tok.Tag is "thead" or "tbody" or "tfoot" or "tr" or "td" or "th")
                {
                    FlushInlines(pending, blocks);
                    pos++;
                    ParseBlockContent(tokens, ref pos, blocks, tok.Tag);
                }
                else
                {
                    // 인라인 태그 → pending에 축적
                    ParseInlineNode(tokens, ref pos, pending);
                }
            }
        }

        FlushInlines(pending, blocks);
    }

    // ── Inline-level Parser ──────────────────────────

    private static void ParseInlineContent(List<Token> tokens, ref int pos,
        List<InlineNode> inlines, string stopTag)
    {
        while (pos < tokens.Count)
        {
            var tok = tokens[pos];

            if (tok.Type == TokenType.Close)
            {
                if (tok.Tag == stopTag) { pos++; return; }
                pos++; continue;
            }

            if (tok.Type == TokenType.SelfClose)
            {
                if (tok.Tag == "br") inlines.Add(new LineBreakNode());
                pos++; continue;
            }

            if (tok.Type == TokenType.Text)
            {
                string text = CollapseWhitespace(tok.Text);
                if (!string.IsNullOrEmpty(text))
                    inlines.Add(new TextRun(text));
                pos++; continue;
            }

            if (tok.Type == TokenType.Open)
            {
                if (BlockTags.Contains(tok.Tag)) return; // 블록 태그 만남 → 호출자에게 위임
                ParseInlineNode(tokens, ref pos, inlines);
            }
        }
    }

    private static void ParseInlineNode(List<Token> tokens, ref int pos,
        List<InlineNode> inlines)
    {
        var tok = tokens[pos];
        if (tok.Type != TokenType.Open) { pos++; return; }

        pos++;
        var span = new FormattedSpan();
        ApplyTagStyle(span, tok.Tag, tok.Attrs);
        ParseInlineContent(tokens, ref pos, span.Children, tok.Tag);

        if (HasStyle(span))
            inlines.Add(span);
        else
            inlines.AddRange(span.Children);
    }

    // ── Table Parser ─────────────────────────────────

    private static TableBlock ParseTable(List<Token> tokens, ref int pos)
    {
        var table = new TableBlock();

        while (pos < tokens.Count)
        {
            var tok = tokens[pos];
            if (tok.Type == TokenType.Close && tok.Tag == "table") { pos++; break; }

            if (tok.Type == TokenType.Open && tok.Tag == "tr")
            {
                pos++;
                table.Rows.Add(ParseTableRow(tokens, ref pos));
            }
            else if (tok.Type == TokenType.Open && tok.Tag is "thead" or "tbody" or "tfoot")
            {
                pos++;
                while (pos < tokens.Count)
                {
                    var t = tokens[pos];
                    if (t.Type == TokenType.Close && t.Tag is "thead" or "tbody" or "tfoot")
                    { pos++; break; }
                    if (t.Type == TokenType.Open && t.Tag == "tr")
                    { pos++; table.Rows.Add(ParseTableRow(tokens, ref pos)); }
                    else pos++;
                }
            }
            else pos++;
        }

        return table;
    }

    private static TableRow ParseTableRow(List<Token> tokens, ref int pos)
    {
        var row = new TableRow();

        while (pos < tokens.Count)
        {
            var tok = tokens[pos];
            if (tok.Type == TokenType.Close && tok.Tag == "tr") { pos++; break; }

            if (tok.Type == TokenType.Open && tok.Tag is "td" or "th")
            {
                pos++;
                var cell = new TableCell
                {
                    ColSpan = ParseIntAttr(tok.Attrs, "colspan", 1),
                    RowSpan = ParseIntAttr(tok.Attrs, "rowspan", 1)
                };
                ParseCellContent(tokens, ref pos, cell, tok.Tag);
                row.Cells.Add(cell);
            }
            else pos++;
        }

        return row;
    }

    private static void ParseCellContent(List<Token> tokens, ref int pos,
        TableCell cell, string stopTag)
    {
        var inlines = new List<InlineNode>();

        while (pos < tokens.Count)
        {
            var tok = tokens[pos];
            if (tok.Type == TokenType.Close && tok.Tag == stopTag) { pos++; break; }

            if (tok.Type == TokenType.Text)
            {
                string text = CollapseWhitespace(tok.Text);
                if (!string.IsNullOrEmpty(text))
                    inlines.Add(new TextRun(text));
                pos++; continue;
            }

            if (tok.Type == TokenType.SelfClose)
            {
                if (tok.Tag == "br") inlines.Add(new LineBreakNode());
                pos++; continue;
            }

            if (tok.Type == TokenType.Open && tok.Tag is "p" or "div")
            {
                FlushInlines(inlines, cell.Content);
                pos++;
                var para = new ParagraphBlock
                {
                    Tag = tok.Tag,
                    Alignment = ParseAlignment(tok.Attrs)
                };
                ParseInlineContent(tokens, ref pos, para.Inlines, tok.Tag);
                cell.Content.Add(para);
                continue;
            }

            if (tok.Type == TokenType.Open)
            {
                ParseInlineNode(tokens, ref pos, inlines);
                continue;
            }

            pos++;
        }

        FlushInlines(inlines, cell.Content);
    }

    // ── Helpers ──────────────────────────────────────

    private static void FlushInlines(List<InlineNode> inlines, List<BlockNode> blocks)
    {
        if (inlines.Count == 0) return;

        bool allWhitespace = inlines.All(n => n is TextRun tr && string.IsNullOrWhiteSpace(tr.Text));
        if (!allWhitespace)
        {
            var para = new ParagraphBlock();
            para.Inlines.AddRange(inlines);
            blocks.Add(para);
        }

        inlines.Clear();
    }

    private static void ApplyTagStyle(FormattedSpan span, string tag, string attrs)
    {
        switch (tag)
        {
            case "b" or "strong":
                span.Bold = true; break;
            case "i" or "em":
                span.Italic = true; break;
            case "u":
                span.Underline = true; break;
            case "s" or "del":
                span.Strikethrough = true; break;
            case "a":
                span.Underline = true;
                span.Color = "#336BCC";
                span.Href = ParseAttr(attrs, "href");
                break;
            case "span":
                var color = ParseStyleProp(attrs, "color");
                if (color != null) span.Color = color;
                if (TryParsePx(ParseStyleProp(attrs, "font-size"), out var px))
                    span.FontSize = px;
                break;
        }
    }

    private static bool HasStyle(FormattedSpan s) =>
        s.Bold == true || s.Italic == true || s.Underline == true ||
        s.Strikethrough == true || s.Color != null || s.FontSize != null || s.Href != null;

    private static ImageBlock ParseImage(string attrs) => new()
    {
        Source = ParseAttr(attrs, "src") ?? "",
        Alt = ParseAttr(attrs, "alt"),
        Width = TryParsePx(ParseAttr(attrs, "width"), out var w) ? w : null,
        Height = TryParsePx(ParseAttr(attrs, "height"), out var h) ? h : null
    };

    private static TextAlignment ParseAlignment(string attrs) =>
        ParseStyleProp(attrs, "text-align")?.ToLowerInvariant() switch
        {
            "center" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            "justify" => TextAlignment.Justify,
            _ => TextAlignment.Left
        };

    private static string? ParseAttr(string attrs, string name)
    {
        var m = Regex.Match(attrs, $@"{name}\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(attrs, $@"{name}\s*=\s*(\S+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ParseStyleProp(string attrs, string property)
    {
        var style = ParseAttr(attrs, "style");
        if (style == null) return null;
        var m = Regex.Match(style, $@"{Regex.Escape(property)}\s*:\s*([^;""']+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static int ParseIntAttr(string attrs, string name, int def)
    {
        var v = ParseAttr(attrs, name);
        return v != null && int.TryParse(v, out var n) ? n : def;
    }

    private static bool TryParsePx(string? value, out double result)
    {
        result = 0;
        if (value == null) return false;
        return double.TryParse(value.AsSpan().TrimEnd("pxPX ".ToCharArray()), out result);
    }

    private static string CollapseWhitespace(string text) =>
        text.Replace('\r', ' ').Replace('\n', ' ');

    private static string DecodeEntities(string text) => text
        .Replace("&amp;", "&")
        .Replace("&lt;", "<")
        .Replace("&gt;", ">")
        .Replace("&nbsp;", " ")
        .Replace("&quot;", "\"")
        .Replace("&#39;", "'")
        .Replace("&apos;", "'");
}
