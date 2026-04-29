using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaemDesk.Scheduler;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 할 일 + 일정 통합 어젠다 컨트롤 — NewSchool KAgendaControl 동등 포팅.
///
/// 사용 시나리오:
/// - 기본: 부모가 <see cref="LoadPendingAndFutureAsync"/> 또는 <see cref="LoadByDateRangeAsync"/> 호출
/// - 카테고리 고정: <see cref="FixedCalendarName"/> 설정 시 해당 캘린더로만 잠금 + 필터 UI 자동 숨김
/// - 필터 UI 숨김(전체 표시 유지): <see cref="ShowFilter"/> = false
/// </summary>
public partial class KAgendaControl : UserControl
{
    private List<KCalendarList> _calendars = new();
    private List<AgendaItem>    _allItems  = new();
    private bool _filterInitialized;
    private int  _selectedCalendarId;
    private bool _showTasks  = true;
    private bool _showEvents = true;

    /// <summary>새 항목 추가 시 기본 CalendarId.</summary>
    public int DefaultCalendarId { get; set; }

    public static readonly StyledProperty<bool> ShowFilterProperty =
        AvaloniaProperty.Register<KAgendaControl, bool>(nameof(ShowFilter), defaultValue: true);

    public static readonly StyledProperty<string?> FixedCalendarNameProperty =
        AvaloniaProperty.Register<KAgendaControl, string?>(nameof(FixedCalendarName));

    public bool ShowFilter
    {
        get => GetValue(ShowFilterProperty);
        set => SetValue(ShowFilterProperty, value);
    }

    /// <summary>지정한 카테고리만 표시 + 필터 UI 자동 숨김.</summary>
    public string? FixedCalendarName
    {
        get => GetValue(FixedCalendarNameProperty);
        set => SetValue(FixedCalendarNameProperty, value);
    }

    public KAgendaControl()
    {
        InitializeComponent();
        AttachedToVisualTree   += (_, _) => SchedulerEvents.ItemChanged += OnExternalItemChanged;
        DetachedFromVisualTree += (_, _) => SchedulerEvents.ItemChanged -= OnExternalItemChanged;
    }

    private void OnExternalItemChanged(object? sender, EventArgs e)
    {
        // 다른 화면(달력 등)에서 항목이 바뀐 경우 — 자체적으로 다시 로드
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            // 현재 모드(범위/미래)에 따라 적절히 재로드 — 기본은 LoadPendingAndFutureAsync
            await LoadPendingAndFutureAsync();
        });
    }

    // ────────────────────────────────────────────────────
    //  공개 로드
    // ────────────────────────────────────────────────────

    /// <summary>오늘 기준 미완료 작업 + 미래 60일 일정 로드 (TodayPage 용).</summary>
    public async Task LoadPendingAndFutureAsync()
    {
        try
        {
            await EnsureFiltersAsync();
            using var svc = Scheduler.Scheduler.CreateService();

            var tasks  = await svc.GetPendingAndFutureTasksAsync();
            var events = (await svc.GetEventsByDateAsync(DateTime.Today, 60))
                            .Where(e => e.ItemType != "task").ToList();

            var all = new List<KEvent>(tasks.Count + events.Count);
            all.AddRange(tasks);
            all.AddRange(events);

            BuildAllItems(all);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KAgendaControl] LoadPendingAndFutureAsync 오류: {ex.Message}");
        }
    }

    /// <summary>날짜 범위 지정 로드.</summary>
    public async Task LoadByDateRangeAsync(DateTime start, int days = 30, bool showCompleted = true)
    {
        try
        {
            await EnsureFiltersAsync();
            using var svc = Scheduler.Scheduler.CreateService();

            var tasks  = await svc.GetTasksByDateAsync(start, days, showCompleted);
            var events = (await svc.GetEventsByDateAsync(start, days))
                            .Where(e => e.ItemType != "task").ToList();

            var all = new List<KEvent>(tasks.Count + events.Count);
            all.AddRange(tasks);
            all.AddRange(events);

            BuildAllItems(all);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KAgendaControl] LoadByDateRangeAsync 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  필터 초기화
    // ────────────────────────────────────────────────────

    private async Task EnsureFiltersAsync()
    {
        if (_filterInitialized) return;
        try
        {
            using var svc = Scheduler.Scheduler.CreateService();
            _calendars = await svc.GetAllCalendarsAsync();

            var names = new List<string> { "전체" };
            names.AddRange(_calendars.Select(c => c.Title));
            CBoxFilter.ItemsSource   = names;
            CBoxFilter.SelectedIndex = 0;
            TbTask.IsChecked  = true;
            TbEvent.IsChecked = true;
            _filterInitialized = true;

            // FixedCalendarName 적용 → 자동 숨김
            if (!string.IsNullOrEmpty(FixedCalendarName))
            {
                var fixedCal = _calendars.FirstOrDefault(c => c.Title == FixedCalendarName);
                if (fixedCal is not null)
                {
                    _selectedCalendarId = fixedCal.No;
                    DefaultCalendarId   = fixedCal.No;
                }
                CBoxFilter.IsVisible = false;
                TbTask.IsVisible     = false;
                TbEvent.IsVisible    = false;
            }
            else if (!ShowFilter)
            {
                CBoxFilter.IsVisible = false;
                TbTask.IsVisible     = false;
                TbEvent.IsVisible    = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KAgendaControl] EnsureFiltersAsync 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  데이터 빌드 + 필터 적용
    // ────────────────────────────────────────────────────

    private void BuildAllItems(List<KEvent> allEvents)
    {
        _allItems = new List<AgendaItem>(allEvents.Count);
        foreach (var ev in allEvents)
        {
            var cal = _calendars.FirstOrDefault(c => c.No == ev.CalendarId);
            string name  = cal?.Title ?? string.Empty;
            string color = cal?.Color ?? "#4285F4";

            if (ev.ItemType == "task")
            {
                _allItems.Add(AgendaItem.FromTask(ev, name, color));
            }
            else
            {
                int days = (ev.End.Date - ev.Start.Date).Days;
                if (days < 0) days = 0;
                for (int d = 0; d <= days; d++)
                    _allItems.Add(AgendaItem.FromEvent(ev, name, color,
                        displayDate: ev.Start.Date.AddDays(d)));
            }
        }
    }

    private void ApplyFilter()
    {
        var filtered = _allItems.AsEnumerable();
        if (!_showTasks)  filtered = filtered.Where(i => !i.IsTask);
        if (!_showEvents) filtered = filtered.Where(i => !i.IsEvent);
        if (_selectedCalendarId > 0)
            filtered = filtered.Where(i => i.SourceEvent != null && i.SourceEvent.CalendarId == _selectedCalendarId);

        var flat = new List<object>();
        foreach (var g in filtered.OrderBy(i => i.DisplayDate).ThenBy(i => i.SortKey).GroupBy(i => i.DisplayDate))
        {
            var items = g.ToList();
            var (header, _) = AgendaHeader.Create(g.Key, items);
            flat.Add(header);
            flat.AddRange(items);
        }
        AgendaListBox.ItemsSource = flat;
        EmptyText.IsVisible       = flat.Count == 0;
    }

    // ────────────────────────────────────────────────────
    //  이벤트 핸들러
    // ────────────────────────────────────────────────────

    private void OnCalendarFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_filterInitialized) return;
        int idx = CBoxFilter.SelectedIndex;
        _selectedCalendarId = (idx <= 0) ? 0 : _calendars[idx - 1].No;
        DefaultCalendarId   = _selectedCalendarId;
        ApplyFilter();
    }

    private void OnTypeFilterChanged(object? sender, RoutedEventArgs e)
    {
        if (!_filterInitialized) return;
        _showTasks  = TbTask.IsChecked  == true;
        _showEvents = TbEvent.IsChecked == true;
        ApplyFilter();
    }

    private async void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var (saved, _) = await SaemDesk.Services.DialogService.ShowUnifiedItemEditAsync(DateTime.Today);
            if (saved is null) return;

            var cal = _calendars.FirstOrDefault(c => c.No == saved.CalendarId);
            string name  = cal?.Title ?? string.Empty;
            string color = cal?.Color ?? "#4285F4";

            if (saved.ItemType == "task")
            {
                _allItems.Add(AgendaItem.FromTask(saved, name, color));
            }
            else
            {
                int days = Math.Max(0, (saved.End.Date - saved.Start.Date).Days);
                for (int d = 0; d <= days; d++)
                    _allItems.Add(AgendaItem.FromEvent(saved, name, color,
                        displayDate: saved.Start.Date.AddDays(d)));
            }
            ApplyFilter();
            SchedulerEvents.RaiseItemChanged();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KAgendaControl] OnAddClicked 오류: {ex.Message}");
        }
    }

    private async void OnAgendaItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control c || c.Tag is not AgendaItem item || item.SourceEvent is null) return;
        if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;

        try
        {
            var (saved, deleted) = await SaemDesk.Services.DialogService.ShowUnifiedItemEditAsync(item.SourceEvent);

            if (deleted)
            {
                _allItems.RemoveAll(a => a.SourceEvent == item.SourceEvent);
                ApplyFilter();
                SchedulerEvents.RaiseItemChanged();
                return;
            }
            if (saved is null) return;

            // 같은 SourceEvent 참조 모두 제거 후 재등록 (다일 일정 복제본 포함)
            _allItems.RemoveAll(a => a.SourceEvent == item.SourceEvent);

            var cal = _calendars.FirstOrDefault(c => c.No == saved.CalendarId);
            string name  = cal?.Title ?? string.Empty;
            string color = cal?.Color ?? "#4285F4";

            if (saved.ItemType == "task")
            {
                _allItems.Add(AgendaItem.FromTask(saved, name, color));
            }
            else
            {
                int days = Math.Max(0, (saved.End.Date - saved.Start.Date).Days);
                for (int d = 0; d <= days; d++)
                    _allItems.Add(AgendaItem.FromEvent(saved, name, color,
                        displayDate: saved.Start.Date.AddDays(d)));
            }
            ApplyFilter();
            SchedulerEvents.RaiseItemChanged();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KAgendaControl] 항목 편집 오류: {ex.Message}");
        }
    }

    private async void OnTaskToggleClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not AgendaItem item ||
            item.SourceEvent is null || !item.IsTask) return;

        try
        {
            var task = item.SourceEvent;
            task.IsDone    = btn.IsChecked == true;
            task.Updated   = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            task.Completed = task.IsDone ? task.Updated : string.Empty;
            item.IsTaskDone = task.IsDone;

            using var svc = Scheduler.Scheduler.CreateService();
            await svc.UpdateTaskAsync(task);

            SchedulerEvents.RaiseItemChanged();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KAgendaControl] TaskToggle 오류: {ex.Message}");
        }
    }
}
