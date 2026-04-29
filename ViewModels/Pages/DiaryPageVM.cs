using CommunityToolkit.Mvvm.ComponentModel;
using SaemDesk.Models;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학급일지 페이지 ViewModel.
/// 원본 NewSchool ClassDiaryPage 와 동등 — 실제 데이터 로드/저장은
/// ClassDiaryBox 컨트롤과 code-behind가 담당하고, VM은 필터 상태만 관리.
/// </summary>
public partial class DiaryPageVM : ViewModelBase
{
    [ObservableProperty] private int _workYear = Settings.WorkYear.Value;
    [ObservableProperty] private int _grade    = Settings.HomeGrade.Value;
    [ObservableProperty] private int _classNum = Settings.HomeRoom.Value;
}
