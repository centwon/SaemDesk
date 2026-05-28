using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class CourseManagementPage : UserControl
{
    private CourseManagementPageVM VM => (CourseManagementPageVM)DataContext!;

    public CourseManagementPage()
    {
        InitializeComponent();
        DataContext = new CourseManagementPageVM();
        // YearSemesterPicker가 Loaded 후 YearSemesterChanged를 발화하면
        // OnYearSemesterChanged → VM.SetFilter → QueryAsync 자동 실행
    }

    private void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        VM.SetFilter(e.Year, e.Semester);
    }
}
