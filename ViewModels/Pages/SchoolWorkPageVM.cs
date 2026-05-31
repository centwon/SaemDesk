using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Collections;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Board.Models;
using SaemDesk.Board.Repositories;
using BoardDb = SaemDesk.Board.BoardDatabase;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 업무 관리 — 행정 업무 집계 대시보드.
/// 어젠다(업무 캘린더)·업무 메모·업무 게시판·다가오는 학사일정을 한 화면에 모은다.
/// 신규 모델 없이 기존 리포지토리만 재사용한다.
/// </summary>
public partial class SchoolWorkPageVM : ViewModelBase
{
    /// <summary>업무 캘린더 이름 — KAgendaControl/MemoBoard 의 고정 카테고리와 동일해야 한다.</summary>
    public const string WorkCalendarName = "업무";

    public OptimizedObservableCollection<Post>           WorkPosts         { get; } = new();
    public OptimizedObservableCollection<SchoolSchedule> UpcomingSchedules { get; } = new();

    // ── 요약 칩 ────────────────────────────────────────────
    [ObservableProperty] private int _todayTaskCount;        // 오늘 마감 미완료 할 일
    [ObservableProperty] private int _dueSoonCount;          // 3일 내 마감 미완료
    [ObservableProperty] private int _pendingCount;          // 미완료 전체
    [ObservableProperty] private int _upcomingScheduleCount; // 다가오는 학사일정(14일)

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public bool HasSchedules => UpcomingSchedules.Count > 0;

    public string TodayText => $"{DateTime.Today:yyyy년 M월 d일 dddd}";

    public SchoolWorkPageVM() { _ = ReloadAsync(); }

    /// <summary>전체 새로고침 — 요약 + 게시판 + 학사일정.</summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await RefreshSummaryAsync();
            await LoadPostsAsync();
            await LoadSchedulesAsync();
            StatusText = $"업무 게시글 {WorkPosts.Count}건 · 미완료 {PendingCount}건";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    /// <summary>할 일 요약 카운트만 재집계 — 어젠다에서 항목이 바뀌면 호출.</summary>
    public async Task RefreshSummaryAsync()
    {
        using var svc = Scheduler.Scheduler.CreateService();
        int calId = await svc.GetOrCreateCalendarIdAsync(WorkCalendarName);
        var tasks = await svc.GetTasksByCalendarIdAsync(calId);

        var today   = DateTime.Today;
        var dueEdge = today.AddDays(3);

        PendingCount   = tasks.Count(t => !t.IsDone);
        TodayTaskCount = tasks.Count(t => !t.IsDone && t.Start.Date == today);
        DueSoonCount   = tasks.Count(t => !t.IsDone && t.Start.Date >= today && t.Start.Date <= dueEdge);
    }

    private async Task LoadPostsAsync()
    {
        using var repo = new PostRepository(BoardDb.DbPath);
        var rows = await repo.GetByCategoryAsync(WorkCalendarName);
        WorkPosts.ReplaceAll(rows.OrderByDescending(x => x.DateTime));
    }

    /// <summary>업무 게시글 상세 보기 — 닫은 뒤 목록 갱신.</summary>
    [RelayCommand]
    private async Task OpenPostAsync(Post? post)
    {
        if (post is null) return;
        await SaemDesk.Services.DialogService.ShowPostDetailAsync(post.No);
        await LoadPostsAsync();
    }

    private async Task LoadSchedulesAsync()
    {
        var schoolCode = Settings.SchoolCode.Value;
        if (string.IsNullOrWhiteSpace(schoolCode))
        {
            UpcomingSchedules.Clear();
            UpcomingScheduleCount = 0;
            OnPropertyChanged(nameof(HasSchedules));
            return;
        }

        var today = DateTime.Today;
        using var repo = new SchoolScheduleRepository(SchoolDatabase.DbPath);
        var rows = await repo.GetByDateRangeAsync(schoolCode, today, today.AddDays(14));
        UpcomingSchedules.ReplaceAll(rows.Where(r => !r.IsHoliday));
        UpcomingScheduleCount = UpcomingSchedules.Count;
        OnPropertyChanged(nameof(HasSchedules));
    }
}
