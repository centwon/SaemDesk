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
        string school = Settings.SchoolName;
        return string.IsNullOrWhiteSpace(school) ? "SaemDesk" : $"{school} · SaemDesk";
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
