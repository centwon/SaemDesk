using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>학생 관리 페이지.</summary>
public partial class StudentsPageVM : ViewModelBase
{
    // ── 내부 전체 목록 (검색 필터링용) ────────────────────
    private readonly List<StudentListItemViewModel> _allStudents = [];

    // ── 바인딩 속성 ───────────────────────────────────────
    public ObservableCollection<StudentListItemViewModel> Students { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStudents))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private StudentListItemViewModel? _selectedStudent;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;

    // ── 계산 속성 ─────────────────────────────────────────
    public bool HasStudents => Students.Count > 0;
    public bool IsEmpty     => !IsLoading && !HasStudents && string.IsNullOrEmpty(ErrorText);
    public bool HasSelection => SelectedStudent is not null;

    public string ClassHeaderText =>
        $"{Settings.HomeGrade}학년 {Settings.HomeRoom}반";

    public string SchoolYearText =>
        $"{Settings.WorkYear}학년도 {Settings.WorkSemester}학기";

    // ── 생성자 ────────────────────────────────────────────
    public StudentsPageVM()
    {
        _ = LoadStudentsAsync();
    }

    // ────────────────────────────────────────────────────
    //  목록 로드
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadStudentsAsync()
    {
        IsLoading  = true;
        ErrorText  = string.Empty;
        StatusText = "불러오는 중…";

        try
        {
            using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
            var enrollments = await repo.GetByClassAsync(
                Settings.SchoolCode.Value,
                Settings.WorkYear.Value,
                Settings.HomeGrade.Value,
                Settings.HomeRoom.Value);

            _allStudents.Clear();
            foreach (var e in enrollments)
            {
                _allStudents.Add(new StudentListItemViewModel
                {
                    EnrollmentNo = e.No,
                    StudentID    = e.StudentID,
                    SchoolCode   = e.SchoolCode,
                    Year         = e.Year,
                    Semester     = e.Semester,
                    Grade        = e.Grade,
                    Class        = e.Class,
                    Number       = e.Number,
                    Name         = e.Name,
                    Sex          = e.Sex,
                    Status       = e.Status,
                });
            }

            ApplyFilter(SearchText);
            StatusText = $"총 {_allStudents.Count}명";
        }
        catch (Exception ex)
        {
            StatusText = string.Empty;
            ErrorText  = "학생 목록을 불러오지 못했습니다.";
            System.Diagnostics.Debug.WriteLine($"[StudentsPageVM] 로드 오류: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasStudents));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    // ────────────────────────────────────────────────────
    //  학생 추가
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddStudentAsync()
    {
        bool saved = await DialogService.ShowStudentEditAsync(null);
        if (saved) await LoadStudentsAsync();
    }

    // ────────────────────────────────────────────────────
    //  학생 수정 (선택된 항목)
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task EditSelectedStudentAsync()
    {
        if (SelectedStudent is null) return;
        bool saved = await DialogService.ShowStudentEditAsync(SelectedStudent);
        if (saved) await LoadStudentsAsync();
    }

    // ────────────────────────────────────────────────────
    //  학생 상세 보기 (선택된 항목)
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ViewSelectedStudentDetailAsync()
    {
        if (SelectedStudent is null) return;
        await DialogService.ShowStudentDetailAsync(
            SelectedStudent.StudentID,
            SelectedStudent.Name);
    }

    // ────────────────────────────────────────────────────
    //  학생 삭제 (선택된 항목)
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteSelectedStudentAsync()
    {
        if (SelectedStudent is null) return;

        bool ok = await DialogService.ShowConfirmAsync(
            "학생 삭제",
            $"'{SelectedStudent.Name}' 학생을 삭제하시겠습니까?\n삭제된 학생은 복구할 수 없습니다.");
        if (!ok) return;

        try
        {
            using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(SelectedStudent.EnrollmentNo);
            SelectedStudent = null;
            await LoadStudentsAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[StudentsPageVM] 삭제 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  내보내기
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ExportClassAsync()
    {
        await DialogService.ShowExportAsync();
    }

    // ────────────────────────────────────────────────────
    //  검색 처리
    // ────────────────────────────────────────────────────

    partial void OnSearchTextChanged(string value) => ApplyFilter(value);

    private void ApplyFilter(string keyword)
    {
        Students.Clear();

        var source = string.IsNullOrWhiteSpace(keyword)
            ? _allStudents
            : _allStudents.FindAll(s =>
                s.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                s.Number.ToString().Contains(keyword));

        foreach (var s in source)
            Students.Add(s);

        OnPropertyChanged(nameof(HasStudents));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
