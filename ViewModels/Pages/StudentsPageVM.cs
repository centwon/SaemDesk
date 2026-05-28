using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Collections;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학생 관리 페이지 ViewModel.
/// 원본 NewSchool.Pages.StudentManagementPage 와 동등.
/// 필터(학년도/학년/반) → 조회 → 인라인 편집 표 → 저장/삭제.
/// </summary>
public partial class StudentsPageVM : ViewModelBase
{
    // ────────────────────────────────────────────────────
    //  필터 프로퍼티
    // ────────────────────────────────────────────────────

    [ObservableProperty] private int _filterYear  = Settings.WorkYear.Value;
    [ObservableProperty] private int _filterGrade = 0;   // 0 = 전체
    [ObservableProperty] private int _filterClass = 0;   // 0 = 전체

    // ────────────────────────────────────────────────────
    //  데이터
    // ────────────────────────────────────────────────────

    public OptimizedObservableCollection<StudentManagementViewModel> Students { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCount))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _isAllSelected;

    public int  SelectedCount => Students.Count(s => s.IsSelected);
    public bool HasSelection  => SelectedCount > 0;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool   _isBusy;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public StudentsPageVM() { }

    // ────────────────────────────────────────────────────
    //  조회
    // ────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadStudentsAsync()
    {
        if (FilterYear <= 0) return;

        IsBusy = true;
        StatusText = "조회 중…";

        try
        {
            using var svc = new EnrollmentService();

            var enrollments = FilterGrade == 0
                ? await svc.GetEnrollmentsAsync(
                    Settings.SchoolCode.Value, FilterYear, 0)
                : FilterClass == 0
                    ? await svc.GetEnrollmentsAsync(
                        Settings.SchoolCode.Value, FilterYear, 0, FilterGrade)
                    : await svc.GetClassRosterAsync(
                        Settings.SchoolCode.Value, FilterYear, FilterGrade, FilterClass);

            Students.ReplaceAll(enrollments
                .OrderBy(e => e.Grade).ThenBy(e => e.Class).ThenBy(e => e.Number)
                .Select(e => new StudentManagementViewModel
                {
                    EnrollmentNo = e.No,
                    StudentID    = e.StudentID,
                    Year         = e.Year,
                    Grade        = e.Grade,
                    Class        = e.Class,
                    Number       = e.Number,
                    Name         = e.Name,
                    Status       = e.Status,
                    IsSelected   = false,
                    IsModified   = false,
                }));

            StatusText = $"총 {Students.Count}명";
            IsAllSelected = false;
        }
        catch (Exception ex)
        {
            StatusText = "조회 실패";
            System.Diagnostics.Debug.WriteLine($"[StudentsPageVM] Load: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────
    //  전체 선택/해제
    // ────────────────────────────────────────────────────

    public void ToggleSelectAll(bool select)
    {
        foreach (var s in Students) s.IsSelected = select;
        NotifySelectionChanged();
    }

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    // ────────────────────────────────────────────────────
    //  저장
    // ────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SaveSelectedAsync()
    {
        var selected = Students.Where(s => s.IsSelected && s.IsModified).ToList();
        if (!selected.Any()) return;

        IsBusy = true;
        int ok = 0, fail = 0;

        try
        {
            using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
            foreach (var vm in selected)
            {
                try
                {
                    var e = await repo.GetByIdAsync(vm.EnrollmentNo);
                    if (e is null) { fail++; continue; }

                    e.Year  = vm.Year;
                    e.Grade = vm.Grade;
                    e.Class = vm.Class;
                    e.Number = vm.Number;
                    e.Name   = vm.Name;
                    e.UpdatedAt = DateTime.Now;

                    if (await repo.UpdateAsync(e)) { ok++; vm.IsModified = false; }
                    else fail++;
                }
                catch { fail++; }
            }

            StatusText = fail > 0 ? $"{ok}건 저장, {fail}건 실패" : $"{ok}건 저장 완료";
        }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────
    //  삭제
    // ────────────────────────────────────────────────────

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var selected = Students.Where(s => s.IsSelected).ToList();
        if (!selected.Any()) return;

        IsBusy = true;
        int ok = 0;

        try
        {
            using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
            foreach (var vm in selected)
            {
                try
                {
                    await repo.DeleteAsync(vm.EnrollmentNo);
                    Students.Remove(vm);
                    ok++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StudentsPageVM] Delete {vm.Name}: {ex.Message}");
                }
            }

            StatusText = $"{ok}명 삭제 완료 / 총 {Students.Count}명";
            NotifySelectionChanged();
        }
        finally { IsBusy = false; }
    }
}
