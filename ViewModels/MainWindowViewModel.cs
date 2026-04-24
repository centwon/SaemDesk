using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.ViewModels;

/// <summary>
/// 메인 윈도우 — 네비게이션 호스트.
/// CurrentPage 변경 시 ViewLocator가 대응하는 View를 렌더링합니다.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // ────────────────────────────────────────────────────
    //  네비게이션 항목 정의 (AOT-safe 람다 팩토리)
    // ────────────────────────────────────────────────────

    public static readonly ReadOnlyCollection<NavItem> AllNavItems = new(
    [
        new NavItem("오늘",     "◈", () => new TodayPageVM()),
        new NavItem("학생",     "◉", () => new StudentsPageVM()),
        new NavItem("수업",     "◇", () => new LessonsPageVM()),
        new NavItem("학급일지", "▣", () => new DiaryPageVM()),
        new NavItem("스케줄러", "◷", () => new SchedulerPageVM()),
        new NavItem("학사일정", "◫", () => new CalendarPageVM()),
    ]);

    public static readonly ReadOnlyCollection<NavItem> BottomNavItems = new(
    [
        new NavItem("설정", "⚙", () => new SettingsPageVM()),
    ]);

    // ────────────────────────────────────────────────────
    //  바인딩 프로퍼티
    // ────────────────────────────────────────────────────

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private NavItem? _selectedBottomNavItem;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public MainWindowViewModel()
    {
        // 앱 시작 시 오늘 페이지로
        SelectedNavItem = AllNavItems[0];
    }

    // ────────────────────────────────────────────────────
    //  네비게이션 — 선택 변경 시 CurrentPage 갱신
    // ────────────────────────────────────────────────────

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null) return;
        // 하단 항목 선택 해제
        _selectedBottomNavItem = null;
        OnPropertyChanged(nameof(SelectedBottomNavItem));
        // 페이지 전환
        CurrentPage = value.Factory();
    }

    partial void OnSelectedBottomNavItemChanged(NavItem? value)
    {
        if (value is null) return;
        // 상단 항목 선택 해제
        _selectedNavItem = null;
        OnPropertyChanged(nameof(SelectedNavItem));
        // 페이지 전환
        CurrentPage = value.Factory();
    }
}
