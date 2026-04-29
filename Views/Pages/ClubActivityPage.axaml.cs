using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views.Pages;

public partial class ClubActivityPage : UserControl
{
    public ClubActivityPage() { InitializeComponent(); }

    private void OnAddLogClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ClubActivityPageVM vm)
            vm.AddLogCommand.Execute(LogInput.Text);
        LogInput.Text = string.Empty;
    }

    private void OnLogInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnAddLogClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
