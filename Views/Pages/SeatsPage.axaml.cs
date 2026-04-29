using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views.Pages;

public partial class SeatsPage : UserControl
{
    private SeatsPageVM? _vm;

    /// <summary>드래그 페이로드 — RosterEntry 또는 (Row,Col) 좌표.</summary>
    private static readonly DataFormat<string> DragSourceFormat =
        DataFormat.CreateInProcessFormat<string>("saemdesk.seatdrag");

    public SeatsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded             += (_, _) => RebuildSeats();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as SeatsPageVM;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            RebuildSeats();
            HookRosterDragSource();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            || e.PropertyName == nameof(SeatsPageVM.IsLoading)
            || e.PropertyName == nameof(SeatsPageVM.Jul)
            || e.PropertyName == nameof(SeatsPageVM.IsLocked))
        {
            RebuildSeats();
        }
    }

    // ────────────────────────────────────────────────────
    //  좌석 격자 빌드
    // ────────────────────────────────────────────────────

    private void RebuildSeats()
    {
        var host = this.FindControl<Grid>("SeatsHost");
        if (host is null || _vm?.Grid is null) return;

        host.Children.Clear();
        host.RowDefinitions.Clear();
        host.ColumnDefinitions.Clear();

        int rows = _vm.Grid.GetLength(0);
        int cols = _vm.Grid.GetLength(1);
        for (int r = 0; r < rows; r++)
            host.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        for (int c = 0; c < cols; c++)
            host.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                host.Children.Add(CreateSeatControl(r, c));
    }

    private Control CreateSeatControl(int row, int col)
    {
        if (_vm is null || _vm.Grid is null) return new Border();

        var cell = _vm.Grid[row, col];
        bool empty = string.IsNullOrEmpty(cell.StudentID);

        string display;
        if (empty) display = "+";
        else if (_vm.StudentMeta.TryGetValue(cell.StudentID, out var meta))
            display = $"{meta.Number}\n{meta.Name}";
        else
            display = cell.StudentID;

        var border = new Border
        {
            Margin       = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush  = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)),
            Background   = empty
                           ? Brushes.Transparent
                           : new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD)),
            Cursor = empty ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.SizeAll),
            Child = new TextBlock
            {
                Text = display,
                TextAlignment = TextAlignment.Center,
                TextWrapping  = TextWrapping.Wrap,
                FontSize      = empty ? 18 : 12,
                FontWeight    = empty ? FontWeight.Normal : FontWeight.SemiBold,
                Foreground    = empty
                                ? new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
                                : Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
            Tag = (row, col),
        };

        // 클릭(빈 셀=명렬 선택 학생 배정 / 채워진 셀=비우기), 드래그 소스, 드롭 타겟 모두 등록
        border.PointerPressed += OnSeatPointerPressed;
        DragDrop.SetAllowDrop(border, true);
        border.AddHandler(DragDrop.DragOverEvent, OnSeatDragOver);
        border.AddHandler(DragDrop.DropEvent,     OnSeatDrop);

        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        return border;
    }

    // ────────────────────────────────────────────────────
    //  좌석 PointerPressed → 클릭(빈→배정 / 채움→비우기) 또는 드래그 시작
    // ────────────────────────────────────────────────────

    private async void OnSeatPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null || _vm.IsLocked) return;
        if (sender is not Border b || b.Tag is not (int row, int col)) return;
        if (!e.GetCurrentPoint(b).Properties.IsLeftButtonPressed) return;
        e.Handled = true;

        var cell = _vm.Grid?[row, col];
        if (cell is null) return;
        bool empty = string.IsNullOrEmpty(cell.StudentID);

        // 비어 있는 좌석 → 클릭 처리(명렬 선택 학생 배정)
        if (empty)
        {
            HandleEmptySeatClick(row, col);
            return;
        }

        // 채워진 좌석 → 드래그(payload="seat:r:c"), 단순 클릭이면 None 반환 → 비우기
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(DragSourceFormat, $"seat:{row}:{col}"));

        DragDropEffects res = DragDropEffects.None;
        try
        {
            res = await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SeatsPage] seat drag 오류: {ex.Message}");
        }

        if (res == DragDropEffects.None)
        {
            // 단순 클릭 → 비우기
            _vm.ClearSeat(row, col);
            RebuildSeats();
        }
    }

    private void HandleEmptySeatClick(int row, int col)
    {
        var roster = this.FindControl<ListBox>("RosterListBox");
        if (roster?.SelectedItem is RosterEntry entry)
        {
            _vm!.AssignStudentToSeat(entry.StudentID, row, col);
            RebuildSeats();
        }
        else
        {
            _vm!.StatusText = "좌측 명렬에서 학생을 먼저 선택하거나, 학생을 좌석으로 드래그하세요.";
        }
    }

    // ────────────────────────────────────────────────────
    //  좌석 DragOver / Drop
    // ────────────────────────────────────────────────────

    private void OnSeatDragOver(object? sender, DragEventArgs e)
    {
        if (_vm is null || _vm.IsLocked || !e.DataTransfer.Contains(DragSourceFormat))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Move;
    }

    private void OnSeatDrop(object? sender, DragEventArgs e)
    {
        if (_vm is null || _vm.IsLocked) return;
        if (sender is not Border b || b.Tag is not (int dstRow, int dstCol)) return;
        if (!e.DataTransfer.Contains(DragSourceFormat)) return;

        try
        {
            var payload = e.DataTransfer.TryGetValue(DragSourceFormat);
            if (string.IsNullOrEmpty(payload)) return;

            if (payload.StartsWith("roster:", StringComparison.Ordinal))
            {
                // 명렬 → 좌석 배정
                string sid = payload.Substring("roster:".Length);
                _vm.AssignStudentToSeat(sid, dstRow, dstCol);
            }
            else if (payload.StartsWith("seat:", StringComparison.Ordinal))
            {
                // 좌석 → 좌석 (swap)
                var parts = payload.Split(':');
                if (parts.Length == 3 &&
                    int.TryParse(parts[1], out int srcRow) &&
                    int.TryParse(parts[2], out int srcCol))
                {
                    _vm.SwapSeats(srcRow, srcCol, dstRow, dstCol);
                }
            }
            e.DragEffects = DragDropEffects.Move;
            RebuildSeats();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SeatsPage] OnSeatDrop 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  좌측 명렬 ListBox — 항목을 드래그 소스로 등록
    // ────────────────────────────────────────────────────

    private void HookRosterDragSource()
    {
        var lb = this.FindControl<ListBox>("RosterListBox");
        if (lb is null) return;
        lb.PointerPressed -= OnRosterPointerPressed;
        lb.PointerPressed += OnRosterPointerPressed;
    }

    private async void OnRosterPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null || _vm.IsLocked) return;
        if (sender is not ListBox lb) return;
        if (!e.GetCurrentPoint(lb).Properties.IsLeftButtonPressed) return;

        // 누른 위치 항목 찾기
        var src = e.Source as Visual;
        var lbi = src?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        if (lbi?.DataContext is not RosterEntry entry) return;

        // 단순 클릭이어도 SelectedItem 은 ListBox 가 처리하므로, 드래그 시도만 수행
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(DragSourceFormat, $"roster:{entry.StudentID}"));
        try
        {
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SeatsPage] roster drag 오류: {ex.Message}");
        }
    }
}
