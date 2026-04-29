using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SaemDesk.Google;
using SaemDesk.Scheduler;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 할 일(KEvent task) / 일정(KEvent event) 통합 편집 다이얼로그.
/// NewSchool UnifiedItemDialog 동등 포팅. ContentDialog → Avalonia Window.
/// </summary>
public partial class UnifiedItemDialog : Window
{
    /// <summary>저장된 KEvent (저장 성공 시 채워짐, 취소/삭제 시 null).</summary>
    public KEvent? ResultEvent { get; private set; }

    /// <summary>삭제 여부 (true 시 부모는 새로고침).</summary>
    public bool Deleted { get; private set; }

    private KEvent _taskEvent;
    private KEvent _event;
    private bool _isTaskMode = true;
    private bool _isNew      = true;
    private bool _initialized;

    private List<KCalendarList> _calendars = new();
    private List<string>         _titles   = new();

    /// <summary>새 항목 — 날짜만 지정.</summary>
    public UnifiedItemDialog(DateTime date)
    {
        _taskEvent = NewTaskEvent(date);
        _event     = NewEvent(date);
        _isNew     = true;
        InitializeComponent();
        Opened += OnOpened;
    }

    /// <summary>기존 KEvent 수정.</summary>
    public UnifiedItemDialog(KEvent existing)
    {
        if (existing.ItemType == "task")
        {
            _taskEvent  = existing;
            _event      = NewEvent(existing.Start);
            _isTaskMode = true;
        }
        else
        {
            _taskEvent  = NewTaskEvent(existing.Start);
            _event      = existing;
            _isTaskMode = false;
        }
        _isNew = existing.No < 0;
        InitializeComponent();
        Opened += OnOpened;
    }

    // ────────────────────────────────────────────────────
    //  초기화
    // ────────────────────────────────────────────────────

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            await LoadCalendarListsAsync();

            RbTypeTask.IsChecked  = _isTaskMode;
            RbTypeEvent.IsChecked = !_isTaskMode;

            UpdatePanelVisibility();

            if (_isTaskMode) FillTaskForm();
            else             FillEventForm();

            BtnDelete.IsVisible = !_isNew;
            Title = _isNew
                ? (_isTaskMode ? "새 할 일" : "새 일정")
                : (_isTaskMode ? "할 일 수정" : "일정 수정");

            UpdateGoogleSyncCheckboxVisibility();

            _initialized = true;
            if (_isTaskMode) UpdateRepeatLabels();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnifiedItemDialog] 초기화 오류: {ex.Message}");
        }
    }

    private async Task LoadCalendarListsAsync()
    {
        try
        {
            using var svc = Scheduler.Scheduler.CreateService();
            _calendars = await svc.GetAllCalendarsAsync();
            _titles    = _calendars.Select(c => c.Title).ToList();

            CBoxTaskList.ItemsSource = _titles;
            int taskIdx = _calendars.FindIndex(c => c.No == _taskEvent.CalendarId);
            CBoxTaskList.SelectedIndex = taskIdx >= 0 ? taskIdx : 0;

            CBoxCalendar.ItemsSource = _titles;
            int calIdx = _calendars.FindIndex(c => c.No == _event.CalendarId);
            CBoxCalendar.SelectedIndex = calIdx >= 0 ? calIdx : 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnifiedItemDialog] 목록 로드 오류: {ex.Message}");
        }
    }

    private void FillTaskForm()
    {
        TxtTaskTitle.Text         = _taskEvent.Title;
        TaskDuePicker.SelectedDate = new DateTimeOffset(_taskEvent.Start.Date, TimeSpan.Zero);
        TaskDueTimePicker.Time    = _taskEvent.Start.TimeOfDay;
        ChkTaskAllday.IsChecked   = _taskEvent.IsAllday;
        ChkTaskDone.IsChecked     = _taskEvent.IsDone;
        TxtTaskNotes.Text         = _taskEvent.Notes;

        TaskDueTimePicker.IsVisible = !_taskEvent.IsAllday;
        GridRepeat.IsVisible        = _isNew;
        UpdateRepeatLabels();
    }

    private void FillEventForm()
    {
        TxtEventTitle.Text                 = _event.Title;
        EventStartDatePicker.SelectedDate  = new DateTimeOffset(_event.Start.Date, TimeSpan.Zero);
        EventStartTimePicker.Time          = _event.Start.TimeOfDay;
        EventEndDatePicker.SelectedDate    = new DateTimeOffset(_event.End.Date,   TimeSpan.Zero);
        EventEndTimePicker.Time            = _event.End.TimeOfDay;
        ChkEventAllday.IsChecked           = _event.IsAllday;
        TxtEventLocation.Text              = _event.Location;
        TxtEventNotes.Text                 = _event.Notes;

        EventStartTimePicker.IsVisible = !_event.IsAllday;
        EventEndTimePicker.IsVisible   = !_event.IsAllday;

        CBoxColor.SelectedIndex = FindColorIndex(_event.ColorId);
        UpdateColorPreview(_event.ColorId);
    }

    // ────────────────────────────────────────────────────
    //  탭 전환
    // ────────────────────────────────────────────────────

    private void OnTypeChanged(object? sender, RoutedEventArgs e)
    {
        if (!_initialized) { _isTaskMode = RbTypeTask.IsChecked == true; return; }
        _isTaskMode = RbTypeTask.IsChecked == true;
        UpdatePanelVisibility();
        Title = _isNew
            ? (_isTaskMode ? "새 할 일" : "새 일정")
            : (_isTaskMode ? "할 일 수정" : "일정 수정");
    }

    private void UpdatePanelVisibility()
    {
        PanelTask.IsVisible  = _isTaskMode;
        PanelEvent.IsVisible = !_isTaskMode;
    }

    // ────────────────────────────────────────────────────
    //  할 일 폼 이벤트
    // ────────────────────────────────────────────────────

    private void CBoxTaskList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || CBoxTaskList.SelectedIndex < 0 ||
            CBoxTaskList.SelectedIndex >= _calendars.Count) return;
        _taskEvent.CalendarId = _calendars[CBoxTaskList.SelectedIndex].No;
        if (Settings.UseGoogle.Value)
            UpdateSyncCheckbox(ChkTaskGoogleSync, CBoxTaskList.SelectedIndex);
    }

    private void TaskDuePicker_DateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        if (!_initialized || e.NewDate is not DateTimeOffset dto) return;
        var d = dto.Date;
        _taskEvent.Start = DateTime.SpecifyKind(d + _taskEvent.Start.TimeOfDay, DateTimeKind.Unspecified);
        _taskEvent.End   = DateTime.SpecifyKind(d, DateTimeKind.Unspecified);
        UpdateRepeatLabels();
    }

    private void ChkTaskAllday_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        _taskEvent.IsAllday          = ChkTaskAllday.IsChecked == true;
        TaskDueTimePicker.IsVisible  = !_taskEvent.IsAllday;
        if (_taskEvent.IsAllday)
            _taskEvent.End = DateTime.SpecifyKind(_taskEvent.Start.Date, DateTimeKind.Unspecified);
        UpdateRepeatLabels();
    }

    private void RepeatOption_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        bool show = RbNone.IsChecked != true;
        PickerEnd.IsVisible = show;
    }

    private void UpdateRepeatLabels()
    {
        if (!_initialized) return;
        string t = _taskEvent.IsAllday ? "" : $" {_taskEvent.Start:HH:mm}";
        RbDaily.Content   = $"매일{t}";
        RbWeekly.Content  = $"매주 {KorDow(_taskEvent.Start.DayOfWeek)}{t}";
        RbMonthly.Content = $"매월 {_taskEvent.Start.Day}일{t}";
        RbYearly.Content  = $"매년 {_taskEvent.Start.Month}월 {_taskEvent.Start.Day}일{t}";
    }

    // ────────────────────────────────────────────────────
    //  일정 폼 이벤트
    // ────────────────────────────────────────────────────

    private void CBoxCalendar_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || CBoxCalendar.SelectedIndex < 0 ||
            CBoxCalendar.SelectedIndex >= _calendars.Count) return;
        var cal = _calendars[CBoxCalendar.SelectedIndex];
        _event.CalendarId = cal.No;
        if (string.IsNullOrEmpty(_event.ColorId))
            UpdateColorPreviewHex(cal.Color);
        if (Settings.UseGoogle.Value)
            UpdateSyncCheckbox(ChkEventGoogleSync, CBoxCalendar.SelectedIndex);
    }

    private void EventStartDate_Changed(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        if (!_initialized || e.NewDate is not DateTimeOffset dto) return;
        var d = dto.Date;
        _event.Start = DateTime.SpecifyKind(d + _event.Start.TimeOfDay, DateTimeKind.Unspecified);
        if (_event.End.Date < d)
            _event.End = DateTime.SpecifyKind(d + _event.End.TimeOfDay, DateTimeKind.Unspecified);
    }

    private void EventEndDate_Changed(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        if (!_initialized || e.NewDate is not DateTimeOffset dto) return;
        _event.End = DateTime.SpecifyKind(dto.Date + _event.End.TimeOfDay, DateTimeKind.Unspecified);
    }

    private void ChkEventAllday_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        _event.IsAllday              = ChkEventAllday.IsChecked == true;
        EventStartTimePicker.IsVisible = !_event.IsAllday;
        EventEndTimePicker.IsVisible   = !_event.IsAllday;
    }

    private void CBoxColor_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || CBoxColor.SelectedItem is not ComboBoxItem item) return;
        _event.ColorId = item.Tag?.ToString() ?? string.Empty;
        UpdateColorPreview(_event.ColorId);
    }

    // ────────────────────────────────────────────────────
    //  저장 / 삭제 / 취소
    // ────────────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_isTaskMode) await SaveTaskAsync();
            else             await SaveEventAsync();

            if (ResultEvent is null) return;

            // 즉시 Google Push (옵션)
            bool shouldPush = _isTaskMode
                ? ChkTaskGoogleSync.IsChecked == true
                : ChkEventGoogleSync.IsChecked == true;
            if (shouldPush)
                await PushToGoogleAsync(ResultEvent);

            SaemDesk.Scheduler.SchedulerEvents.RaiseItemChanged();
            Close();
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"저장 실패: {ex.Message}";
            Debug.WriteLine($"[UnifiedItemDialog] 저장 오류: {ex}");
        }
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (_isNew) { Close(); return; }
        try
        {
            using var svc = Scheduler.Scheduler.CreateService();
            int no = _isTaskMode ? _taskEvent.No : _event.No;
            await svc.DeleteEventAsync(no);
            Deleted = true;
            SaemDesk.Scheduler.SchedulerEvents.RaiseItemChanged();
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"삭제 실패: {ex.Message}";
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private async Task SaveTaskAsync()
    {
        _taskEvent.Title    = (TxtTaskTitle.Text ?? "").Trim();
        _taskEvent.Notes    = TxtTaskNotes.Text ?? "";
        _taskEvent.IsDone   = ChkTaskDone.IsChecked == true;
        _taskEvent.ItemType = "task";
        _taskEvent.Updated  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        if (string.IsNullOrWhiteSpace(_taskEvent.Title))
            throw new InvalidOperationException("제목을 입력해주세요.");

        _taskEvent.Start = DateTime.SpecifyKind(_taskEvent.Start, DateTimeKind.Unspecified);
        _taskEvent.End   = DateTime.SpecifyKind(_taskEvent.End,   DateTimeKind.Unspecified);
        _taskEvent.Completed = _taskEvent.IsDone ? _taskEvent.Updated : string.Empty;

        var generated = GenerateRepeatTasks();
        using var svc = Scheduler.Scheduler.CreateService();

        if (generated.Count <= 1)
        {
            if (_taskEvent.No <= 0) _taskEvent.No = await svc.CreateTaskAsync(_taskEvent);
            else                    await svc.UpdateTaskAsync(_taskEvent);
            ResultEvent = _taskEvent;
        }
        else
        {
            // 반복 → 트랜잭션 일괄 생성
            using var uow = Scheduler.Scheduler.CreateUnitOfWork();
            await uow.ExecuteInTransactionAsync(async () =>
            {
                foreach (var t in generated)
                {
                    t.Start = DateTime.SpecifyKind(t.Start, DateTimeKind.Unspecified);
                    t.End   = DateTime.SpecifyKind(t.End,   DateTimeKind.Unspecified);
                    t.No    = await uow.KEvents.CreateAsync(t);
                }
            });
            ResultEvent = generated.First();
        }
    }

    private async Task SaveEventAsync()
    {
        _event.Title    = (TxtEventTitle.Text ?? "").Trim();
        _event.Notes    = TxtEventNotes.Text ?? "";
        _event.Location = (TxtEventLocation.Text ?? "").Trim();
        _event.Updated  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        _event.User     = Environment.UserName;
        _event.ItemType = "event";

        if (string.IsNullOrWhiteSpace(_event.Title))
            throw new InvalidOperationException("제목을 입력해주세요.");

        _event.Start = DateTime.SpecifyKind(_event.Start, DateTimeKind.Unspecified);
        _event.End   = DateTime.SpecifyKind(_event.End,   DateTimeKind.Unspecified);
        if (_event.End < _event.Start) _event.End = _event.Start.AddHours(1);

        using var svc = Scheduler.Scheduler.CreateService();
        if (_event.No <= 0) _event.No = await svc.CreateEventAsync(_event);
        else                await svc.UpdateEventAsync(_event);

        ResultEvent = _event;
    }

    // ────────────────────────────────────────────────────
    //  반복 작업 생성
    // ────────────────────────────────────────────────────

    private List<KEvent> GenerateRepeatTasks()
    {
        var list = new List<KEvent>();
        if (RbNone.IsChecked == true) { list.Add(_taskEvent); return list; }

        var endDate = DateTime.SpecifyKind(
            PickerEnd.SelectedDate?.Date ?? _taskEvent.Start.Date.AddYears(1),
            DateTimeKind.Unspecified);
        var current = DateTime.SpecifyKind(_taskEvent.Start.Date, DateTimeKind.Unspecified);
        int count = 0;

        while (current <= endDate && count < 365)
        {
            var t = CloneTaskEvent(_taskEvent);
            t.Start = DateTime.SpecifyKind(current + _taskEvent.Start.TimeOfDay, DateTimeKind.Unspecified);
            t.End   = DateTime.SpecifyKind(current, DateTimeKind.Unspecified);
            list.Add(t);
            count++;

            if      (RbDaily.IsChecked   == true) current = current.AddDays(1);
            else if (RbWeekly.IsChecked  == true) current = current.AddDays(7);
            else if (RbMonthly.IsChecked == true) current = current.AddMonths(1);
            else if (RbYearly.IsChecked  == true) current = current.AddYears(1);
            else break;
        }
        return list;
    }

    private static KEvent CloneTaskEvent(KEvent src) => new()
    {
        GoogleId   = src.GoogleId,
        Title      = src.Title,
        Notes      = src.Notes,
        Start      = src.Start,
        End        = src.End,
        IsAllday   = src.IsAllday,
        IsDone     = src.IsDone,
        ItemType   = "task",
        CalendarId = src.CalendarId,
        User       = src.User,
        Updated    = src.Updated,
        Completed  = src.Completed,
        Status     = "confirmed",
    };

    // ────────────────────────────────────────────────────
    //  색상 프리뷰
    // ────────────────────────────────────────────────────

    private void UpdateColorPreview(string colorId)
    {
        var hex = string.IsNullOrEmpty(colorId)
            ? GetCalendarColor()
            : KEvent.ColorIdToHex(colorId);
        UpdateColorPreviewHex(hex);
    }

    private void UpdateColorPreviewHex(string hex)
    {
        if (string.IsNullOrEmpty(hex)) hex = "#4285F4";
        try
        {
            string h = hex.TrimStart('#');
            byte r = byte.Parse(h.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(h.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(h.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            ColorPreview.Background = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }
        catch { ColorPreview.Background = new SolidColorBrush(Colors.Gray); }
    }

    private string GetCalendarColor()
    {
        if (CBoxCalendar.SelectedIndex >= 0 && CBoxCalendar.SelectedIndex < _calendars.Count)
            return _calendars[CBoxCalendar.SelectedIndex].Color;
        return "#4285F4";
    }

    private static int FindColorIndex(string colorId)
    {
        var ids = new[] { "", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" };
        int idx = Array.IndexOf(ids, colorId ?? string.Empty);
        return idx >= 0 ? idx : 0;
    }

    // ────────────────────────────────────────────────────
    //  Google Sync 체크박스
    // ────────────────────────────────────────────────────

    private void UpdateGoogleSyncCheckboxVisibility()
    {
        bool googleEnabled = Settings.UseGoogle.Value;
        if (googleEnabled)
        {
            UpdateSyncCheckbox(ChkTaskGoogleSync,  CBoxTaskList.SelectedIndex);
            UpdateSyncCheckbox(ChkEventGoogleSync, CBoxCalendar.SelectedIndex);
        }
        else
        {
            ChkTaskGoogleSync.IsVisible  = false;
            ChkEventGoogleSync.IsVisible = false;
        }
    }

    private void UpdateSyncCheckbox(CheckBox chk, int calendarIndex)
    {
        if (calendarIndex >= 0 && calendarIndex < _calendars.Count)
        {
            var cal = _calendars[calendarIndex];
            bool isTwoWay = cal.SyncMode == "TwoWay"
                            && !string.IsNullOrEmpty(cal.GoogleId)
                            && cal.Title != "담임";
            chk.IsVisible = isTwoWay;
            chk.IsChecked = isTwoWay;
        }
        else
        {
            chk.IsVisible = false;
        }
    }

    private async Task PushToGoogleAsync(KEvent ev)
    {
        try
        {
            var cal = _calendars.FirstOrDefault(c => c.No == ev.CalendarId);
            if (cal is null || string.IsNullOrEmpty(cal.GoogleId)) return;

            using var auth = new GoogleAuthService();
            var api  = new GoogleCalendarApiClient(auth);
            var ge   = GoogleSyncService.ConvertToGoogleEvent(ev);

            using var svc = Scheduler.Scheduler.CreateService();
            if (string.IsNullOrEmpty(ev.GoogleId))
            {
                var created = await api.InsertEventAsync(cal.GoogleId, ge);
                if (created?.Id != null)
                {
                    ev.GoogleId = created.Id;
                    ev.Updated  = created.Updated ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    await svc.UpdateEventAsync(ev);
                }
            }
            else
            {
                var updated = await api.UpdateEventAsync(cal.GoogleId, ev.GoogleId, ge);
                if (updated != null)
                {
                    ev.Updated = updated.Updated ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    await svc.UpdateEventAsync(ev);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnifiedItemDialog] 구글 Push 실패: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  Static Factories / Helpers
    // ────────────────────────────────────────────────────

    private static KEvent NewTaskEvent(DateTime date) => new()
    {
        No       = -1,
        ItemType = "task",
        Start    = DateTime.SpecifyKind(date.Date.AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute), DateTimeKind.Unspecified),
        End      = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified),
        IsAllday = true,
        IsDone   = false,
        Status   = "confirmed",
        User     = Environment.UserName,
        Updated  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
    };

    private static KEvent NewEvent(DateTime date) => new()
    {
        No       = -1,
        ItemType = "event",
        Start    = DateTime.SpecifyKind(date.Date.AddHours(9),  DateTimeKind.Unspecified),
        End      = DateTime.SpecifyKind(date.Date.AddHours(10), DateTimeKind.Unspecified),
        IsAllday = false,
        Status   = "confirmed",
        User     = Environment.UserName,
        Updated  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
    };

    private static string KorDow(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday    => "일요일",
        DayOfWeek.Monday    => "월요일",
        DayOfWeek.Tuesday   => "화요일",
        DayOfWeek.Wednesday => "수요일",
        DayOfWeek.Thursday  => "목요일",
        DayOfWeek.Friday    => "금요일",
        DayOfWeek.Saturday  => "토요일",
        _                   => string.Empty,
    };
}
