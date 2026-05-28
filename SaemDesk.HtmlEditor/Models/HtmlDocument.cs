using Avalonia.Media;

namespace SaemDesk.HtmlEditor;

// ── Block Nodes ──────────────────────────────────────

public abstract class BlockNode { }

public sealed class ParagraphBlock : BlockNode
{
    public string Tag { get; set; } = "p";
    public TextAlignment Alignment { get; set; }
    public List<InlineNode> Inlines { get; } = [];
}

public sealed class ImageBlock : BlockNode
{
    public string Source { get; set; } = "";
    public string? Alt { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
}

public sealed class TableBlock : BlockNode
{
    public List<TableRow> Rows { get; } = [];
}

public sealed class TableRow
{
    public List<TableCell> Cells { get; } = [];
}

public sealed class TableCell
{
    public int ColSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public List<BlockNode> Content { get; } = [];
}

// ── Inline Nodes ─────────────────────────────────────

public abstract class InlineNode { }

public sealed class TextRun : InlineNode
{
    public string Text { get; set; } = "";
    public TextRun() { }
    public TextRun(string text) => Text = text;
}

public sealed class LineBreakNode : InlineNode { }

public sealed class FormattedSpan : InlineNode
{
    public List<InlineNode> Children { get; } = [];
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public bool? Strikethrough { get; set; }
    public string? Color { get; set; }
    public double? FontSize { get; set; }
    public string? Href { get; set; }
}

// ── Document Root ────────────────────────────────────

public sealed class HtmlDocument
{
    public List<BlockNode> Blocks { get; } = [];
}
