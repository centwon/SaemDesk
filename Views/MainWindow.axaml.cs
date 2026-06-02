using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel VM => (MainWindowViewModel)DataContext!;

    private Size _restoredSize;

    public MainWindow()
    {
        InitializeComponent();
        RestoreWindowState();
        Closing += (_, _) => SaveWindowState();
    }

    private void RestoreWindowState()
    {
        Width  = Settings.WindowWidth;
        Height = Settings.WindowHeight;
        _restoredSize = new Size(Width, Height);

        if (Settings.WindowX.Value >= 0 && Settings.WindowY.Value >= 0)
            Position = new PixelPoint(Settings.WindowX, Settings.WindowY);

        if (Settings.WindowIsMaximized.Value)
            WindowState = WindowState.Maximized;

        Topmost = Settings.TopMost.Value;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (WindowState == WindowState.Normal)
            _restoredSize = new Size(Width, Height);
    }

    private void SaveWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        Settings.WindowWidth.Set(maximized ? (int)_restoredSize.Width : (int)Width);
        Settings.WindowHeight.Set(maximized ? (int)_restoredSize.Height : (int)Height);
        Settings.WindowX.Set(Position.X);
        Settings.WindowY.Set(Position.Y);
        Settings.WindowIsMaximized.Set(maximized);
    }

    /// <summary>
    /// 메뉴 클릭 → Tag 기반 페이지 네비게이션.
    /// NewSchool MainWindow.xaml.cs NavView_ItemInvoked 의 switch(tag) 와 동일 구조.
    /// </summary>
    private void OnMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        string tag = item.Tag?.ToString() ?? "";
        Navigate(tag);
    }

    private void Navigate(string tag)
    {
        VM.CurrentPage = tag switch
        {
            // 홈 / 달력
            "Home"                      => new TodayPageVM(),
            "Calendar"                  => new CalendarHomePageVM(),

            // 학급
            "ClassDiary"                => new DiaryPageVM(),
            "StudentInfo"               => new StudentInfoPageVM(),
            "StudentLog"                => new StudentLogPageVM(),
            "StudentSpec"               => new StudentSpecPageVM(),
            "Seats"                     => new SeatsPageVM(),
            "ClassBoard"                => new BoardPageVM(new BoardPageParameter
            {
                Title               = "학급 게시판",
                FixedCategory       = CategoryNames.Homeroom,
                AllowCategoryChange = false,
                ShowSubjectFilter   = true,
            }),
            "StudentInfoExport"         => new StudentInfoExportPageVM(),
            "UnifiedExport"             => new UnifiedExportPageVM(),
            "Timetable_ClassManagement" => new ClassTimetablePageVM(),

            // 수업
            "LessonHome"                => new LessonHomePageVM(),
            "LessonActivity"            => new LessonActivityPageVM(),
            "LessonSpec"                => new LessonSpecPageVM(),
            "Timetable_Teacher"         => new TeacherTimetablePageVM(),
            "ClubActivity"              => new ClubActivityPageVM(),
            "LessonBoard"               => new BoardPageVM(new BoardPageParameter
            {
                Title               = "수업 게시판",
                FixedCategory       = CategoryNames.Lesson,
                AllowCategoryChange = false,
                ShowSubjectFilter   = true,
            }),
            "CourseManagement"          => new CourseManagementPageVM(),

            // 업무
            "SchoolWork"                => new SchoolWorkPageVM(),
            "WorkBoard"                 => new BoardPageVM(new BoardPageParameter
            {
                Title               = "업무 게시판",
                FixedCategory       = CategoryNames.Work,
                AllowCategoryChange = false,
                ShowSubjectFilter   = true,
            }),

            // 아카이브
            "Archive" => new BoardPageVM(new BoardPageParameter
            {
                Title               = "아카이브",
                AllowCategoryChange = true,
                ShowSubjectFilter   = true,
            }),

            // 설정
            "Settings_SchoolSchedule"   => new SchoolScheduleManagementPageVM(),
            "Settings_Student"          => new StudentsPageVM(),
            "Settings_App"              => new SettingsPageVM(),
            "Help"                      => new HelpPageVM(),
            "CheckUpdate"               => new HelpPageVM(),

            _                           => VM.CurrentPage,
        };
    }
}
