using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SaemDesk.HtmlEditor;

/// <summary>
/// 블록 단위 편집 영역.
/// 싱글클릭: 서식이 보이는 SelectableTextBlock에서 텍스트 선택.
/// 더블클릭: TextBox로 전환하여 텍스트 편집.
/// </summary>
public class EditArea : UserControl
{
    private readonly StackPanel _blocksPanel;
    private HtmlDocument _document = new();
    private int _activeBlockIndex = -1;
    private TextBox? _activeTextBox;
    private SelectableTextBlock? _activeStb; // 선택 모드 활성 컨트롤

    // 셀 편집 컨텍스트
    private record CellContext(int TableBlockIndex, int Row, int Cell, int Para);
    private CellContext? _activeCellCtx;
    private StackPanel? _activeCellPanel;

    // 마지막 유효 선택 (포커스 손실 후에도 유지)
    private int _lastSelBlock = -1;
    private int _lastSelStart;
    private int _lastSelEnd;
    private CellContext? _lastSelCellCtx;

    /// <summary>활성 블록의 선택 범위가 변경될 때 발생.</summary>
    public event Action? SelectionChanged;

    /// <summary>문서 내용이 변경될 때 발생.</summary>
    public event Action? DocumentChanged;

    public HtmlDocument Document => _document;
    public int ActiveBlockIndex => _activeBlockIndex;

    /// <summary>마지막 유효 선택 (블록인덱스/시작/끝).</summary>
    public (int blockIndex, int start, int end) LastSelection => (_lastSelBlock, _lastSelStart, _lastSelEnd);

    /// <summary>마지막 선택 상태를 초기화한다.</summary>
    public void ClearLastSelection()
    {
        _lastSelBlock = -1;
        _lastSelStart = 0;
        _lastSelEnd = 0;
        _lastSelCellCtx = null;
    }

    public EditArea()
    {
        _blocksPanel = new StackPanel { Margin = new Thickness(4) };
        Content = new ScrollViewer
        {
            Content = _blocksPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
    }

    // ── Public API ───────────────────────────────────

    public void LoadDocument(HtmlDocument doc)
    {
        _document = doc;
        ResetEditState();
        RenderAll();
    }

    /// <summary>활성 컨트롤(TextBox 또는 SelectableTextBlock)의 선택을 반환한다.</summary>
    public (int start, int end) GetSelection()
    {
        if (_activeTextBox != null)
        {
            int s = _activeTextBox.SelectionStart;
            int e = _activeTextBox.SelectionEnd;
            return s <= e ? (s, e) : (e, s);
        }

        if (_activeStb != null)
        {
            int s = _activeStb.SelectionStart;
            int e = _activeStb.SelectionEnd;
            return s <= e ? (s, e) : (e, s);
        }

        return (0, 0);
    }

    /// <summary>현재 편집/선택 중인 문단을 반환한다.</summary>
    public ParagraphBlock? GetActiveParagraph()
    {
        if (_activeCellCtx != null)
            return GetCellParagraph(_activeCellCtx);
        if (_activeBlockIndex >= 0 && _activeBlockIndex < _document.Blocks.Count)
            return _document.Blocks[_activeBlockIndex] as ParagraphBlock;
        return null;
    }

    /// <summary>마지막 선택이 있었던 문단을 반환한다.</summary>
    public ParagraphBlock? GetLastSelectionParagraph()
    {
        if (_lastSelCellCtx != null)
            return GetCellParagraph(_lastSelCellCtx);
        if (_lastSelBlock >= 0 && _lastSelBlock < _document.Blocks.Count)
            return _document.Blocks[_lastSelBlock] as ParagraphBlock;
        return null;
    }

    /// <summary>TextBox 텍스트를 모델에 동기화한다.</summary>
    public void SyncTextToModel()
    {
        if (_activeTextBox == null) return;

        var para = GetActiveParagraph();
        if (para == null) return;

        string newText = _activeTextBox.Text ?? "";
        string oldText = FormatCommand.GetPlainText(para);

        if (newText != oldText)
        {
            para.Inlines.Clear();
            para.Inlines.Add(new TextRun(newText));
        }
    }

    /// <summary>활성 블록을 커밋하고 렌더링 뷰로 복귀.</summary>
    public void CommitAndRender(int? blockIndex = null)
    {
        int idx;
        if (blockIndex.HasValue)
            idx = blockIndex.Value;
        else if (_activeCellCtx != null)
            idx = _activeCellCtx.TableBlockIndex;
        else
            idx = _activeBlockIndex;

        if (idx < 0 || idx >= _document.Blocks.Count) return;

        ResetEditState();
        ReplaceWithRendered(idx);
        DocumentChanged?.Invoke();
    }

    /// <summary>전체 블록을 다시 렌더링한다.</summary>
    public void RenderAll()
    {
        _blocksPanel.Children.Clear();
        ResetEditState();

        for (int i = 0; i < _document.Blocks.Count; i++)
            _blocksPanel.Children.Add(CreateRenderedBlock(i));

        if (_document.Blocks.Count == 0)
        {
            var empty = new ParagraphBlock();
            empty.Inlines.Add(new TextRun(""));
            _document.Blocks.Add(empty);
            _blocksPanel.Children.Add(CreateRenderedBlock(0));
        }
    }

    // ── 싱글클릭 → 선택 모드 ────────────────────────

    private void OnBlockSingleClicked(int blockIndex, Control control)
    {
        // 다른 블록/셀 편집 중이면 커밋
        if (_activeBlockIndex != blockIndex || _activeCellCtx != null)
            CommitActiveBlock();

        _activeBlockIndex = blockIndex;
        _activeCellCtx = null;
        _activeStb = control as SelectableTextBlock;
    }

    private void OnCellSingleClicked(int tableBlockIndex, int rowIdx, int cellIdx, int paraIdx, Control control)
    {
        var newCtx = new CellContext(tableBlockIndex, rowIdx, cellIdx, paraIdx);
        if (_activeCellCtx != newCtx)
            CommitActiveBlock();

        _activeBlockIndex = tableBlockIndex;
        _activeCellCtx = newCtx;
        _activeStb = control as SelectableTextBlock;
    }

    // ── 더블클릭 → 편집 모드 (TextBox) ──────────────

    private void EnterEditMode(int blockIndex, PointerPressedEventArgs e)
    {
        if (blockIndex == _activeBlockIndex && _activeTextBox != null) return;

        Point clickPos = e.GetPosition(_blocksPanel.Children[blockIndex]);
        CommitActiveBlock();

        var block = _document.Blocks[blockIndex];
        if (block is not ParagraphBlock para) return;

        _activeBlockIndex = blockIndex;
        _activeStb = null;

        string plainText = FormatCommand.GetPlainText(para);
        double fontSize = para.Tag switch
        {
            "h1" => 24, "h2" => 20, "h3" => 17, _ => 14
        };

        var tb = CreateEditTextBox(plainText, fontSize, para.Tag);
        _activeTextBox = tb;

        if (blockIndex < _blocksPanel.Children.Count)
        {
            _blocksPanel.Children[blockIndex] = tb;
            tb.Focus();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                int caret = EstimateCaretIndex(plainText, clickPos.X, fontSize);
                tb.CaretIndex = caret;
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private void EnterCellEditMode(
        int tableBlockIndex, int rowIdx, int cellIdx, int paraIdx,
        StackPanel cellPanel, PointerPressedEventArgs e)
    {
        if (_activeCellCtx is { } ctx
            && ctx.TableBlockIndex == tableBlockIndex
            && ctx.Row == rowIdx && ctx.Cell == cellIdx && ctx.Para == paraIdx
            && _activeTextBox != null)
            return;

        Point clickPos = e.GetPosition(cellPanel.Children[paraIdx]);
        CommitActiveBlock();

        var para = GetCellParagraph(new CellContext(tableBlockIndex, rowIdx, cellIdx, paraIdx));
        if (para == null) return;

        _activeCellCtx = new CellContext(tableBlockIndex, rowIdx, cellIdx, paraIdx);
        _activeCellPanel = cellPanel;
        _activeBlockIndex = tableBlockIndex;
        _activeStb = null;

        string plainText = FormatCommand.GetPlainText(para);
        var tb = CreateEditTextBox(plainText, 14, null);
        _activeTextBox = tb;

        if (paraIdx < cellPanel.Children.Count)
        {
            cellPanel.Children[paraIdx] = tb;
            tb.Focus();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                int caret = EstimateCaretIndex(plainText, clickPos.X, 14);
                tb.CaretIndex = caret;
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    // ── TextBox 생성 ─────────────────────────────────

    private TextBox CreateEditTextBox(string text, double fontSize, string? tag)
    {
        var tb = new TextBox
        {
            Text = text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 2),
            MinHeight = 16,
            FontSize = fontSize,
            FontWeight = tag switch
            {
                "h1" or "h2" or "h3" => FontWeight.Bold,
                _ => FontWeight.Normal
            }
        };

        tb.LostFocus += OnTextBoxLostFocus;

        tb.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name is "SelectionStart" or "SelectionEnd")
            {
                var (s, e) = GetSelection();
                if (s != e)
                {
                    _lastSelBlock = _activeCellCtx?.TableBlockIndex ?? _activeBlockIndex;
                    _lastSelStart = s;
                    _lastSelEnd = e;
                    _lastSelCellCtx = _activeCellCtx;
                }
                SelectionChanged?.Invoke();
            }
        };

        return tb;
    }

    private static int EstimateCaretIndex(string text, double x, double fontSize)
    {
        double avgCharWidth = fontSize * 0.55;
        int index = (int)Math.Round(x / avgCharWidth);
        return Math.Clamp(index, 0, text.Length);
    }

    // ── Focus ────────────────────────────────────────

    private void OnTextBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_activeTextBox == null) return;

            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

            if (focused is Visual v && (IsWithinParentEditor(v) || IsInPopup(v)))
                return;

            CommitActiveBlock();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private bool IsWithinParentEditor(Visual target)
    {
        var editor = this.FindAncestorOfType<HtmlEditorControl>();
        if (editor == null) return false;
        return editor.IsVisualAncestorOf(target);
    }

    private static bool IsInPopup(Visual target)
    {
        return target.FindAncestorOfType<Avalonia.Controls.Primitives.Popup>() != null;
    }

    // ── Commit ───────────────────────────────────────

    private void CommitActiveBlock()
    {
        if (_activeTextBox == null)
        {
            // 선택 모드만 해제 (TextBox 없으면 커밋할 내용 없음)
            _activeStb = null;
            return;
        }

        var para = GetActiveParagraph();
        if (para != null)
        {
            string newText = _activeTextBox.Text ?? "";
            string oldText = FormatCommand.GetPlainText(para);
            if (newText != oldText)
            {
                para.Inlines.Clear();
                para.Inlines.Add(new TextRun(newText));
                DocumentChanged?.Invoke();
            }
        }

        int idx = _activeCellCtx?.TableBlockIndex ?? _activeBlockIndex;
        ResetEditState();

        if (idx >= 0) ReplaceWithRendered(idx);
    }

    private void ResetEditState()
    {
        _activeBlockIndex = -1;
        _activeTextBox = null;
        _activeStb = null;
        _activeCellCtx = null;
        _activeCellPanel = null;
    }

    // ── Helpers ──────────────────────────────────────

    private ParagraphBlock? GetCellParagraph(CellContext ctx)
    {
        if (ctx.TableBlockIndex < 0 || ctx.TableBlockIndex >= _document.Blocks.Count) return null;
        if (_document.Blocks[ctx.TableBlockIndex] is not TableBlock tbl) return null;
        if (ctx.Row >= tbl.Rows.Count) return null;
        if (ctx.Cell >= tbl.Rows[ctx.Row].Cells.Count) return null;
        var cell = tbl.Rows[ctx.Row].Cells[ctx.Cell];
        if (ctx.Para >= cell.Content.Count) return null;
        return cell.Content[ctx.Para] as ParagraphBlock;
    }

    /// <summary>SelectableTextBlock의 선택을 추적한다.</summary>
    private void TrackStbSelection(SelectableTextBlock stb, int blockIndex, CellContext? cellCtx)
    {
        // 드래그 완료 시 선택 저장 (PointerReleased가 가장 안정적)
        stb.PointerReleased += (_, _) =>
        {
            SaveStbSelection(stb, blockIndex, cellCtx);
        };

        // PropertyChanged도 보조적으로 사용
        stb.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name is "SelectionStart" or "SelectionEnd")
                SaveStbSelection(stb, blockIndex, cellCtx);
        };
    }

    private void SaveStbSelection(SelectableTextBlock stb, int blockIndex, CellContext? cellCtx)
    {
        int s = stb.SelectionStart;
        int end = stb.SelectionEnd;
        if (s > end) (s, end) = (end, s);
        Debug.WriteLine($"[SaveStbSel] block={blockIndex} s={s} end={end}");
        if (s != end)
        {
            _lastSelBlock = blockIndex;
            _lastSelStart = s;
            _lastSelEnd = end;
            _lastSelCellCtx = cellCtx;
            Debug.WriteLine($"[SaveStbSel] SAVED lastSel=({_lastSelBlock},{_lastSelStart},{_lastSelEnd})");
        }
        SelectionChanged?.Invoke();
    }

    private Control CreateRenderedBlock(int index)
    {
        var block = _document.Blocks[index];

        if (block is TableBlock tbl)
            return CreateEditableTable(index, tbl);

        var control = HtmlRenderer.RenderBlock(block);
        int idx = index;
        control.Tag = idx;

        // 싱글클릭 = 선택 모드, 더블클릭 = 편집 모드
        control.AddHandler(
            Avalonia.Input.InputElement.PointerPressedEvent,
            (object? _, PointerPressedEventArgs e) =>
            {
                OnBlockSingleClicked(idx, control);

                if (e.ClickCount >= 2 && block is ParagraphBlock)
                {
                    EnterEditMode(idx, e);
                    e.Handled = true; // SelectableTextBlock 처리 방지
                }
            },
            Avalonia.Interactivity.RoutingStrategies.Tunnel,
            handledEventsToo: true);

        // SelectableTextBlock 선택 추적
        if (control is SelectableTextBlock stb)
            TrackStbSelection(stb, idx, null);

        control.Cursor = new Cursor(StandardCursorType.Ibeam);
        return control;
    }

    private Control CreateEditableTable(int tableBlockIndex, TableBlock tbl)
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
            for (int ci = 0; ci < tbl.Rows[r].Cells.Count; ci++)
            {
                var cell = tbl.Rows[r].Cells[ci];
                var panel = new StackPanel { Margin = new Thickness(4) };

                for (int pi = 0; pi < cell.Content.Count; pi++)
                {
                    var rendered = HtmlRenderer.RenderBlock(cell.Content[pi]);
                    int rowIdx = r, cellIdx = ci, paraIdx = pi;
                    var panelRef = panel;
                    var cellCtx = new CellContext(tableBlockIndex, rowIdx, cellIdx, paraIdx);

                    rendered.AddHandler(
                        Avalonia.Input.InputElement.PointerPressedEvent,
                        (object? _, PointerPressedEventArgs e) =>
                        {
                            OnCellSingleClicked(tableBlockIndex, rowIdx, cellIdx, paraIdx, rendered);

                            if (e.ClickCount >= 2)
                            {
                                EnterCellEditMode(tableBlockIndex, rowIdx, cellIdx, paraIdx, panelRef, e);
                                e.Handled = true;
                            }
                        },
                        Avalonia.Interactivity.RoutingStrategies.Tunnel,
                        handledEventsToo: true);

                    if (rendered is SelectableTextBlock stb)
                        TrackStbSelection(stb, tableBlockIndex, cellCtx);

                    rendered.Cursor = new Cursor(StandardCursorType.Ibeam);
                    panel.Children.Add(rendered);
                }

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

    private void ReplaceWithRendered(int index)
    {
        if (index < _blocksPanel.Children.Count && index < _document.Blocks.Count)
            _blocksPanel.Children[index] = CreateRenderedBlock(index);
    }

    /// <summary>활성 블록 뒤에 새 블록을 삽입한다.</summary>
    public void InsertBlockAfter(int blockIndex)
    {
        var para = new ParagraphBlock();
        para.Inlines.Add(new TextRun(""));
        int insertAt = Math.Min(blockIndex + 1, _document.Blocks.Count);
        _document.Blocks.Insert(insertAt, para);
        RenderAll();
        DocumentChanged?.Invoke();
    }
}
