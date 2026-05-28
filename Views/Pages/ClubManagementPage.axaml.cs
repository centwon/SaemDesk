using Avalonia.Controls;
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
        // YearSemesterPicker가 Loaded 후 YearSemesterChanged를 발화하면
        // OnYearSemesterChanged → VM.SetFilter → QueryAsync 자동 실행
    }

    private void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        VM.SetFilter(e.Year);
    }
}
