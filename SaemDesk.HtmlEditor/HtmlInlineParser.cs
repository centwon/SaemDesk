using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace SaemDesk.HtmlEditor;

/// <summary>
/// HTML 소스를 Avalonia Inline 객체(Run, LineBreak)로 변환하는 파서.
/// SelectableTextBlock / TextBlock 의 Inlines 에 직접 추가할 수 있다.
/// </summary>
public static class HtmlInlineParser
{
    /// <summary>
    /// HTML 소스를 Inline 시퀀스로 변환한다.
    /// </summary>
    /// <param name="source">인라인 HTML 태그가 포함된 텍스트</param>
    /// <param name="posMap">
    /// null 이 아니면, 각 렌더 텍스트 문자의 소스 인덱스를 기록한다.
    /// posMap[렌더위치] = 소스위치. LineBreak 는 기록하지 않는다.
    /// </param>
    public static IEnumerable<Inline> Parse(string source, List<int>? posMap = null)
    {
        if (string.IsNullOrEmpty(source)) yield break;

        var stack = new Stack<TagStyle>();
        var buf = new StringBuilder();
        var bufPos = new List<int>();
        int i = 0;

        while (i < source.Length)
        {
            if (source[i] == '<')
            {
                if (buf.Length > 0)
                {
                    yield return MakeRun(buf.ToString(), stack);
                    posMap?.AddRange(bufPos);
                    buf.Clear(); bufPos.Clear();
                }

                int gt = source.IndexOf('>', i);
                if (gt < 0) { bufPos.Add(i); buf.Append(source[i++]); continue; }

                string raw = source[(i + 1)..gt].Trim();
                i = gt + 1;

                if (raw.StartsWith('/'))
                {
                    string closing = raw[1..].Split(' ')[0].ToLowerInvariant();
                    if (stack.Count > 0 && stack.Peek().Tag == closing) stack.Pop();
                }
                else
                {
                    string name = raw.Split(' ', '/', '>')[0].ToLowerInvariant();
                    if (name == "br")
                        yield return new LineBreak();
                    else
                    {
                        var style = TagToStyle(name, raw);
                        if (style is not null) stack.Push(style);
                    }
                }
            }
            else if (source[i] == '\r')
            {
                i++;
            }
            else if (source[i] == '\n')
            {
                if (buf.Length > 0)
                {
                    yield return MakeRun(buf.ToString(), stack);
                    posMap?.AddRange(bufPos);
                    buf.Clear(); bufPos.Clear();
                }
                yield return new LineBreak();
                i++;
            }
            else
            {
                bufPos.Add(i);
                buf.Append(source[i++]);
            }
        }

        if (buf.Length > 0)
        {
            yield return MakeRun(buf.ToString(), stack);
            posMap?.AddRange(bufPos);
        }
    }

    // ── Run 생성 ──────────────────────────────────────

    private static Run MakeRun(string text, Stack<TagStyle> styles)
    {
        var run = new Run(text);
        foreach (var s in styles)
        {
            if (s.Weight.HasValue) run.FontWeight = s.Weight.Value;
            if (s.Style.HasValue)  run.FontStyle  = s.Style.Value;
            if (s.Deco is not null)  run.TextDecorations = s.Deco;
            if (s.Fg is not null)    run.Foreground      = s.Fg;
            if (s.Size.HasValue)   run.FontSize  = s.Size.Value;
        }
        return run;
    }

    // ── 태그 → 스타일 매핑 ────────────────────────────

    private static TagStyle? TagToStyle(string tag, string raw) => tag switch
    {
        "b" or "strong" => new() { Tag = tag, Weight = FontWeight.Bold },
        "i" or "em"     => new() { Tag = tag, Style  = FontStyle.Italic },
        "u"             => new() { Tag = tag, Deco   = TextDecorations.Underline },
        "s"             => new() { Tag = tag, Deco   = TextDecorations.Strikethrough },
        "h1"            => new() { Tag = tag, Weight = FontWeight.Bold, Size = 20 },
        "h2"            => new() { Tag = tag, Weight = FontWeight.Bold, Size = 17 },
        "h3"            => new() { Tag = tag, Weight = FontWeight.Bold, Size = 15 },
        "a"             => new() { Tag = tag, Deco = TextDecorations.Underline,
                                   Fg = new SolidColorBrush(Color.FromRgb(0x33, 0x6B, 0xCC)) },
        "span"          => ParseSpanColor(raw),
        _ => null,
    };

    private static TagStyle? ParseSpanColor(string raw)
    {
        var m = Regex.Match(raw, @"color:\s*([^;""']+)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        try
        {
            var c = Color.Parse(m.Groups[1].Value.Trim());
            return new TagStyle { Tag = "span", Fg = new SolidColorBrush(c) };
        }
        catch { return null; }
    }

    // ── 내부 타입 ─────────────────────────────────────

    private sealed class TagStyle
    {
        public string Tag { get; init; } = "";
        public FontWeight? Weight { get; init; }
        public FontStyle? Style { get; init; }
        public TextDecorationCollection? Deco { get; init; }
        public IBrush? Fg { get; init; }
        public double? Size { get; init; }
    }
}
