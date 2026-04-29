using CommunityToolkit.Mvvm.ComponentModel;
using SaemDesk.Models;
using System.Collections.ObjectModel;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학생부 특기사항 페이지 ViewModel.
/// 원본 NewSchool StudentSpecPage 와 동등 — 필터 상태(학년도/학년/반/카테고리)만 관리.
/// 실제 로드/저장은 SpecListViewer 컨트롤과 code-behind 에서 처리.
/// </summary>
public partial class StudentSpecPageVM : ViewModelBase
{
    [ObservableProperty] private int _workYear  = Settings.WorkYear.Value;
    [ObservableProperty] private int _grade     = Settings.HomeGrade.Value;
    [ObservableProperty] private int _classNum  = Settings.HomeRoom.Value;
    [ObservableProperty] private LogCategory _selectedCategory = LogCategory.전체;

    public ObservableCollection<LogCategory> Categories { get; } = new(new[]
    {
        LogCategory.전체,
        LogCategory.자율활동,
        LogCategory.진로활동,
        LogCategory.동아리활동,
        LogCategory.봉사활동,
        LogCategory.교과활동,
        LogCategory.개인별세특,
        LogCategory.종합의견,
    });
}
