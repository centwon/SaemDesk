using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 수업홈 페이지 — NewSchool LessonHomePage MVP 포팅.
/// 내 수업 목록 + 최근 수업 기록 + 수업 카테고리 KAgendaControl(코드비하인드 임베드).
/// </summary>
public partial class LessonHomePageVM : ViewModelBase
{
    public ObservableCollection<Course>     MyCourses     { get; } = new();
    public ObservableCollection<LessonLog>  RecentLessons { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCourses))]
    [NotifyPropertyChangedFor(nameof(HasRecent))]
    private bool _isLoading;

    [ObservableProperty] private string _statusText  = string.Empty;
    [ObservableProperty] private string _errorText   = string.Empty;

    public bool HasCourses => MyCourses.Count > 0;
    public bool HasRecent  => RecentLessons.Count > 0;

    public string SchoolYearText
    {
        get
        {
            int y = Settings.WorkYear.Value;
            int s = Settings.WorkSemester.Value;
            return (y <= 0) ? "학년도 미설정" : $"{y}학년도 {s}학기";
        }
    }

    public LessonHomePageVM()
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;
        StatusText = "로드 중…";
        try
        {
            string sc = Settings.SchoolCode.Value;
            int year   = Settings.WorkYear.Value;
            int sem    = Settings.WorkSemester.Value;
            string tid = Settings.UserName.Value; // 교사 ID 필드가 따로 없으면 교사명을 키로 사용

            // 1) 내 수업
            MyCourses.Clear();
            if (!string.IsNullOrEmpty(sc) && year > 0)
            {
                using var courseSvc = new CourseService();
                var list = await courseSvc.GetMyCoursesAsync();
                foreach (var c in list.OrderBy(x => x.Subject)) MyCourses.Add(c);
            }

            // 2) 최근 수업 기록 — 내가 가르친 차시 중 최근 20건
            RecentLessons.Clear();
            try
            {
                using var logSvc = new LessonLogService();
                var logs = await logSvc.GetMyLessonsAsync(sem);
                foreach (var l in logs.OrderByDescending(x => x.Date).Take(20))
                    RecentLessons.Add(l);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LessonHomeVM] 최근 수업 로드 실패: {ex.Message}");
            }

            StatusText = $"내 수업 {MyCourses.Count}개 · 최근 기록 {RecentLessons.Count}건 — {SchoolYearText}";
        }
        catch (Exception ex)
        {
            ErrorText = "수업 데이터를 불러오지 못했습니다.";
            Debug.WriteLine($"[LessonHomeVM] {ex}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasCourses));
            OnPropertyChanged(nameof(HasRecent));
        }
    }
}
