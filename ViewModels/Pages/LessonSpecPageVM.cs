using CommunityToolkit.Mvvm.ComponentModel;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 수업용 학생부 관리 ViewModel.
/// 카테고리는 교과활동으로 고정, 과목/강의실 필터 사용.
/// </summary>
public partial class LessonSpecPageVM : ViewModelBase
{
    [ObservableProperty] private int _workYear = Settings.WorkYear.Value;
}
