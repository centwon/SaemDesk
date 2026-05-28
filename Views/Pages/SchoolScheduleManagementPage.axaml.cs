using Avalonia.Controls;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class SchoolScheduleManagementPage : UserControl
{
    public SchoolScheduleManagementPage()
    {
        InitializeComponent();
        DataContext = new SchoolScheduleManagementPageVM();
        YearPicker.YearSemesterChanged += OnYearSemesterChanged;
    }

    private void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        if (DataContext is SchoolScheduleManagementPageVM vm)
            vm.SetYear(e.Year);
    }
}
