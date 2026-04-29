using CommunityToolkit.Mvvm.ComponentModel;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학생 관리 페이지용 행 ViewModel.
/// NewSchool.Pages.StudentManagementViewModel 과 동등.
/// </summary>
public partial class StudentManagementViewModel : ViewModelBase
{
    [ObservableProperty] private int    _enrollmentNo;
    [ObservableProperty] private string _studentID  = string.Empty;
    [ObservableProperty] private int    _year;
    [ObservableProperty] private int    _grade;
    [ObservableProperty] private int    _class;
    [ObservableProperty] private int    _number;
    [ObservableProperty] private string _name       = string.Empty;
    [ObservableProperty] private string _status     = string.Empty;
    [ObservableProperty] private string _memo       = string.Empty;
    [ObservableProperty] private bool   _isSelected;
    [ObservableProperty] private bool   _isModified;

    public string ClassInfo => $"{Year}학년도 {Grade}학년 {Class}반 {Number}번";
}
