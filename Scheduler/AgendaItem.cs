using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace SaemDesk.Scheduler;

/// <summary>
/// 할 일/일정을 통합 표현하는 ViewModel — KAgendaControl 의 행 단위.
/// NewSchool AgendaItem 동등 포팅 (KEvent 단일 모델, ItemType 으로 구분).
/// </summary>
public sealed class AgendaItem : INotifyPropertyChanged
{
    public KEvent? SourceEvent { get; init; }

    public bool IsTask  => SourceEvent?.ItemType == "task";
    public bool IsEvent => SourceEvent != null && SourceEvent.ItemType != "task";

    public string Title    => SourceEvent?.Title ?? string.Empty;
    public DateTime SortKey => SourceEvent?.Start ?? DateTime.MinValue;

    /// <summary>리스트 그룹핑용 표시 날짜(다일 일정은 날짜별로 복제).</summary>
    public DateTime DisplayDate { get; init; }

    public string TimeLabel
    {
        get
        {
            if (SourceEvent is null) return string.Empty;
            if (IsTask)
                return SourceEvent.IsAllday ? "종일" : SourceEvent.Start.ToString("HH:mm");
            if (SourceEvent.IsAllday) return "종일";
            return $"{SourceEvent.Start:HH:mm}~{SourceEvent.End:HH:mm}";
        }
    }

    public string TypeIcon  => IsTask ? (IsTaskDone ? "●" : "○") : "▶";

    public string AccentColor => IsTask
        ? (IsTaskDone ? "#AAAAAA" : "#1565C0")
        : (string.IsNullOrEmpty(_accentHex) ? "#4285F4" : _accentHex);

    private string _accentHex = string.Empty;

    /// <summary>분류/캘린더 이름 (배지).</summary>
    public string CategoryName    { get; init; } = string.Empty;
    /// <summary>배지 배경색 HEX.</summary>
    public string BadgeBackground { get; init; } = "#9E9E9E";

    private bool _isTaskDone;
    public bool IsTaskDone
    {
        get => _isTaskDone;
        set
        {
            if (_isTaskDone == value) return;
            _isTaskDone = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TypeIcon));
            OnPropertyChanged(nameof(AccentColor));
            OnPropertyChanged(nameof(IsStrikethrough));
            OnPropertyChanged(nameof(TitleOpacity));
            OnPropertyChanged(nameof(DoneLabel));
        }
    }

    public string DoneLabel        => IsTaskDone ? "완료" : "진행";
    public bool   IsStrikethrough  => IsTaskDone;
    public double TitleOpacity     => IsTaskDone ? 0.45 : 1.0;

    /// <summary>x:DataType 컴파일 바인딩용 SolidColorBrush 직접 노출.</summary>
    public IBrush  AccentBrush => HexToBrush(AccentColor);
    public IBrush  BadgeBrush  => HexToBrush(BadgeBackground);
    public bool    HasCategory => !string.IsNullOrEmpty(CategoryName);

    /// <summary>KEvent(ItemType="task") → AgendaItem.</summary>
    public static AgendaItem FromTask(KEvent taskEvent, string categoryName, string badgeColor, DateTime? displayDate = null) => new()
    {
        SourceEvent     = taskEvent,
        CategoryName    = categoryName,
        BadgeBackground = badgeColor,
        DisplayDate     = displayDate ?? taskEvent.Start.Date,
        _isTaskDone     = taskEvent.IsDone,
    };

    /// <summary>KEvent(event) → AgendaItem.</summary>
    public static AgendaItem FromEvent(KEvent ev, string calendarName, string calendarColor, DateTime? displayDate = null)
    {
        string hex = !string.IsNullOrEmpty(ev.ColorId)
            ? KEvent.ColorIdToHex(ev.ColorId)
            : calendarColor;
        if (string.IsNullOrEmpty(hex)) hex = calendarColor;

        return new AgendaItem
        {
            SourceEvent     = ev,
            CategoryName    = calendarName,
            BadgeBackground = hex,
            DisplayDate     = displayDate ?? ev.Start.Date,
            _accentHex      = hex,
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>HEX → SolidColorBrush. 잘못된 입력 시 회색.</summary>
    public static IBrush HexToBrush(string? hex)
    {
        try
        {
            string h = (hex ?? "#9E9E9E").TrimStart('#');
            if (h.Length == 6
                && byte.TryParse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
                && byte.TryParse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
                && byte.TryParse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        catch { }
        return Brushes.Gray;
    }
}

/// <summary>날짜 헤더(평탄 리스트 그룹핑용).</summary>
public sealed class AgendaHeader
{
    public string DateHeader { get; init; } = string.Empty;
    public string CountLabel { get; init; } = string.Empty;

    public static (AgendaHeader header, List<AgendaItem> items) Create(DateTime date, List<AgendaItem> items)
    {
        int dayDiff = (date.Date - DateTime.Today).Days;
        string rel = dayDiff switch
        {
            -2 => "그제",
            -1 => "어제",
             0 => "오늘",
             1 => "내일",
             2 => "모레",
             _ => string.Empty,
        };
        string dow     = date.ToString("ddd");
        string dateStr = date.ToString("M월 d일");
        string header  = string.IsNullOrEmpty(rel)
            ? $"{dateStr} ({dow})"
            : $"{rel} — {dateStr} ({dow})";

        int taskCnt  = items.Count(i => i.IsTask);
        int eventCnt = items.Count(i => i.IsEvent);
        var parts = new List<string>();
        if (taskCnt  > 0) parts.Add($"할 일 {taskCnt}");
        if (eventCnt > 0) parts.Add($"일정 {eventCnt}");
        string countLabel = parts.Count > 0 ? $"({string.Join(", ", parts)})" : string.Empty;

        return (new AgendaHeader { DateHeader = header, CountLabel = countLabel }, items);
    }
}
