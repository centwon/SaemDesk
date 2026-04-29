using CommunityToolkit.Mvvm.ComponentModel;
using SaemDesk.Models;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학생 정보 페이지 ViewModel.
/// 원본 NewSchool PageStudentInfo — 필터 상태만 관리.
/// 실제 로드/저장은 StudentCard + LogListViewer 컨트롤과 code-behind 가 담당.
/// </summary>
public partial class StudentInfoPageVM : ViewModelBase
{
    [ObservableProperty] private int _workYear  = Settings.WorkYear.Value;
    [ObservableProperty] private int _grade     = Settings.HomeGrade.Value;
    [ObservableProperty] private int _classNum  = Settings.HomeRoom.Value;
}
