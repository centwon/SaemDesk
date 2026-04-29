using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학생 누가기록 페이지 ViewModel.
/// 원본 NewSchool PageStudentLog 와 동등 — 학생 목록은 ListStudent 컨트롤이 직접 관리,
/// ViewModel 은 필터 상태(학년도/학기/학년/반/카테고리)와 로드 커맨드만 담당.
/// </summary>
public partial class StudentLogPageVM : ViewModelBase
{
    // ────────────────────────────────────────────────────
    //  필터 프로퍼티
    // ────────────────────────────────────────────────────

    [ObservableProperty] private int _workYear   = Settings.WorkYear.Value;
    [ObservableProperty] private int _semesterIndex = 0;   // 0=전체, 1=1학기, 2=2학기
    [ObservableProperty] private int _grade      = Settings.HomeGrade.Value;
    [ObservableProperty] private int _classNum   = Settings.HomeRoom.Value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Semester))]
    private LogCategory _selectedCategory = LogCategory.전체;

    /// <summary>실제 학기값 (0=전체, 1, 2)</summary>
    public int Semester => SemesterIndex; // index가 곧 값

    public ObservableCollection<LogCategory> Categories { get; } = new(
        Enum.GetValues<LogCategory>().Cast<LogCategory>());

    // ────────────────────────────────────────────────────
    //  커맨드
    // ────────────────────────────────────────────────────

    /// <summary>조회 버튼 — 학생 목록 로드 (실제 로드는 code-behind의 ListStudent)</summary>
    [RelayCommand]
    private Task LoadStudentsAsync() => Task.CompletedTask; // code-behind에서 처리
}
