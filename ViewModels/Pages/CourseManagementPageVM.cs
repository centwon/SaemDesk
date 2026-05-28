using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Collections;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 수업 관리 페이지 — NewSchool CourseManagementPage MVP 포팅.
/// 학년도/학기 필터로 학교의 모든 Course 를 조회/추가/편집/삭제.
/// </summary>
public partial class CourseManagementPageVM : ViewModelBase
{

    // 현재 필터 값 — YearSemesterPicker 이벤트로 주입
    public int FilterYear     { get; private set; } = Settings.WorkYear.Value > 0 ? Settings.WorkYear.Value : DateTime.Today.Year;
    public int FilterSemester { get; private set; } = Settings.WorkSemester.Value >= 1 ? Settings.WorkSemester.Value : 1;

    public OptimizedObservableCollection<Course> Courses { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasCourses))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Course? _selectedCourse;

    [ObservableProperty] private string _statusText  = string.Empty;
    [ObservableProperty] private string _errorText   = string.Empty;

    public bool HasCourses   => Courses.Count > 0;
    public bool IsEmpty      => !IsLoading && !HasCourses && string.IsNullOrEmpty(ErrorText);
    public bool HasSelection => SelectedCourse is not null;

    public CourseManagementPageVM() { }

    /// <summary>YearSemesterPicker 이벤트로 호출 — 학년도/학기 갱신 후 재조회.</summary>
    public void SetFilter(int year, int semester)
    {
        FilterYear     = year;
        FilterSemester = semester;
        _ = QueryAsync();
    }

    [RelayCommand]
    private async Task QueryAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;
        StatusText = "조회 중…";
        try
        {
            string sc = Settings.SchoolCode.Value;
            if (string.IsNullOrEmpty(sc))
            {
                ErrorText = "학교 코드가 설정되어 있지 않습니다.";
                Courses.Clear();
                return;
            }

            using var repo = new CourseRepository(SchoolDatabase.DbPath);
            var list = await repo.GetBySchoolAsync(sc, FilterYear, FilterSemester);

            Courses.ReplaceAll(list.OrderBy(x => x.Grade).ThenBy(x => x.Subject));

            StatusText = $"총 {list.Count}개 수업";
        }
        catch (Exception ex)
        {
            ErrorText = "수업을 불러오지 못했습니다.";
            Debug.WriteLine($"[CourseMgmtVM] {ex}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasCourses));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task EnrollStudentsAsync(Course? course)
    {
        if (course is null) return;
        await SaemDesk.Services.DialogService.ShowCourseEnrollmentAsync(course);
    }

    [RelayCommand]
    private async Task ScheduleCourseAsync(Course? course)
    {
        if (course is null) return;
        await SaemDesk.Services.DialogService.ShowCourseScheduleAsync(course);
    }

    /// <summary>카드 버튼 전용 — 특정 Course 직접 수정.</summary>
    [RelayCommand]
    private async Task EditItemAsync(Course? course)
    {
        if (course is null) return;
        var saved = await SaemDesk.Services.DialogService.ShowCourseEditAsync(
            course.SchoolCode, course.TeacherID, course.Year, course.Semester, course);
        if (saved is not null) await QueryAsync();
    }

    /// <summary>카드 버튼 전용 — 특정 Course 직접 삭제.</summary>
    [RelayCommand]
    private async Task DeleteItemAsync(Course? course)
    {
        if (course is null) return;
        bool ok = await SaemDesk.Services.DialogService.ShowConfirmAsync(
            "수업 삭제",
            $"'{course.Subject}' 수업을 삭제하시겠습니까?\n관련 차시·진도·수강 정보가 함께 영향을 받을 수 있습니다.");
        if (!ok) return;
        try
        {
            using var repo = new CourseRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(course.No);
            if (SelectedCourse?.No == course.No) SelectedCourse = null;
            await QueryAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddCourseAsync()
    {
        string sc = Settings.SchoolCode.Value;
        if (string.IsNullOrEmpty(sc))
        {
            ErrorText = "학교 코드가 설정되어 있지 않습니다.";
            return;
        }
        var saved = await SaemDesk.Services.DialogService.ShowCourseEditAsync(
            sc, Settings.User.Value, FilterYear, FilterSemester);
        if (saved is not null) await QueryAsync();
    }

    [RelayCommand]
    private async Task EditSelectedAsync()
    {
        if (SelectedCourse is null) return;
        var saved = await SaemDesk.Services.DialogService.ShowCourseEditAsync(
            SelectedCourse.SchoolCode, SelectedCourse.TeacherID, SelectedCourse.Year, SelectedCourse.Semester,
            SelectedCourse);
        if (saved is not null) await QueryAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedCourse is null) return;
        bool ok = await SaemDesk.Services.DialogService.ShowConfirmAsync(
            "수업 삭제",
            $"'{SelectedCourse.Subject}' 수업을 삭제하시겠습니까?\n관련 차시·진도·수강 정보가 함께 영향을 받을 수 있습니다.");
        if (!ok) return;

        try
        {
            using var repo = new CourseRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(SelectedCourse.No);
            SelectedCourse = null;
            await QueryAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
        }
    }
}
