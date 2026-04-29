using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SaemDesk.Scheduler;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class CalendarHomePage : UserControl
{
    private readonly DayCell[] _cells = new DayCell[42];
    private bool _cellsBuilt;
    private CalendarHomePageVM? _vm;

    private int _pickerYear;

    public CalendarHomePage()
    {
        InitializeComponent();
        Loaded               += OnLoaded;
        Unloaded             += OnUnloaded;
        DataContextChanged   += OnDataContextChanged;
        BuildMonthGrid();
        if (MonthPickerButton.Flyout is FlyoutBase fb)
            fb.Opened += (_, _) => RefreshPickerUi();
    }

    // ────────────────────────────────────────────────────
    //  MonthPicker
    // ────────────────────────────────────────────────────

    private void BuildMonthGrid()
    {
        for (int m = 1; m <= 12; m++)
        {
            var btn = new Button
            {
                Content   = $"{m}월",
                Tag       = m,
                MinWidth  = 50,
                Margin    = new Thickness(2),
                Padding   = new Thickness(8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            btn.Click += OnPickerMonthClicked;
            MonthGrid.Children.Add(btn);
        }
    }

    private void RefreshPickerUi()
    {
        if (_vm is null) return;
        _pickerYear = _vm.BaseDate.Year;
        UpdateMonthGridHighlights();
    }

    private void OnPickerYearPrev(object? sender, RoutedEventArgs e)
    {
        _pickerYear--;
        UpdateMonthGridHighlights();
    }

    private void OnPickerYearNext(object? sender, RoutedEventArgs e)
    {
        _pickerYear++;
        UpdateMonthGridHighlights();
    }

    private void UpdateMonthGridHighlights()
    {
        if (_vm is null) return;
        PickerYearText.Text = $"{_pickerYear}년";
        foreach (var child in MonthGrid.Children)
        {
            if (child is Button b && b.Tag is int m)
            {
                bool active = (_pickerYear == _vm.BaseDate.Year && m == _vm.BaseDate.Month);
                b.FontWeight = active ? FontWeight.Bold : FontWeight.Normal;
                b.Background = active
                    ? new SolidColorBrush(Color.FromArgb(0x33, 0x42, 0x85, 0xF4))
                    : Brushes.Transparent;
            }
        }
    }

    private void OnPickerMonthClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not int m || _vm is null) return;
        _vm.BaseDate = new DateTime(_pickerYear, m, 1);
        MonthPickerButton.Flyout?.Hide();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SchedulerEvents.ItemChanged -= OnExternalItemChanged;
    }

    private void OnExternalItemChanged(object? sender, EventArgs e)
    {
        // 다른 화면(예: TodayPage 어젠다)에서 항목이 바뀐 경우 → UI 스레드에서 새로고침
        Dispatcher.UIThread.Post(async () => await ReloadAsync());
    }

    // ────────────────────────────────────────────────────
    //  초기화
    // ────────────────────────────────────────────────────

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        BuildCellsOnce();
        // 외부 변경(어젠다/다이얼로그 등) 구독 — Unloaded 에서 해제
        SchedulerEvents.ItemChanged -= OnExternalItemChanged;
        SchedulerEvents.ItemChanged += OnExternalItemChanged;
        _ = ReloadAsync();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as CalendarHomePageVM;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private async void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalendarHomePageVM.BaseDate))
            await ReloadAsync();
    }

    private void BuildCellsOnce()
    {
        if (_cellsBuilt) return;
        var grid = this.FindControl<Grid>("CellsGrid")!;
        grid.Children.Clear();

        for (int i = 0; i < 42; i++)
        {
            int row = i / 7;
            int col = i % 7;
            var cell = new DayCell();
            cell.CellClicked += OnCellClicked;
            cell.ItemClicked += OnItemClicked;
            cell.ItemDropped += OnItemDropped;
            cell.CellChanged += OnCellChanged;
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            grid.Children.Add(cell);
            _cells[i] = cell;
        }
        _cellsBuilt = true;
    }

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task ReloadAsync()
    {
        if (_vm is null || !_cellsBuilt) return;
        try
        {
            var infos = await _vm.LoadAsync();
            for (int i = 0; i < 42; i++)
                _cells[i].Dayinfo = infos[i];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CalendarHomePage] 로드 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  헤더 버튼
    // ────────────────────────────────────────────────────

    private void OnPreviousMonth(object? sender, RoutedEventArgs e) => _vm?.GotoPreviousMonth();
    private void OnNextMonth   (object? sender, RoutedEventArgs e) => _vm?.GotoNextMonth();
    private void OnToday       (object? sender, RoutedEventArgs e) => _vm?.GotoToday();
    private async void OnRefresh(object? sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnSettings(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SaemDesk.Services.DialogService.ShowCalendarSettingsAsync();
            // 표시 옵션(ShowEvents/ShowTasks)이 바뀌었을 수 있으므로 새로고침
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CalendarHomePage] 설정 다이얼로그 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  셀 이벤트
    // ────────────────────────────────────────────────────

    private async void OnCellClicked(object? sender, DayInfo info)
    {
        try
        {
            var (saved, deleted) = await SaemDesk.Services.DialogService.ShowUnifiedItemEditAsync(info.Date);
            if (saved is not null || deleted)
            {
                await ReloadAsync();
                SchedulerEvents.RaiseItemChanged();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CalendarHomePage] 셀 클릭 처리 오류: {ex.Message}");
        }
    }

    /// <summary>셀 안의 일정 바 / 할일 행 클릭 — 그 항목 편집.</summary>
    private async void OnItemClicked(object? sender, KEvent existing)
    {
        try
        {
            var (saved, deleted) = await SaemDesk.Services.DialogService.ShowUnifiedItemEditAsync(existing);
            if (saved is not null || deleted)
            {
                await ReloadAsync();
                SchedulerEvents.RaiseItemChanged();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CalendarHomePage] 항목 편집 오류: {ex.Message}");
        }
    }

    private async void OnCellChanged(object? sender, EventArgs e)
    {
        await ReloadAsync();
    }

    /// <summary>드래그-드롭으로 항목이 다른 날짜로 이동된 경우 — Start/End 시프트 + DB 저장.</summary>
    private async void OnItemDropped(object? sender, (KEvent Moved, DateTime NewDate) args)
    {
        var ev = args.Moved;
        var newDate = args.NewDate.Date;
        try
        {
            // 시프트량 = 새 날짜 - 기존 시작일(시간 보존)
            int dayShift = (newDate - ev.Start.Date).Days;
            if (dayShift == 0) return;

            ev.Start = DateTime.SpecifyKind(ev.Start.AddDays(dayShift), DateTimeKind.Unspecified);
            ev.End   = DateTime.SpecifyKind(ev.End.AddDays(dayShift),   DateTimeKind.Unspecified);
            ev.Updated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            using var svc = SaemDesk.Scheduler.Scheduler.CreateService();
            if (ev.ItemType == "task") await svc.UpdateTaskAsync(ev);
            else                       await svc.UpdateEventAsync(ev);

            SchedulerEvents.RaiseItemChanged();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CalendarHomePage] 항목 이동 오류: {ex.Message}");
        }
    }
}
