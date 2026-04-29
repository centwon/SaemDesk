using CommunityToolkit.Mvvm.ComponentModel;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 진도 관리 (격자) 페이지 VM — Phase 5 placeholder.
/// 본 이식: 수업 선택 → 차시×주차 격자, 격차 분석, 일괄 진도 입력.
/// </summary>
public partial class ProgressMatrixPageVM : ViewModelBase
{
    [ObservableProperty]
    private string _statusText = "Phase 5 진도 매트릭스 이식 예정";
}
