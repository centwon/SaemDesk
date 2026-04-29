using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 수업 관리 페이지 — NewSchool CourseManagementPage MVP 포팅.
/// 학년도/학기 필터로 학교의 모든 Course 를 조회/추가/편집/삭제.
/// </summary>
public partial class CourseManagementPageVM : ViewModelBase
{
    private readonly List<Course> _all = new();

    public ObservableCollection<Course> Courses { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasCourses))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Course? _selectedCourse;

    [ObservableProperty] private decimal _workYear     = DateTime.Today.Year;
    [ObservableProperty] private int     _workSemester = 1;
    [ObservableProperty] private string  _searchText   = string.Empty;
    [ObservableProperty] private string  _typeFilter   = "전체";
    [ObservableProperty] private string  _statusText   = string.Empty;
    [ObservableProperty] private string  _errorText    = string.Empty;

    public ObservableCollection<string> TypeFilters { get; } = new(new[]
    {
        "전체", CourseTypes.Class, CourseTypes.Selective, CourseTypes.Club,
    });

    public bool HasCourses   => Courses.Count > 0;
    public bool IsEmpty      => !IsLoading && !HasCourses && string.IsNullOrEmpty(ErrorText);
    public bool HasSelection => SelectedCourse is not null;

    public CourseManagementPageVM()
    {
        WorkYear     = Settings.WorkYear.Value > 0 ? Settings.WorkYear.Value : DateTime.Today.Year;
        WorkSemester = Math.Max(1, Settings.WorkSemester.Value);
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
            int year = (int)WorkYear;
            int sem  = WorkSemester;
            if (string.IsNullOrEmpty(sc))
            {
                ErrorText = "학교 코드가 설정되어 있지 않습니다.";
                _all.Clear();
                ApplyFilter();
                return;
            }

            using var repo = new CourseRepository(SchoolDatabase.DbPath);
            var list = await repo.GetBySchoolAsync(sc, year, sem);
            _all.Clear();
            _all.AddRange(list);
            ApplyFilter();
            StatusText = $"총 {_all.Count}개 수업";
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

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnTypeFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Courses.Clear();
        IEnumerable<Course> filtered = _all;

        if (TypeFilter != "전체")
            filtered = filtered.Where(c => c.EffectiveType == TypeFilter);

        string keyword = SearchText ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(c =>
                (c.Subject?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (c.Rooms?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)   == true) ||
                (c.Remark?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)  == true));
        }

        foreach (var c in filtered.OrderBy(x => x.Grade).ThenBy(x => x.Subject))
            Courses.Add(c);

        OnPropertyChanged(nameof(HasCourses));
        OnPropertyChanged(nameof(IsEmpty));
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
            sc, Settings.UserName.Value, (int)WorkYear, WorkSemester);
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
