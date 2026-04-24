using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
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
        ViewLocator.Register<SchedulerPageVM>(() => new SchedulerPage());
        ViewLocator.Register<CalendarPageVM> (() => new CalendarPage());
        ViewLocator.Register<SettingsPageVM> (() => new SettingsPage());
    }
}
