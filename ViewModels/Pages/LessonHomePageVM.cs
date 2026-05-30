using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 수업 홈 페이지 ViewModel.
/// - 페이지 헤더 날짜 + 과목 목록(로그리스트 "추가"의 기본 과목용)만 관리
/// - Timetable / LessonLogList / KAgendaControl / MemoBoard 로드는 code-behind 유지
/// - 수업기록 작성은 시간표 셀 클릭(code-behind)에서 처리
/// </summary>
public partial class LessonHomePageVM : ViewModelBase
{
    [ObservableProperty] private string _pageHeaderDate = string.Empty;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _errorText = string.Empty;

    /// <summary>LessonLogList AddRequested 시 빈 다이얼로그 열기 — code-behind에서 구독</summary>
    public event Func<string, Task>? AddLogRequested;

    private List<Course> _courses = [];

    public LessonHomePageVM()
    {
        PageHeaderDate = DateTime.Today.ToString("yyyy년 M월 d일 (ddd)");
    }

    /// <summary>페이지 로드 시 code-behind에서 호출 (과목 목록 로드).</summary>
    [RelayCommand]
    public async Task LoadAllAsync()
    {
        IsBusy    = true;
        ErrorText = string.Empty;
        try
        {
            using var svc = new CourseService();
            _courses = await svc.GetMyCoursesAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"데이터 로드 실패: {ex.Message}";
            Debug.WriteLine($"[LessonHomePageVM] {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task OnAddLogRequestedAsync()
    {
        string defaultSubject = _courses.Count > 0 ? _courses[0].Subject : string.Empty;
        if (AddLogRequested is not null)
            await AddLogRequested(defaultSubject);
    }
}
