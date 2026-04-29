using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SaemDesk.Google;
using SaemDesk.Services;
using SaemDesk.Services.Platform;
using SaemDesk.Services.Platform.Windows;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views;
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

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. 앱 설정 로드 (동기 — DB 경로 결정에 필요)
        Settings.Initialize();

        // 2. School DB 스키마 초기화 (fire-and-forget; CREATE TABLE IF NOT EXISTS → 항상 안전)
        //    첫 페이지 탐색 전에 완료되므로 타이밍 충돌 없음
        _ = SchoolDatabase.InitAsync();
        _ = BoardDatabase.InitAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            // 종료 시 Google 동기화 서비스 정리
            desktop.ShutdownRequested += (_, _) => StopGoogleAutoSync();
        }

        // Google Calendar 자동 동기화 시작 (인증 + 자동 동기화 활성 시에만)
        TryStartGoogleAutoSync();

        base.OnFrameworkInitializationCompleted();
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
        ViewLocator.Register<StudentsPageVM> (() => new StudentsPage());
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
    }
}
