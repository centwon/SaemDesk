using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace SaemDesk.HtmlEditor;

public partial class EditorToolbar : UserControl
{
    private EditArea? _editArea;

    // Tunnel 단계에서 저장하는 선택 상태 (포커스 변경 전)
    private int _savedBlockIndex = -1;
    private int _savedSelStart;
    private int _savedSelEnd;

    public EditorToolbar()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnToolbarPointerPressed, RoutingStrategies.Tunnel);
        AttachHandlers();
        BuildFlyouts();
    }

    public void Bind(EditArea editArea) => _editArea = editArea;

    // ── 선택 상태 저장 ───────────────────────────────

    private void OnToolbarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_editArea == null) return;
        int idx = _editArea.ActiveBlockIndex;
        var (s, end) = _editArea.GetSelection();
        var last = _editArea.LastSelection;
        Debug.WriteLine($"[Tunnel] idx={idx} sel=({s},{end}) last=({last.blockIndex},{last.start},{last.end})");

        // 유효한 선택이 있을 때만 저장 (빈 선택으로 덮어쓰지 않음)
        if (idx >= 0 && s != end)
        {
            _savedBlockIndex = idx;
            _savedSelStart = s;
            _savedSelEnd = end;
            Debug.WriteLine($"[Tunnel] path=LIVE saved=({_savedBlockIndex},{_savedSelStart},{_savedSelEnd})");
        }
        // 드래그 선택 → LastSelection 폴백
        else
        {
            if (last.blockIndex >= 0 && last.start != last.end)
            {
                _savedBlockIndex = last.blockIndex;
                _savedSelStart = last.start;
                _savedSelEnd = last.end;
                Debug.WriteLine($"[Tunnel] path=LAST saved=({_savedBlockIndex},{_savedSelStart},{_savedSelEnd})");
            }
            // 블록은 활성이지만 선택 없음 → 블록 인덱스만 갱신
            else if (idx >= 0 && _savedBlockIndex < 0)
            {
                _savedBlockIndex = idx;
                Debug.WriteLine($"[Tunnel] path=IDX_ONLY savedBlock={_savedBlockIndex}");
            }
            else
            {
                Debug.WriteLine("[Tunnel] path=NONE (no selection captured)");
            }
        }
    }

    // ── 이벤트 연결 ──────────────────────────────────

    private void AttachHandlers()
    {
        BoldBtn.Click += (_, _) => ApplyInlineFormat(s => s.Bold = true);
        ItalicBtn.Click += (_, _) => ApplyInlineFormat(s => s.Italic = true);
        UnderlineBtn.Click += (_, _) => ApplyInlineFormat(s => s.Underline = true);
        StrikeBtn.Click += (_, _) => ApplyInlineFormat(s => s.Strikethrough = true);
        AlignLeftBtn.Click += (_, _) => ApplyAlignment(TextAlignment.Left);
        AlignCenterBtn.Click += (_, _) => ApplyAlignment(TextAlignment.Center);
        AlignRightBtn.Click += (_, _) => ApplyAlignment(TextAlignment.Right);
        ImageBtn.Click += OnInsertImage;
    }

    // ── Flyout 생성 ──────────────────────────────────

    private void BuildFlyouts()
    {
        // 글꼴 크기 Flyout
        var sizeFlyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        foreach (int size in new[] { 10, 12, 14, 16, 18, 20, 24, 28, 36, 48 })
        {
            var item = new MenuItem { Header = $"{size}px", Tag = size };
            int s = size;
            item.Click += (_, _) => ApplyInlineFormat(f => f.FontSize = s);
            sizeFlyout.Items.Add(item);
        }
        FontSizeBtn.Flyout = sizeFlyout;

        // 색상 Flyout
        var colorFlyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        (string name, string hex)[] colors =
        [
            ("검정", "#000000"), ("빨강", "#FF0000"), ("파랑", "#0000FF"),
            ("초록", "#008000"), ("주황", "#FF8C00"), ("보라", "#800080"),
            ("회색", "#808080"), ("하늘", "#1E90FF"), ("분홍", "#FF69B4")
        ];
        foreach (var (name, hex) in colors)
        {
            var item = new MenuItem { Tag = hex };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            panel.Children.Add(new Border
            {
                Width = 14, Height = 14,
                Background = new SolidColorBrush(Color.Parse(hex)),
                BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5),
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            item.Header = panel;
            string h = hex;
            item.Click += (_, _) => ApplyInlineFormat(f => f.Color = h);
            colorFlyout.Items.Add(item);
        }
        ColorBtn.Flyout = colorFlyout;

        // 표 Flyout
        var tableFlyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        var tableRows = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 3, Width = 120, FormatString = "0" };
        var tableCols = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 3, Width = 120, FormatString = "0" };
        var tableInsertBtn = new Button { Content = "삽입", Margin = new Thickness(4, 0, 0, 0) };
        tableInsertBtn.Click += (_, _) =>
        {
            if (_editArea == null) return;
            int rows = (int)(tableRows.Value ?? 3);
            int cols = (int)(tableCols.Value ?? 3);
            int idx = Math.Max(0, _savedBlockIndex);
            FormatCommand.InsertTable(_editArea.Document, idx, rows, cols);
            _editArea.RenderAll();
            tableFlyout.Hide();
        };
        var tablePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        tablePanel.Children.Add(new TextBlock { Text = "행", VerticalAlignment = VerticalAlignment.Center });
        tablePanel.Children.Add(tableRows);
        tablePanel.Children.Add(new TextBlock { Text = "x", VerticalAlignment = VerticalAlignment.Center });
        tablePanel.Children.Add(tableCols);
        tablePanel.Children.Add(tableInsertBtn);
        tableFlyout.Content = tablePanel;
        TableBtn.Flyout = tableFlyout;
    }

    // ── 인라인 서식 ──────────────────────────────────

    private void ApplyInlineFormat(Action<FormattedSpan> configure)
    {
        if (_editArea == null) return;

        // 재렌더링 대상 블록 인덱스 결정
        int renderIdx = _editArea.ActiveBlockIndex;
        if (renderIdx < 0) renderIdx = _editArea.LastSelection.blockIndex;
        if (renderIdx < 0) renderIdx = _savedBlockIndex;
        Debug.WriteLine($"[Apply] renderIdx={renderIdx}");

        // 1) 현재 활성 선택에서 문단 가져오기
        ParagraphBlock? para = _editArea.GetActiveParagraph();
        var (s, end) = _editArea.GetSelection();
        Debug.WriteLine($"[Apply] step1: para={para != null} sel=({s},{end})");

        // 2) 없으면 마지막 유효 선택 사용
        if (para == null || s == end)
        {
            para = _editArea.GetLastSelectionParagraph();
            var last = _editArea.LastSelection;
            s = last.start;
            end = last.end;
            Debug.WriteLine($"[Apply] step2: para={para != null} sel=({s},{end})");
        }

        // 3) 그래도 없으면 Tunnel 저장값 사용
        if (para == null || s == end)
        {
            if (_savedBlockIndex >= 0 && _savedBlockIndex < _editArea.Document.Blocks.Count)
                para = _editArea.Document.Blocks[_savedBlockIndex] as ParagraphBlock;
            s = _savedSelStart;
            end = _savedSelEnd;
            Debug.WriteLine($"[Apply] step3: para={para != null} sel=({s},{end})");
        }

        if (para == null || s == end || renderIdx < 0)
        {
            Debug.WriteLine($"[Apply] BAIL: para={para != null} s==end={s == end} renderIdx={renderIdx}");
            return;
        }

        Debug.WriteLine($"[Apply] EXEC: renderIdx={renderIdx} sel=({s},{end})");
        Debug.WriteLine($"[Apply] BEFORE: {para.Inlines.Count} inlines");
        _editArea.SyncTextToModel();
        FormatCommand.ApplyInline(para, s, end, configure);
        Debug.WriteLine($"[Apply] AFTER: {para.Inlines.Count} inlines");
        for (int di = 0; di < para.Inlines.Count; di++)
        {
            var n = para.Inlines[di];
            if (n is FormattedSpan fs)
                Debug.WriteLine($"  [{di}] FormattedSpan Bold={fs.Bold} Italic={fs.Italic} children={fs.Children.Count}");
            else if (n is TextRun tr)
                Debug.WriteLine($"  [{di}] TextRun \"{tr.Text}\"");
        }
        _editArea.CommitAndRender(renderIdx);

        _savedBlockIndex = -1;
        _savedSelStart = 0;
        _savedSelEnd = 0;
        _editArea.ClearLastSelection();
    }

    // ── 정렬 ─────────────────────────────────────────

    private void ApplyAlignment(TextAlignment alignment)
    {
        if (_editArea == null) return;

        int renderIdx = _editArea.ActiveBlockIndex;
        if (renderIdx < 0) renderIdx = _editArea.LastSelection.blockIndex;
        if (renderIdx < 0) renderIdx = _savedBlockIndex;

        ParagraphBlock? para = _editArea.GetActiveParagraph()
            ?? _editArea.GetLastSelectionParagraph();

        if (para == null && _savedBlockIndex >= 0 && _savedBlockIndex < _editArea.Document.Blocks.Count)
            para = _editArea.Document.Blocks[_savedBlockIndex] as ParagraphBlock;

        if (para == null || renderIdx < 0) return;

        FormatCommand.SetAlignment(para, alignment);
        _editArea.CommitAndRender(renderIdx);
    }

    // ── 이미지 삽입 ──────────────────────────────────

    private async void OnInsertImage(object? sender, RoutedEventArgs e)
    {
        if (_editArea == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "이미지 선택",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("이미지")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"]
                    }
                ]
            });

        if (files.Count == 0) return;

        await using var stream = await files[0].OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        byte[] bytes = ms.ToArray();
        string base64 = Convert.ToBase64String(bytes);
        string ext = files[0].Name.Split('.').LastOrDefault()?.ToLowerInvariant() ?? "png";
        string mime = ext switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            "webp" => "image/webp",
            _ => "image/png"
        };

        int idx = Math.Max(0, _savedBlockIndex);
        FormatCommand.InsertImage(_editArea.Document, idx,
            $"data:{mime};base64,{base64}", width: 400);
        _editArea.RenderAll();
    }
}
