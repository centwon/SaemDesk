using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SaemDesk.Scheduler;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 달력의 날짜 셀 — NewSchool DayCell 동등 포팅(MVP).
///
/// - <see cref="Dayinfo"/> StyledProperty 변경 시 UI 재구성
/// - <see cref="CellChanged"/> 이벤트(편집/삭제 후 부모 새로고침 트리거)
/// - 호버/오늘 강조, 평일·주말·공휴일·휴업일 색상 구분
/// - 일정/할일을 컬러 바 + 점 + 텍스트로 표시
/// </summary>
public partial class DayCell : UserControl
{
    public static readonly StyledProperty<DayInfo?> DayinfoProperty =
        AvaloniaProperty.Register<DayCell, DayInfo?>(nameof(Dayinfo));

    public DayInfo? Dayinfo
    {
        get => GetValue(DayinfoProperty);
        set => SetValue(DayinfoProperty, value);
    }

    /// <summary>편집/삭제 후 부모(Kcalendar)에 새로고침을 알리는 이벤트.</summary>
    public event EventHandler? CellChanged;

    /// <summary>빈 영역 클릭 — 부모는 새 항목 다이얼로그를 연다.</summary>
    public event EventHandler<DayInfo>? CellClicked;

    /// <summary>셀 안의 일정/할일 항목 클릭 — 부모는 그 항목 편집 다이얼로그를 연다.</summary>
    public event EventHandler<KEvent>? ItemClicked;

    public DayCell()
    {
        InitializeComponent();
        DayinfoProperty.Changed.AddClassHandler<DayCell>((s, _) => s.UpdateDisplay());

        // 드롭 타겟 등록 — 다른 셀에서 드래그한 KEvent 를 이 날짜로 이동
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnCellDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnCellDragLeave);
        AddHandler(DragDrop.DropEvent,     OnCellDrop);
    }

    /// <summary>다른 셀에서 항목이 드롭되어 날짜가 변경되었을 때 부모(달력)에 알림.</summary>
    public event EventHandler<(KEvent Moved, DateTime NewDate)>? ItemDropped;

    // ────────────────────────────────────────────────────
    //  표시 갱신
    // ────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        var info = Dayinfo;
        var lbDate         = this.FindControl<TextBlock>("LbDate")!;
        var tbDateName     = this.FindControl<TextBlock>("TbDateName")!;
        var taskBadge      = this.FindControl<Border>("TaskCountBadge")!;
        var taskBadgeText  = this.FindControl<TextBlock>("TaskCountText")!;
        var todayHighlight = this.FindControl<Border>("TodayHighlight")!;
        var itemsPanel     = this.FindControl<StackPanel>("ItemsPanel")!;

        itemsPanel.Children.Clear();

        if (info is null)
        {
            lbDate.Text         = string.Empty;
            tbDateName.Text     = string.Empty;
            taskBadge.IsVisible = false;
            todayHighlight.IsVisible = false;
            return;
        }

        // 날짜 + 색상(주말/공휴일/휴업/평일/타월)
        lbDate.Text       = info.Date.Day.ToString(CultureInfo.InvariantCulture);
        lbDate.Foreground = ResolveDateBrush(info);

        // 학사일정 이름
        tbDateName.Text       = info.DateName;
        tbDateName.Foreground = info.IsHoliday ? new SolidColorBrush(Color.FromRgb(0xD9, 0x37, 0x2A))
                              : info.IsVacation ? new SolidColorBrush(Color.FromRgb(0xC9, 0x6E, 0x10))
                              : Brushes.Gray;

        // 오늘 강조
        todayHighlight.IsVisible = info.IsToday;

        // 흐림(타월) 처리
        Opacity = info.IsInCurrentMonth ? 1.0 : 0.45;

        // 일정 + 할일 표시
        RenderItems(itemsPanel, info);

        // 작업 배지(미완료 task 개수)
        int undone = 0;
        foreach (var t in info.Tasks)
            if (!t.IsDone) undone++;
        if (undone > 0)
        {
            taskBadgeText.Text = undone.ToString(CultureInfo.InvariantCulture);
            taskBadge.IsVisible = true;
        }
        else
        {
            taskBadge.IsVisible = false;
        }
    }

    private static IBrush ResolveDateBrush(DayInfo info)
    {
        if (info.IsHoliday)         return new SolidColorBrush(Color.FromRgb(0xD9, 0x37, 0x2A));
        if (info.Date.DayOfWeek == DayOfWeek.Sunday)   return new SolidColorBrush(Color.FromRgb(0xD9, 0x37, 0x2A));
        if (info.Date.DayOfWeek == DayOfWeek.Saturday) return new SolidColorBrush(Color.FromRgb(0x2A, 0x4F, 0xD9));
        return Brushes.Black;
    }

    /// <summary>셀 본문에 최대 표시 항목 수(이벤트+할일 합).</summary>
    private const int MaxVisibleItems = 4;

    private void RenderItems(StackPanel panel, DayInfo info)
    {
        // 우선순위: 이벤트 → 할일. 합이 한도 초과 시 마지막에 "+N개 더" 표시
        var built = new System.Collections.Generic.List<Control>();

        foreach (var ev in info.Events)
            built.Add(BuildEventBar(ev));
        foreach (var task in info.Tasks)
            built.Add(BuildTaskRow(task));

        if (built.Count <= MaxVisibleItems)
        {
            foreach (var c in built) panel.Children.Add(c);
            return;
        }

        // 한도까지만 표시 후 "+N개 더" Border 추가
        for (int i = 0; i < MaxVisibleItems - 1; i++)
            panel.Children.Add(built[i]);

        int hidden = built.Count - (MaxVisibleItems - 1);
        panel.Children.Add(BuildMoreLink(info, hidden));
    }

    /// <summary>"+N개 더" 라벨 — 클릭 시 Flyout 으로 그 날짜의 모든 항목 보여줌.</summary>
    private Border BuildMoreLink(DayInfo info, int hiddenCount)
    {
        var text = new TextBlock
        {
            Text       = $"+ {hiddenCount}개 더…",
            FontSize   = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x66, 0xBF)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var border = new Border
        {
            Padding      = new Thickness(4, 1),
            Background   = Brushes.Transparent,
            CornerRadius = new CornerRadius(3),
            Cursor       = new Cursor(StandardCursorType.Hand),
            Child        = text,
            Tag          = info,
        };
        border.PointerPressed += OnMoreLinkPressed;
        return border;
    }

    private void OnMoreLinkPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border b || b.Tag is not DayInfo info) return;
        e.Handled = true;

        // 모든 항목을 담은 패널을 만들어 Flyout 으로 띄움
        var inner = new StackPanel { Spacing = 2 };
        foreach (var ev in info.Events)
            inner.Children.Add(BuildEventBar(ev));
        foreach (var task in info.Tasks)
            inner.Children.Add(BuildTaskRow(task));

        var header = new TextBlock
        {
            Text       = info.Date.ToString("M월 d일 (ddd)"),
            FontSize   = 12,
            FontWeight = FontWeight.SemiBold,
            Margin     = new Thickness(0, 0, 0, 6),
        };
        var root = new StackPanel { Spacing = 0, MinWidth = 220, MaxWidth = 320 };
        root.Children.Add(header);
        var sv = new ScrollViewer
        {
            MaxHeight = 320,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility   = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = inner,
        };
        root.Children.Add(sv);

        var flyout = new Flyout { Content = root, Placement = PlacementMode.Bottom };
        flyout.ShowAt(b);
    }

    private Border BuildEventBar(KEvent ev)
    {
        var brush = HexToBrush(ev.DisplayColor, fallback: Color.FromRgb(0x42, 0x85, 0xF4));

        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 4,
        };
        if (!string.IsNullOrEmpty(ev.TimeLabel))
        {
            sp.Children.Add(new TextBlock
            {
                Text       = ev.TimeLabel,
                FontSize   = 9,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        sp.Children.Add(new TextBlock
        {
            Text         = ev.Title,
            FontSize     = 10,
            FontWeight   = FontWeight.SemiBold,
            Foreground   = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var border = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(4, 1),
            Background   = brush,
            Child        = sp,
            Tag          = ev,
            Cursor       = new Cursor(StandardCursorType.Hand),
        };
        AttachItemDragHandlers(border);
        return border;
    }

    private Grid BuildTaskRow(KEvent task)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin            = new Thickness(2, 0, 2, 0),
            Background        = Brushes.Transparent,
            Tag               = task,
            Cursor            = new Cursor(StandardCursorType.Hand),
        };
        AttachItemDragHandlers(grid);

        // 완료 토글 버튼 (점 대신 — 클릭 시 즉시 IsDone 토글)
        var toggle = new ToggleButton
        {
            IsChecked = task.IsDone,
            Tag       = task,
            MinWidth  = 14,
            Width     = 14,
            Height    = 14,
            Padding   = new Thickness(0),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(2, 0, 6, 0),
            CornerRadius = new CornerRadius(7),
            Background = task.IsDone
                         ? new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
                         : Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)),
        };
        toggle.IsCheckedChanged += OnTaskToggle;
        Grid.SetColumn(toggle, 0);
        grid.Children.Add(toggle);

        var title = new TextBlock
        {
            Text          = task.Title,
            FontSize      = 11,
            TextTrimming  = TextTrimming.CharacterEllipsis,
            TextWrapping  = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            TextDecorations   = task.IsDone ? TextDecorations.Strikethrough : null,
            Foreground        = task.IsDone ? Brushes.Gray : Brushes.Black,
        };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);
        return grid;
    }

    private static SolidColorBrush HexToBrush(string hex, Color fallback)
    {
        if (!string.IsNullOrEmpty(hex) &&
            hex.StartsWith('#') &&
            hex.Length is 7 or 9 &&
            byte.TryParse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
            byte.TryParse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
            byte.TryParse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        return new SolidColorBrush(fallback);
    }

    // ────────────────────────────────────────────────────
    //  포인터 핸들러
    // ────────────────────────────────────────────────────

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (this.FindControl<Border>("HoverHighlight") is { } b)
            b.Opacity = 1;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (this.FindControl<Border>("HoverHighlight") is { } b)
            b.Opacity = 0;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Dayinfo is null) return;
        if (e.Handled) return; // 항목/토글에서 이미 처리됨
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            CellClicked?.Invoke(this, Dayinfo);
    }

    // ────────────────────────────────────────────────────
    //  항목 드래그-드롭 (이벤트/할일을 다른 날짜 셀로 이동)
    // ────────────────────────────────────────────────────

    /// <summary>드래그 페이로드 포맷 — KEvent 인-프로세스 전송.</summary>
    public static readonly DataFormat<KEvent> KEventDragFormat =
        DataFormat.CreateInProcessFormat<KEvent>("saemdesk.kevent");

    /// <summary>일정 바 / 할일 행에 드래그·클릭 핸들러 부착.</summary>
    private void AttachItemDragHandlers(Control element)
    {
        element.PointerPressed += OnItemPointerPressed;
    }

    /// <summary>
    /// PointerPressed 시 즉시 DragDrop.DoDragDropAsync 호출.
    /// Avalonia 가 자체 임계값으로 드래그 vs 클릭을 구분하며, 드래그가 일어나지 않은 경우
    /// DragDropEffects.None 반환 → 클릭으로 처리(ItemClicked 발사).
    /// </summary>
    private async void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not KEvent ev) return;
        if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;
        e.Handled = true; // 빈 영역 클릭(CellClicked)과 충돌 방지

        DragDropEffects result = DragDropEffects.None;
        try
        {
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.Create(KEventDragFormat, ev));
            result = await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DayCell] DoDragDrop 오류: {ex.Message}");
        }

        // 드래그 없이 릴리스(=클릭) → 편집 다이얼로그 진입
        if (result == DragDropEffects.None)
            ItemClicked?.Invoke(this, ev);
    }

    /// <summary>할일 토글 — 즉시 IsDone 갱신 + DB 저장 + 시각 갱신.</summary>
    private async void OnTaskToggle(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not KEvent task) return;

        bool newState = btn.IsChecked == true;
        if (task.IsDone == newState) return; // 외부 갱신으로 IsChecked 가 변경되어 트리거된 경우 — 저장 X

        try
        {
            task.IsDone    = newState;
            task.Updated   = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            task.Completed = task.IsDone ? task.Updated : string.Empty;

            using var svc = SaemDesk.Scheduler.Scheduler.CreateService();
            await svc.UpdateTaskAsync(task);

            // 시각 갱신 (취소선/회색)
            UpdateDisplay();

            SaemDesk.Scheduler.SchedulerEvents.RaiseItemChanged();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DayCell] 할일 토글 오류: {ex.Message}");
        }
    }

    /// <summary>외부에서 강제로 새로고침 알림 트리거.</summary>
    public void RaiseCellChanged() => CellChanged?.Invoke(this, EventArgs.Empty);

    // ────────────────────────────────────────────────────
    //  드롭 타겟
    // ────────────────────────────────────────────────────

    private void OnCellDragOver(object? sender, DragEventArgs e)
    {
        if (Dayinfo is null || !e.DataTransfer.Contains(KEventDragFormat))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Move;
        if (this.FindControl<Border>("HoverHighlight") is { } hi) hi.Opacity = 1;
    }

    private void OnCellDragLeave(object? sender, DragEventArgs e)
    {
        if (this.FindControl<Border>("HoverHighlight") is { } hi) hi.Opacity = 0;
    }

    private void OnCellDrop(object? sender, DragEventArgs e)
    {
        if (this.FindControl<Border>("HoverHighlight") is { } hi) hi.Opacity = 0;
        if (Dayinfo is null) return;
        if (!e.DataTransfer.Contains(KEventDragFormat)) return;

        try
        {
            var moved = e.DataTransfer.TryGetValue(KEventDragFormat);
            if (moved is null) return;
            if (moved.Start.Date == Dayinfo.Date.Date) return;

            e.DragEffects = DragDropEffects.Move;
            ItemDropped?.Invoke(this, (moved, Dayinfo.Date.Date));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DayCell] Drop 처리 오류: {ex.Message}");
        }
    }
}
