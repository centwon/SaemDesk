using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.ViewModels;

/// <summary>
/// 메인 윈도우 — 페이지 호스트.
/// 실제 메뉴 네비게이션은 MainWindow.axaml 의 Menu + MainWindow.axaml.cs Navigate() 가 처리한다.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // ────────────────────────────────────────────────────
    //  타이틀 바 정보 (앱 시작 시 1회 계산)
    // ────────────────────────────────────────────────────

    public string WindowTitle { get; } = BuildWindowTitle();

    private static string BuildWindowTitle()
    {
        int h = DateTime.Now.Hour;
        string name = Settings.UserName;
        string phrase = h < 12 ? "좋은 아침이에요" : h < 18 ? "좋은 오후예요" : "좋은 저녁이에요";
        string greeting = string.IsNullOrWhiteSpace(name) ? phrase : $"{name} 선생님, {phrase}";

        string date = $"{DateTime.Today:M월 d일} {DateTime.Today.DayOfWeek switch
        {
            DayOfWeek.Monday    => "월요일",
            DayOfWeek.Tuesday   => "화요일",
            DayOfWeek.Wednesday => "수요일",
            DayOfWeek.Thursday  => "목요일",
            DayOfWeek.Friday    => "금요일",
            DayOfWeek.Saturday  => "토요일",
            _                   => "일요일",
        }}";

        int grade = Settings.HomeGrade;
        int room  = Settings.HomeRoom;
        string teacher = grade > 0 && room > 0 ? $" · {grade}학년 {room}반 담임" : "";

        return $"{greeting} · {date}{teacher}";
    }

    // ────────────────────────────────────────────────────
    //  현재 페이지 (MainWindow.axaml TransitioningContentControl 바인딩)
    // ────────────────────────────────────────────────────

    [ObservableProperty] private ViewModelBase? _currentPage;

    public MainWindowViewModel()
    {
        // 앱 시작 시 홈 페이지
        CurrentPage = new TodayPageVM();
    }
}
