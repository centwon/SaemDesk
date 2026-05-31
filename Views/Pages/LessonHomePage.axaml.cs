using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Dialogs;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 수업 홈 페이지 code-behind.
/// - 데이터 로직 : LessonHomePageVM
/// - TimetableControl / LessonLogList / KAgendaControl / MemoBoard 로드 : 각 컨트롤 자체 API 호출
/// - 오늘의 수업 버튼 클릭 → LessonLogEditDialog (VM 이벤트 브릿지)
/// </summary>
public partial class LessonHomePage : UserControl
{
    private LessonHomePageVM? _vm;

    public LessonHomePage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded             += OnLoaded;
    }

    // ────────────────────────────────────────────────────
    //  DataContext 변경 — VM 이벤트 구독/해제
    // ────────────────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.AddLogRequested -= OpenAddLogDialogAsync;

        _vm = DataContext as LessonHomePageVM;

        if (_vm is not null)
            _vm.AddLogRequested += OpenAddLogDialogAsync;
    }

    // ────────────────────────────────────────────────────
    //  Loaded — 초기 데이터 로드
    // ────────────────────────────────────────────────────

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // LessonLogList 이벤트 연결
        LessonLogList.LessonSelected += LessonLogList_LessonSelected;
        LessonLogList.AddRequested   += LessonLogList_AddRequested;
        LessonLogList.ExportRequested += LessonLogList_ExportRequested;

        // 시간표 수업 클릭 → 수업기록
        Timetable.LessonClicked += Timetable_LessonClicked;

        if (_vm is null) return;

        try
        {
            // VM: 오늘의 수업 로드
            await _vm.LoadAllAsync();

            // 각 컨트롤 자체 로딩 (병렬)
            await Task.WhenAll(
                LoadTimetableAsync(),
                LoadLessonTasksAsync(),
                LessonLogList.InitFiltersAsync()
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LessonHomePage] OnLoaded 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  컨트롤 로딩
    // ────────────────────────────────────────────────────

    private async Task LoadTimetableAsync()
    {
        try { await Timetable.LoadTeacherScheduleAsync(Settings.User.Value, Settings.WorkYear.Value, Settings.WorkSemester.Value); }
        catch (Exception ex) { Debug.WriteLine($"[LessonHomePage] 시간표 로드 실패: {ex.Message}"); }
    }

    private async Task LoadLessonTasksAsync()
    {
        try { await LessonTaskList.LoadByDateRangeAsync(DateTime.Today, days: 14, showCompleted: false); }
        catch (Exception ex) { Debug.WriteLine($"[LessonHomePage] 수업 할일 로드 실패: {ex.Message}"); }
    }

    private async Task LoadLessonLogsAsync()
    {
        try { await LessonLogList.RefreshAsync(); }
        catch (Exception ex) { Debug.WriteLine($"[LessonHomePage] 수업 기록 로드 실패: {ex.Message}"); }
    }

    // ────────────────────────────────────────────────────
    //  시간표 수업 클릭 → 수업기록 (해당 주 요일 날짜, 기존 기록 있으면 편집)
    // ────────────────────────────────────────────────────

    private async void Timetable_LessonClicked(object? sender, TimetableItemViewModel item)
    {
        DateTime date = DateOfThisWeek(item.DayOfWeek);

        // 빈 셀(예기치 않은 수업): 날짜·교시만 채운 새 기록
        if (item.IsEmpty)
        {
            var blank = LessonLogEditDialog.CreateNew(period: item.Period, date: date);
            await DialogService.ShowAsync(blank);
            if (blank.Result is not null) await LoadLessonLogsAsync();
            return;
        }

        LessonLogEditDialog dialog;

        try
        {
            using var svc = new LessonLogService();
            var logs = await svc.GetByDateAsync(date);
            var existing = logs.FirstOrDefault(l =>
                l.Period == item.Period && l.Grade == item.Grade && l.Class == item.Class);

            dialog = existing is not null
                ? new LessonLogEditDialog(existing)
                : LessonLogEditDialog.CreateNew(
                    subject: item.SubjectName, room: item.Room,
                    grade: item.Grade, cls: item.Class, period: item.Period, date: date);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LessonHomePage] 기존 기록 조회 실패: {ex.Message}");
            dialog = LessonLogEditDialog.CreateNew(
                subject: item.SubjectName, room: item.Room,
                grade: item.Grade, cls: item.Class, period: item.Period, date: date);
        }

        await DialogService.ShowAsync(dialog);

        if (dialog.Deleted || dialog.Result is not null)
            await LoadLessonLogsAsync();
    }

    /// <summary>이번 주의 해당 요일(1=월~5=금) 날짜.</summary>
    private static DateTime DateOfThisWeek(int dayOfWeek)
    {
        var today = DateTime.Today;
        int todayDow = today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.DayOfWeek;
        return today.AddDays(dayOfWeek - todayDow).Date;
    }

    private async Task OpenAddLogDialogAsync(string defaultSubject)
    {
        var dialog = LessonLogEditDialog.CreateNew(subject: defaultSubject);
        await DialogService.ShowAsync(dialog);

        if (dialog.Result is not null)
            await LoadLessonLogsAsync();
    }

    // ────────────────────────────────────────────────────
    //  LessonLogList 이벤트
    // ────────────────────────────────────────────────────

    private void LessonLogList_LessonSelected(object? sender, LessonLog log)
        => _ = OpenExistingLogDialogAsync(log);

    private async Task OpenExistingLogDialogAsync(LessonLog log)
    {
        var dialog = new LessonLogEditDialog(log);
        await DialogService.ShowAsync(dialog);

        if (dialog.Deleted || dialog.Result is not null)
            await LoadLessonLogsAsync();
    }

    private void LessonLogList_AddRequested(object? sender, EventArgs e)
        => _ = (_vm?.OnAddLogRequestedAsync() ?? Task.CompletedTask);

    private async void LessonLogList_ExportRequested(object? sender, EventArgs e)
    {
        var dialog = new LessonExportDialog();
        await DialogService.ShowAsync(dialog);
    }
}
