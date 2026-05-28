namespace SaemDesk.HtmlEditor;

/// <summary>
/// 문서 모델에 서식을 적용하는 명령 모음.
/// </summary>
public static class FormatCommand
{
    /// <summary>단일 블록 내 선택 범위에 서식을 적용한다.</summary>
    public static void ApplyInline(ParagraphBlock para, int startOff, int endOff,
        Action<FormattedSpan> configure)
    {
        if (startOff >= endOff) return;

        // 1) 인라인 → 플랫 리스트 (TextRun, LineBreak)
        var flat = Flatten(para.Inlines);

        // 2) 범위 분할 → 3개 구간: before, selected, after
        var before = new List<InlineNode>();
        var selected = new List<InlineNode>();
        var after = new List<InlineNode>();
        int cursor = 0;

        foreach (var (node, style) in flat)
        {
            if (node is LineBreakNode lb)
            {
                var target = cursor < startOff ? before
                    : cursor < endOff ? selected : after;
                target.Add(lb);
                cursor++;
                continue;
            }

            if (node is TextRun tr)
            {
                string text = tr.Text;
                int nodeStart = cursor;
                int nodeEnd = cursor + text.Length;

                // 완전히 before
                if (nodeEnd <= startOff)
                {
                    before.Add(CloneRun(text, style));
                    cursor = nodeEnd;
                    continue;
                }

                // 완전히 after
                if (nodeStart >= endOff)
                {
                    after.Add(CloneRun(text, style));
                    cursor = nodeEnd;
                    continue;
                }

                // 분할 필요
                if (nodeStart < startOff)
                {
                    before.Add(CloneRun(text[..(startOff - nodeStart)], style));
                }

                int selStart = Math.Max(0, startOff - nodeStart);
                int selEnd = Math.Min(text.Length, endOff - nodeStart);
                selected.Add(CloneRun(text[selStart..selEnd], style));

                if (nodeEnd > endOff)
                {
                    after.Add(CloneRun(text[(endOff - nodeStart)..], style));
                }

                cursor = nodeEnd;
            }
        }

        // 3) 선택 영역을 FormattedSpan으로 감싸기
        var span = new FormattedSpan();
        configure(span);
        span.Children.AddRange(selected);

        // 4) 재조립
        para.Inlines.Clear();
        para.Inlines.AddRange(before);
        para.Inlines.Add(span);
        para.Inlines.AddRange(after);
    }

    /// <summary>블록의 정렬을 설정한다.</summary>
    public static void SetAlignment(ParagraphBlock para, Avalonia.Media.TextAlignment alignment)
    {
        para.Alignment = alignment;
    }

    /// <summary>현재 캐럿 위치에 이미지 블록을 삽입한다.</summary>
    public static void InsertImage(HtmlDocument doc, int blockIndex, string source,
        double? width = null, double? height = null)
    {
        var img = new ImageBlock { Source = source, Width = width, Height = height };
        int insertAt = Math.Min(blockIndex + 1, doc.Blocks.Count);
        doc.Blocks.Insert(insertAt, img);
    }

    /// <summary>현재 캐럿 위치에 빈 테이블을 삽입한다.</summary>
    public static void InsertTable(HtmlDocument doc, int blockIndex, int rows, int cols)
    {
        var table = new TableBlock();
        for (int r = 0; r < rows; r++)
        {
            var row = new TableRow();
            for (int c = 0; c < cols; c++)
            {
                var cell = new TableCell();
                var para = new ParagraphBlock();
                para.Inlines.Add(new TextRun(" "));
                cell.Content.Add(para);
                row.Cells.Add(cell);
            }
            table.Rows.Add(row);
        }

        int insertAt = Math.Min(blockIndex + 1, doc.Blocks.Count);
        doc.Blocks.Insert(insertAt, table);
    }

    // ── Helpers ──────────────────────────────────────

    /// <summary>인라인 트리를 플랫 리스트로 펼친다. (노드, 상위 스타일 정보)</summary>
    private static List<(InlineNode node, FormattedSpan? style)> Flatten(
        List<InlineNode> inlines, FormattedSpan? parentStyle = null)
    {
        var result = new List<(InlineNode, FormattedSpan?)>();
        foreach (var node in inlines)
        {
            if (node is FormattedSpan fs)
            {
                var merged = MergeStyle(parentStyle, fs);
                result.AddRange(Flatten(fs.Children, merged));
            }
            else
            {
                result.Add((node, parentStyle));
            }
        }
        return result;
    }

    private static InlineNode CloneRun(string text, FormattedSpan? style)
    {
        var run = new TextRun(text);
        if (style == null) return run;

        var span = new FormattedSpan
        {
            Bold = style.Bold,
            Italic = style.Italic,
            Underline = style.Underline,
            Strikethrough = style.Strikethrough,
            Color = style.Color,
            FontSize = style.FontSize,
            Href = style.Href
        };
        span.Children.Add(run);
        return span;
    }

    private static FormattedSpan MergeStyle(FormattedSpan? parent, FormattedSpan child) => new()
    {
        Bold = child.Bold ?? parent?.Bold,
        Italic = child.Italic ?? parent?.Italic,
        Underline = child.Underline ?? parent?.Underline,
        Strikethrough = child.Strikethrough ?? parent?.Strikethrough,
        Color = child.Color ?? parent?.Color,
        FontSize = child.FontSize ?? parent?.FontSize,
        Href = child.Href ?? parent?.Href
    };

    /// <summary>블록의 플레인 텍스트 길이를 계산한다.</summary>
    public static int PlainTextLength(ParagraphBlock para)
    {
        int len = 0;
        CountInlines(para.Inlines, ref len);
        return len;
    }

    private static void CountInlines(List<InlineNode> inlines, ref int len)
    {
        foreach (var n in inlines)
        {
            if (n is TextRun tr) len += tr.Text.Length;
            else if (n is LineBreakNode) len += 1;
            else if (n is FormattedSpan fs) CountInlines(fs.Children, ref len);
        }
    }

    /// <summary>블록의 플레인 텍스트를 추출한다.</summary>
    public static string GetPlainText(ParagraphBlock para)
    {
        var sb = new System.Text.StringBuilder();
        AppendPlainText(para.Inlines, sb);
        return sb.ToString();
    }

    private static void AppendPlainText(List<InlineNode> inlines, System.Text.StringBuilder sb)
    {
        foreach (var n in inlines)
        {
            if (n is TextRun tr) sb.Append(tr.Text);
            else if (n is LineBreakNode) sb.Append('\n');
            else if (n is FormattedSpan fs) AppendPlainText(fs.Children, sb);
        }
    }
}
