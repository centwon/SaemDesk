using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.ViewModels;

/// <summary>
/// 메인 윈도우 — 네비게이션 호스트.
/// NewSchool NavigationView 메뉴 구조와 동일하게 구성.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // ────────────────────────────────────────────────────
    //  네비게이션 항목 정의 (AOT-safe 람다 팩토리)
    //  NewSchool MainWindow.xaml NavView.MenuItems 와 1:1 대응
    // ────────────────────────────────────────────────────

    public static readonly ReadOnlyCollection<NavItem> AllNavItems = new(
    [
        // ── 홈 (단독) ──────────────────────────────────
        new NavItem("홈",   "🏠", () => new TodayPageVM()),

        // ── 달력 (단독) ────────────────────────────────
        new NavItem("달력", "📅", () => new CalendarHomePageVM()),

        // ── 학급 그룹 ──────────────────────────────────
        new NavItem("학급", "👥", new NavItem[]
        {
            new NavItem("학급 일지",       "📓", () => new DiaryPageVM()),
            new NavItem("학생 정보",       "👤", () => new StudentInfoPageVM()),
            new NavItem("학생 기록",       "✏",  () => new StudentLogPageVM()),
            new NavItem("학생부 관리",     "📋", () => new StudentSpecPageVM()),
            new NavItem("자리 배정",       "🪑", () => new SeatsPageVM()),
            new NavItem("학급 게시판",     "📌", () => new BoardPageVM()),
            new NavItem("학생정보 출력",   "🖨", () => new UnifiedExportPageVM()),
            new NavItem("통합 내보내기",   "📤", () => new UnifiedExportPageVM()),
            new NavItem("학급 시간표 관리","🗓", () => new TeacherTimetablePageVM()),
        }),

        // ── 수업 그룹 ──────────────────────────────────
        new NavItem("수업", "📚", new NavItem[]
        {
            new NavItem("수업홈",          "🏠", () => new LessonHomePageVM()),
            new NavItem("연간 수업 계획",  "📅", () => new AnnualLessonPlanPageVM()),
            new NavItem("진도 관리",       "📊", () => new ProgressMatrixPageVM()),
            new NavItem("누가 기록",       "✏",  () => new LessonActivityPageVM()),
            new NavItem("수업 시간표",     "🗓", () => new TeacherTimetablePageVM()),
            new NavItem("동아리 활동 기록","🎭", () => new ClubActivityPageVM()),
            new NavItem("수업 게시판",     "📌", () => new BoardPageVM()),
            new NavItem("동아리 관리",     "🎪", () => new ClubManagementPageVM()),
            new NavItem("수업 관리",       "📚", () => new CourseManagementPageVM()),
        }),

        // ── 업무 그룹 ──────────────────────────────────
        new NavItem("업무", "💼", new NavItem[]
        {
            new NavItem("업무 관리",   "💼", () => new SchoolWorkPageVM()),
            new NavItem("업무 게시판", "📌", () => new BoardPageVM()),
        }),

        // ── 아카이브 (단독) ────────────────────────────
        new NavItem("아카이브", "🗃", () => new HelpPageVM()),

        // ── 설정 그룹 ──────────────────────────────────
        new NavItem("설정", "⚙", new NavItem[]
        {
            new NavItem("학교 설정",     "🏫", () => new SettingsPageVM()),
            new NavItem("학사일정 관리", "🗓", () => new SchoolScheduleManagementPageVM()),
            new NavItem("학생 관리",     "👤", () => new StudentsPageVM()),
            new NavItem("앱 설정",       "⚙",  () => new SettingsPageVM()),
            new NavItem("도움말",        "❓", () => new HelpPageVM()),
            new NavItem("업데이트 확인", "🔄", () => new HelpPageVM()),
        }),
    ]);

    /// <summary>하단 고정 버튼은 제거 — 설정/도움말 모두 설정 그룹으로 통합됨.</summary>
    public static readonly ReadOnlyCollection<NavItem> BottomNavItems = new([]);

    // ────────────────────────────────────────────────────
    //  바인딩 프로퍼티
    // ────────────────────────────────────────────────────

    private readonly Dictionary<NavItem, ViewModelBase> _pageCache = new();

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private NavItem?       _selectedNavItem;
    [ObservableProperty] private NavItem?       _selectedBottomNavItem;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public MainWindowViewModel()
    {
        // 앱 시작 시 홈 페이지
        SelectedNavItem = AllNavItems[0];
    }

    // ────────────────────────────────────────────────────
    //  네비게이션 — 선택 변경 시 CurrentPage 갱신
    // ────────────────────────────────────────────────────

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null) return;
        if (value.IsGroup) return;

        _selectedBottomNavItem = null;
        OnPropertyChanged(nameof(SelectedBottomNavItem));
        CurrentPage = GetOrCreatePage(value);
    }

    partial void OnSelectedBottomNavItemChanged(NavItem? value)
    {
        if (value is null) return;
        if (value.IsGroup) return;

        _selectedNavItem = null;
        OnPropertyChanged(nameof(SelectedNavItem));
        CurrentPage = GetOrCreatePage(value);
    }

    private ViewModelBase GetOrCreatePage(NavItem item)
    {
        if (!_pageCache.TryGetValue(item, out var page))
        {
            page = item.Factory!();
            _pageCache[item] = page;
        }
        return page;
    }
}
