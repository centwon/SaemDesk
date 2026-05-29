using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SaemDesk.Board;
using SaemDesk.Google;
using SaemDesk.Services;
using SaemDesk.Services.Platform;
using SaemDesk.Services.Platform.Windows;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views;
using SaemDesk.Views.Dialogs;
using SaemDesk.Views.Pages;

namespace SaemDesk;

public partial class App : Application
{
    // ────────────────────────────────────────────────────
    //  정적 서비스 — 플랫폼 구현체 (Phase 2)
    //  ViewModel/Service 레이어에서 App.FilePicker 등으로 접근.
    // ────────────────────────────────────────────────────

    /// <summary>Avalonia StorageProvider 기반 파일 선택기 (Windows)</summary>
    public static IFilePickerService FilePicker { get; } = new AvaloniaFilePickerService();

#pragma warning disable CA1416 // SaemDesk Phase 1-2: Windows-only 구현체
    /// <summary>Windows DPAPI 암호화 서비스</summary>
    public static ICryptoService Crypto { get; } = new WindowsDpApiCryptoService();

    /// <summary>Windows 한글 IME 전환 서비스</summary>
    public static IKoreanImeService KoreanIme { get; } = new WindowsKoreanImeService();
#pragma warning restore CA1416

    /// <summary>앱 수명 동안 유지되는 Google 동기화 서비스(자동 동기화용). 비활성 시 null.</summary>
    public static GoogleSyncService? GoogleSync { get; private set; }
    private static GoogleAuthService? _googleAuthForSync;
    private static GoogleCalendarApiClient? _googleApiForSync;

    // ────────────────────────────────────────────────────
    //  Avalonia 진입점
    // ────────────────────────────────────────────────────

    public override void Initialize()
    {
        RegisterViews();
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // 1. 앱 설정 로드
        Settings.Initialize();
        Debug.WriteLine($"[App] UserDataPath = {Settings.UserDataPath}");
        Debug.WriteLine($"[App] BoardDatabase.DbPath = {BoardDatabase.DbPath}");

        // 2. DB 스키마 초기화 — 3개 DB를 병렬로 초기화하여 시작 시간 단축.
        await Task.WhenAll(
            SchoolDatabase.InitAsync(),
            BoardDatabase.InitAsync(),
            SaemDesk.Scheduler.Scheduler.InitAsync());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 3. 초기 설정 여부 확인 — SchoolCode가 없으면 최초 실행
            if (string.IsNullOrEmpty(Settings.SchoolCode.Value))
            {
                Debug.WriteLine("[App] 초기 설정이 필요합니다.");

                var setupDialog = new InitialSetupDialog();
                setupDialog.Closed += (_, _) =>
                {
                    if (setupDialog.IsSuccess)
                    {
                        Debug.WriteLine("[App] 초기 설정 완료 — MainWindow 표시");
                        ShowMainWindow(desktop);
                    }
                    else
                    {
                        Debug.WriteLine("[App] 초기 설정 취소 — 앱 종료");
                        desktop.Shutdown();
                    }
                };

                // 임시 숨김 MainWindow (Avalonia는 MainWindow 없이 Window를 Show 불가)
                desktop.MainWindow = new Avalonia.Controls.Window { IsVisible = false };
                setupDialog.Show();
            }
            else
            {
                // 초기 설정 완료 — 바로 MainWindow 표시
                ShowMainWindow(desktop);
            }

            desktop.ShutdownRequested += (_, _) => StopGoogleAutoSync();
        }

        TryStartGoogleAutoSync();
        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var mainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        Debug.WriteLine("[App] 앱 시작 완료");
    }

    // ────────────────────────────────────────────────────
    //  Google Calendar 자동 동기화 — 앱 수명 동안 백그라운드 실행
    // ────────────────────────────────────────────────────

    /// <summary>
    /// 설정 변경(자동 동기화 토글/간격/연결 해제) 후 호출 — 기존 동기화를 중단하고 현재 설정에 맞게 재시작.
    /// </summary>
    public static void RestartGoogleAutoSync()
    {
        StopGoogleAutoSync();
        TryStartGoogleAutoSync();
    }

    private static void TryStartGoogleAutoSync()
    {
        try
        {
            if (!GoogleAuthService.HasCredentials) return;
            if (!Settings.GoogleAutoSync.Value) return;

            _googleAuthForSync = new GoogleAuthService();
            if (!_googleAuthForSync.IsAuthenticated)
            {
                _googleAuthForSync.Dispose();
                _googleAuthForSync = null;
                return;
            }

            _googleApiForSync = new GoogleCalendarApiClient(_googleAuthForSync);
            GoogleSync = new GoogleSyncService(_googleAuthForSync, _googleApiForSync);

            int minutes = Math.Max(5, Settings.GoogleSyncIntervalMinutes.Value);
            GoogleSync.StartPeriodicSync(TimeSpan.FromMinutes(minutes));
            Debug.WriteLine($"[App] Google 자동 동기화 시작: {minutes}분 간격");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Google 자동 동기화 시작 실패: {ex.Message}");
            StopGoogleAutoSync();
        }
    }

    private static void StopGoogleAutoSync()
    {
        try
        {
            GoogleSync?.Dispose();
            _googleAuthForSync?.Dispose();
        }
        catch { /* shutdown — 무시 */ }
        finally
        {
            GoogleSync = null;
            _googleApiForSync = null;
            _googleAuthForSync = null;
        }
    }

    // ViewLocator AOT 등록 — 새 ViewModel/View 쌍 추가 시 여기에 등록
    private static void RegisterViews()
    {
        // Shell
        ViewLocator.Register<MainWindowViewModel>(() => new MainWindow());

        // Pages (Phase 3 placeholder — Phase 5에서 실제 페이지로 교체)
        ViewLocator.Register<TodayPageVM>    (() => new TodayPage());
        ViewLocator.Register<StudentInfoPageVM>    (() => new StudentInfoPage());
        ViewLocator.Register<StudentsPageVM>       (() => new StudentsPage());
        ViewLocator.Register<LessonsPageVM>  (() => new LessonsPage());
        ViewLocator.Register<DiaryPageVM>    (() => new DiaryPage());
        ViewLocator.Register<BoardPageVM>    (() => new BoardPage());
        ViewLocator.Register<SchedulerPageVM>(() => new SchedulerPage());
        ViewLocator.Register<CalendarPageVM>     (() => new CalendarPage());
        ViewLocator.Register<CalendarHomePageVM> (() => new CalendarHomePage());
        ViewLocator.Register<SettingsPageVM> (() => new SettingsPage());
        ViewLocator.Register<HelpPageVM>         (() => new HelpPage());
        ViewLocator.Register<StudentSpecPageVM>  (() => new StudentSpecPage());
        ViewLocator.Register<StudentLogPageVM>   (() => new StudentLogPage());
        ViewLocator.Register<SeatsPageVM>        (() => new SeatsPage());
        ViewLocator.Register<AddStudentsPageVM>      (() => new AddStudentsPage());
        ViewLocator.Register<LessonHomePageVM>       (() => new LessonHomePage());
        ViewLocator.Register<LessonActivityPageVM>   (() => new LessonActivityPage());
        ViewLocator.Register<CourseManagementPageVM> (() => new CourseManagementPage());
        ViewLocator.Register<UnifiedExportPageVM>            (() => new UnifiedExportPage());
        ViewLocator.Register<StudentInfoExportPageVM>        (() => new StudentInfoExportPage());
        ViewLocator.Register<SchoolScheduleManagementPageVM> (() => new SchoolScheduleManagementPage());
        ViewLocator.Register<ClubHomePageVM>                 (() => new ClubHomePage());
        ViewLocator.Register<ClubManagementPageVM>           (() => new ClubManagementPage());
        ViewLocator.Register<ClubActivityPageVM>             (() => new ClubActivityPage());
        ViewLocator.Register<TeacherTimetablePageVM>         (() => new TeacherTimetablePage());
        ViewLocator.Register<ClassTimetablePageVM>           (() => new ClassTimetablePage());
        ViewLocator.Register<SchoolWorkPageVM>               (() => new SchoolWorkPage());
        ViewLocator.Register<LessonSpecPageVM>                (() => new LessonSpecPage());
    }
}
