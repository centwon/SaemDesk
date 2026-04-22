using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SaemDesk.ViewModels;
using SaemDesk.Views;

namespace SaemDesk;

public partial class App : Application
{
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

    // ViewLocator AOT 등록 — 새 페이지 추가 시 여기에 추가
    private static void RegisterViews()
    {
        ViewLocator.Register<MainWindowViewModel>(() => new MainWindow());
    }
}
