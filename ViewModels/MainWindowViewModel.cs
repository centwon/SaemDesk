using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.ViewModels;

/// <summary>
/// 메인 윈도우 — 네비게이션 호스트.
/// CurrentPage 변경 시 ViewLocator가 대응하는 View를 렌더링합니다.
/// 트리형 NavItem 구조: 그룹(Factory=null + Children) / 리프(Factory + Children=null).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // ────────────────────────────────────────────────────
    //  네비게이션 항목 정의 (AOT-safe 람다 팩토리)
    // ────────────────────────────────────────────────────

    /// <summary>상단 트리 — NewSchool nav 구조에 대응.</summary>
    public static readonly ReadOnlyCollection<NavItem> AllNavItems = new(
    [
        // ── 단독 항목 ──
        new NavItem("오늘", "◈", () => new TodayPageVM()),
        new NavItem("달력", "◫", () => new CalendarHomePageVM()),

        // ── 학급 그룹 ──
        new NavItem("학급", "◉", new NavItem[]
        {
            new NavItem("학급 일지",       "▣", () => new DiaryPageVM()),
            new NavItem("학생",            "◉", () => new StudentsPageVM()),
            new NavItem("학생 추가",       "＋", () => new AddStudentsPageVM()),
            new NavItem("학생 누가기록",   "✏", () => new StudentLogPageVM()),
            new NavItem("학생부 특기사항", "★", () => new StudentSpecPageVM()),
            new NavItem("자리 배정",       "▦", () => new SeatsPageVM()),
            new NavItem("학급 게시판",     "✎", () => new BoardPageVM()),
        }),

        // ── 수업 그룹 ──
        new NavItem("수업", "◇", new NavItem[]
        {
            new NavItem("수업홈",      "🏠", () => new LessonHomePageVM()),
            new NavItem("주간 시간표", "📅", () => new LessonsPageVM()),
            new NavItem("수업 누가기록","✏", () => new LessonActivityPageVM()),
            new NavItem("수업 관리",   "◇", () => new CourseManagementPageVM()),
            new NavItem("스케줄러",    "◷", () => new SchedulerPageVM()),
        }),

        // ── 설정/관리 항목 ──
        new NavItem("학사일정 관리", "◷", () => new CalendarPageVM()),
        new NavItem("통합 내보내기", "📤", () => new UnifiedExportPageVM()),
    ]);

    public static readonly ReadOnlyCollection<NavItem> BottomNavItems = new(
    [
        new NavItem("도움말", "?", () => new HelpPageVM()),
        new NavItem("설정",   "⚙", () => new SettingsPageVM()),
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
        // 앱 시작 시 오늘 페이지로 (첫 번째 리프)
        SelectedNavItem = AllNavItems[0];
    }

    // ────────────────────────────────────────────────────
    //  네비게이션 — 선택 변경 시 CurrentPage 갱신
    // ────────────────────────────────────────────────────

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null) return;
        // 그룹 노드는 페이지 전환 없이 펼침/접힘만 (TreeView 가 자체적으로 처리).
        if (value.IsGroup) return;

        // 하단 항목 선택 해제
        _selectedBottomNavItem = null;
        OnPropertyChanged(nameof(SelectedBottomNavItem));
        // 페이지 전환
        CurrentPage = value.Factory!();
    }

    partial void OnSelectedBottomNavItemChanged(NavItem? value)
    {
        if (value is null) return;
        if (value.IsGroup) return;

        // 상단 항목 선택 해제
        _selectedNavItem = null;
        OnPropertyChanged(nameof(SelectedNavItem));
        // 페이지 전환
        CurrentPage = value.Factory!();
    }
}
