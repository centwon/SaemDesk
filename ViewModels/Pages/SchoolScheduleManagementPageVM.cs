using CommunityToolkit.Mvvm.ComponentModel;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학사일정 관리 페이지 VM — Phase 5 placeholder.
/// 본 이식: NEIS 학사일정 직접 편집/추가/삭제, 휴업일 토글, 학기 경계 설정.
/// (CalendarPage는 보기 전용 / 본 페이지는 편집 전용)
/// </summary>
public partial class SchoolScheduleManagementPageVM : ViewModelBase
{
    [ObservableProperty]
    private string _statusText = "Phase 5 학사일정 편집 페이지 이식 예정";
}
