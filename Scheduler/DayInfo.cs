using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SaemDesk.Models;

namespace SaemDesk.Scheduler;

/// <summary>
/// 달력 한 칸이 표시할 일별 정보 — NewSchool DayInfo 동등 포팅.
/// SchoolSchedule 로부터 공휴일/휴업/날짜명을 자동 산출한다.
/// </summary>
public sealed class DayInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private DateTime _date;
    private string _dateName = string.Empty;
    private bool _isHoliday;
    private bool _isVacation;
    private bool _isToday;
    private List<KEvent> _tasks  = new();
    private List<KEvent> _events = new();
    private List<SchoolSchedule> _schoolSchedules = new();

    public DateTime Date
    {
        get => _date;
        set { if (_date != value) { _date = value; OnPropertyChanged(); UpdateTodayStatus(); } }
    }

    public string DateName
    {
        get => _dateName;
        set { if (_dateName != value) { _dateName = value ?? string.Empty; OnPropertyChanged(); } }
    }

    public bool IsHoliday
    {
        get => _isHoliday;
        set { if (_isHoliday != value) { _isHoliday = value; OnPropertyChanged(); } }
    }

    public bool IsVacation
    {
        get => _isVacation;
        set { if (_isVacation != value) { _isVacation = value; OnPropertyChanged(); } }
    }

    public bool IsToday
    {
        get => _isToday;
        set { if (_isToday != value) { _isToday = value; OnPropertyChanged(); } }
    }

    /// <summary>할 일(ItemType="task")</summary>
    public List<KEvent> Tasks
    {
        get => _tasks;
        set { _tasks = value ?? new(); OnPropertyChanged(); }
    }

    /// <summary>일정(ItemType != "task")</summary>
    public List<KEvent> Events
    {
        get => _events;
        set { _events = value ?? new(); OnPropertyChanged(); }
    }

    public List<SchoolSchedule> SchoolSchedules
    {
        get => _schoolSchedules;
        set { _schoolSchedules = value ?? new(); OnPropertyChanged(); UpdateDateInfo(); }
    }

    /// <summary>현재 달의 날짜인지(Kcalendar 의 BaseDate.Month 와 비교).</summary>
    public bool IsInCurrentMonth { get; set; } = true;

    public DayInfo() { _date = DateTime.Today; UpdateTodayStatus(); }

    public DayInfo(DateTime date,
                   List<SchoolSchedule>? schedules,
                   List<KEvent>? tasks,
                   List<KEvent>? events,
                   bool isInCurrentMonth = true)
    {
        _date = date;
        _schoolSchedules = schedules ?? new();
        _tasks   = tasks  ?? new();
        _events  = events ?? new();
        IsInCurrentMonth = isInCurrentMonth;
        UpdateDateInfo();
        UpdateTodayStatus();
    }

    private void UpdateDateInfo()
    {
        if (_schoolSchedules.Count > 0)
        {
            DateName   = string.Join(", ", _schoolSchedules.Select(x => x.EVENT_NM));
            IsHoliday  = _schoolSchedules.Any(x => x.SBTR_DD_SC_NM?.Equals("공휴일") == true);
            IsVacation = _schoolSchedules.Any(x => x.SBTR_DD_SC_NM?.Equals("휴업일") == true);
        }
        else
        {
            DateName   = string.Empty;
            IsHoliday  = false;
            IsVacation = false;
        }
    }

    private void UpdateTodayStatus() => IsToday = _date.Date == DateTime.Today;
}
