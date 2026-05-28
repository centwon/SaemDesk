using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;
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
        {
            _vm.TodayItemClicked -= OpenTodayItemDialogAsync;
            _vm.AddLogRequested  -= OpenAddLogDialogAsync;
        }

        _vm = DataContext as LessonHomePageVM;

        if (_vm is not null)
        {
            _vm.TodayItemClicked += OpenTodayItemDialogAsync;
            _vm.AddLogRequested  += OpenAddLogDialogAsync;
        }
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

        if (_vm is null) return;

        try
        {
            // VM: 오늘의 수업 로드
            await _vm.LoadAllAsync();

            // 각 컨트롤 자체 로딩 (병렬)
            await Task.WhenAll(
                LoadTimetableAsync(),
                LoadLessonTasksAsync(),
                LoadLessonLogsAsync()
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
        try { await LessonLogList.LoadAsync(); }
        catch (Exception ex) { Debug.WriteLine($"[LessonHomePage] 수업 기록 로드 실패: {ex.Message}"); }
    }

    // ────────────────────────────────────────────────────
    //  오늘의 수업 버튼 클릭 (XAML Click 핸들러)
    // ────────────────────────────────────────────────────

    private async void TodayLessonItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TodayLessonItem item) return;
        await (_vm?.OnTodayItemClickedAsync(item) ?? Task.CompletedTask);
    }

    // ────────────────────────────────────────────────────
    //  VM 이벤트 핸들러 — LessonLogEditDialog 오픈
    // ────────────────────────────────────────────────────

    private async Task OpenTodayItemDialogAsync(TodayLessonItem item)
    {
        LessonLogEditDialog dialog = item.ExistingLog is not null
            ? new LessonLogEditDialog(item.ExistingLog)
            : LessonLogEditDialog.CreateNew(
                subject: item.Subject,
                room:    item.Lesson.Room,
                grade:   item.Lesson.Grade,
                cls:     item.Lesson.Class,
                period:  item.Lesson.Period);

        await DialogService.ShowAsync(dialog);

        if (dialog.Deleted || dialog.Result is not null)
        {
            await (_vm?.RefreshTodayAsync() ?? Task.CompletedTask);
            await LoadLessonLogsAsync();
        }
    }

    private async Task OpenAddLogDialogAsync(string defaultSubject)
    {
        var dialog = LessonLogEditDialog.CreateNew(subject: defaultSubject);
        await DialogService.ShowAsync(dialog);

        if (dialog.Result is not null)
        {
            await (_vm?.RefreshTodayAsync() ?? Task.CompletedTask);
            await LoadLessonLogsAsync();
        }
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
        {
            await LoadLessonLogsAsync();
            await (_vm?.RefreshTodayAsync() ?? Task.CompletedTask);
        }
    }

    private void LessonLogList_AddRequested(object? sender, EventArgs e)
        => _ = (_vm?.OnAddLogRequestedAsync() ?? Task.CompletedTask);
}
