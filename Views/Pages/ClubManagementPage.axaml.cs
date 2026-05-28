using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class ClubManagementPage : UserControl
{
    private ClubManagementPageVM VM => (ClubManagementPageVM)DataContext!;

    public ClubManagementPage()
    {
        InitializeComponent();
        DataContext = new ClubManagementPageVM();
    }

    private void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        VM.SetFilter(e.Year);
    }

    private void BtnBackToActivity_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Views.MainWindow mw
            && mw.DataContext is ViewModels.MainWindowViewModel mainVm)
        {
            mainVm.CurrentPage = new ClubActivityPageVM();
        }
    }
}
