using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SaemDesk.Views.Dialogs;
using System.Threading.Tasks;

namespace SaemDesk.Services;

/// <summary>
/// ContentDialog 대체. 모달 다이얼로그를 메인 윈도우 위에 띄운다.
/// </summary>
public static class DialogService
{
    public static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
        ?.MainWindow;

    public static async Task<TResult?> ShowAsync<TDialog, TResult>(TDialog dialog)
        where TDialog : Window
    {
        var owner = MainWindow;
        if (owner is null) return default;

        await dialog.ShowDialog(owner);

        return dialog is IDialogResult<TResult> result ? result.Result : default;
    }

    public static Task ShowAsync<TDialog>(TDialog dialog)
        where TDialog : Window
    {
        var owner = MainWindow;
        if (owner is null) return Task.CompletedTask;
        return dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message);
        var owner = MainWindow;
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }
}

/// <summary>
/// 결과값을 반환하는 다이얼로그가 구현하는 인터페이스.
/// </summary>
public interface IDialogResult<out T>
{
    T Result { get; }
}
