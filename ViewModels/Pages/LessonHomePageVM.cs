using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Collections;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 수업 홈 페이지 ViewModel (방향 B)
/// - 오늘의 수업 목록 + 요약 텍스트는 VM이 관리
/// - TimetableControl, LessonLogList, KAgendaControl, MemoBoard 로드는 code-behind 유지
///   (컨트롤별 자체 로딩 API가 있으므로)
/// </summary>
public partial class LessonHomePageVM : ViewModelBase
{
    // ────────────────────────────────────────────────────
    //  오늘의 수업
    // ────────────────────────────────────────────────────

    public OptimizedObservableCollection<TodayLessonItem> TodayLessons { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTodayLessons))]
    private int _todayTotal;

    [ObservableProperty] private int    _todayCompleted;
    [ObservableProperty] private string _todaySummaryText = string.Empty;
    [ObservableProperty] private bool   _isNoLessons;
    [ObservableProperty] private string _noLessonsText  = "오늘은 수업이 없습니다.";
    [ObservableProperty] private string _pageHeaderDate = string.Empty;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _errorText = string.Empty;

    public bool HasTodayLessons => TodayTotal > 0;

    // ────────────────────────────────────────────────────
    //  내부 상태 (LessonLogList/Dialog 연동용)
    // ────────────────────────────────────────────────────

    /// <summary>오늘의 수업 클릭 시 LessonLogEditDialog 열기 — code-behind에서 구독</summary>
    public event Func<TodayLessonItem, Task>? TodayItemClicked;

    /// <summary>LessonLogList AddRequested 시 빈 다이얼로그 열기 — code-behind에서 구독</summary>
    public event Func<string, Task>? AddLogRequested;

    private List<Course> _courses = [];

    // ────────────────────────────────────────────────────
    //  초기화
    // ────────────────────────────────────────────────────

    public LessonHomePageVM()
    {
        PageHeaderDate = DateTime.Today.ToString("yyyy년 M월 d일 (ddd)");
    }

    // ────────────────────────────────────────────────────
    //  커맨드
    // ────────────────────────────────────────────────────

    /// <summary>페이지 로드 시 code-behind에서 호출</summary>
    [RelayCommand]
    public async Task LoadAllAsync()
    {
        IsBusy    = true;
        ErrorText = string.Empty;
        try
        {
            await LoadCoursesAsync();
            await LoadTodayLessonsAsync();
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

    // ────────────────────────────────────────────────────
    //  오늘의 수업 로드
    // ────────────────────────────────────────────────────

    private async Task LoadCoursesAsync()
    {
        using var svc = new CourseService();
        _courses = await svc.GetMyCoursesAsync();
    }

    public async Task LoadTodayLessonsAsync()
    {
        try
        {
            // 1. 시간표 기반 오늘 수업
            using var lessonSvc = new LessonService();
            var todayLessons = await lessonSvc.GetTodayLessonsAsync();

            // 2. 과목 dict
            var courseDict = _courses.ToDictionary(c => c.No, c => c);

            // 3. 오늘 이미 작성된 기록
            using var logSvc = new LessonLogService();
            var todayLogs = await logSvc.GetTodayLessonsAsync();

            // 4. 현재 교시
            int currentPeriod = LessonLogService.GetCurrentPeriod();

            // 5. TodayLessonItem 빌드
            var items = new List<TodayLessonItem>();
            foreach (var lesson in todayLessons.OrderBy(l => l.Period))
            {
                if (lesson.IsCancelled) continue;

                string subject = courseDict.TryGetValue(lesson.Course, out var course)
                    ? course.Subject : string.Empty;

                var matchedLog = todayLogs.FirstOrDefault(log =>
                    log.Period == lesson.Period &&
                    log.Grade  == lesson.Grade  &&
                    log.Class  == lesson.Class);

                items.Add(new TodayLessonItem(lesson, subject, lesson.Course, matchedLog, currentPeriod));
            }
            TodayLessons.ReplaceAll(items);

            // 6. 요약 갱신
            TodayTotal     = TodayLessons.Count;
            TodayCompleted = TodayLessons.Count(i => i.IsCompleted);

            TodaySummaryText = TodayTotal > 0
                ? $"{TodayTotal}시간 중 {TodayCompleted}건 기록"
                : string.Empty;

            IsNoLessons  = TodayTotal == 0;
            NoLessonsText = "오늘은 수업이 없습니다.";

            Debug.WriteLine($"[LessonHomePageVM] 오늘의 수업: {TodayTotal}건, 기록: {TodayCompleted}건");
        }
        catch (Exception ex)
        {
            NoLessonsText = "수업 정보를 불러올 수 없습니다.";
            IsNoLessons   = true;
            Debug.WriteLine($"[LessonHomePageVM] 오늘의 수업 로드 실패: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  code-behind 브릿지 메서드
    // ────────────────────────────────────────────────────

    public async Task OnTodayItemClickedAsync(TodayLessonItem item)
    {
        if (TodayItemClicked is not null)
            await TodayItemClicked(item);
    }

    public async Task OnAddLogRequestedAsync()
    {
        string defaultSubject = _courses.Count > 0 ? _courses[0].Subject : string.Empty;
        if (AddLogRequested is not null)
            await AddLogRequested(defaultSubject);
    }

    /// <summary>수업 기록 추가/편집 후 오늘의 수업 갱신</summary>
    public async Task RefreshTodayAsync() => await LoadTodayLessonsAsync();
}

// ════════════════════════════════════════════════════════
//  TodayLessonItem — 오늘의 수업 행 바인딩 모델
//  WinUI Brush 직접 반환 → bool 상태 프로퍼티 + XAML Classes 방식으로 개선
// ════════════════════════════════════════════════════════

/// <summary>
/// 오늘의 수업 한 행 (XAML ItemsControl 바인딩용).
/// 원본(NewSchool)에서 WinUI Brush/FontWeight/Visibility를 직접 반환하던 구조를
/// Avalonia CompiledBinding + AOT 안전 방식(bool 상태)으로 변환.
/// </summary>
public sealed class TodayLessonItem
{
    // ── 원본 데이터
    public Lesson     Lesson      { get; }
    public string     Subject     { get; }
    public int        CourseNo    { get; }
    public LessonLog? ExistingLog { get; }
    public int        CurrentPeriod { get; }

    // ── 핵심 상태 (XAML Classes 바인딩)
    public bool IsCompleted => ExistingLog is not null;
    public bool IsCurrent   => !IsCompleted && Lesson.Period == CurrentPeriod;
    public bool IsPending   => !IsCompleted && !IsCurrent;

    // ── 표시용 텍스트
    public string PeriodText   => $"{Lesson.Period}교시";
    public string ClassDisplay => Lesson.ClassDisplay;
    public string TopicText    => ExistingLog?.Topic ?? string.Empty;
    public bool   HasTopic     => IsCompleted && !string.IsNullOrWhiteSpace(ExistingLog?.Topic);
    public string StatusText   => IsCompleted ? "완료" : IsCurrent ? "기록" : "예정";

    public TodayLessonItem(Lesson lesson, string subject, int courseNo,
                           LessonLog? existingLog, int currentPeriod)
    {
        Lesson        = lesson;
        Subject       = subject;
        CourseNo      = courseNo;
        ExistingLog   = existingLog;
        CurrentPeriod = currentPeriod;
    }
}
