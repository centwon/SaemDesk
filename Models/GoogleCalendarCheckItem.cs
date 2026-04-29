using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SaemDesk.Models;

/// <summary>
/// Google Calendar 연동 다이얼로그의 캘린더 체크박스 항목.
/// (로컬 KCalendarList 행 1개 = 1 항목)
/// </summary>
public class GoogleCalendarCheckItem : INotifyPropertyChanged
{
    private bool _isChecked;
    private string _title = string.Empty;
    private string _googleId = string.Empty;

    /// <summary>로컬 KCalendarList.No</summary>
    public int CalendarNo { get; set; }

    /// <summary>표시 이름 (수업/학급/업무/개인 등)</summary>
    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    /// <summary>매핑된 Google Calendar ID (없으면 빈 문자열)</summary>
    public string GoogleId
    {
        get => _googleId;
        set { if (_googleId != value) { _googleId = value; OnPropertyChanged(); } }
    }

    /// <summary>양방향 동기화 사용 여부 (체크 시 SyncMode=TwoWay, 해제 시 None)</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set { if (_isChecked != value) { _isChecked = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
