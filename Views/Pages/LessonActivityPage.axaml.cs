using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 수업 활동 기록 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.LessonActivityPage (WinUI3).
/// 구성: 수업/강의실 선택 + 좌측 ListStudent + 우측 LogListViewer.
/// </summary>
public partial class LessonActivityPage : UserControl, IDisposable
{
    private bool _disposed;
    private LessonActivityPageVM VM => (LessonActivityPageVM)DataContext!;

    private Enrollment? _selectedStudent;

    public LessonActivityPage()
    {
        InitializeComponent();
        DataContext = new LessonActivityPageVM();

        StudentList.StudentSelected += OnStudentSelected;
        LogList.StudentInfoMode = StudentInfoMode.HideAll;
        LogList.LogEdited       += (_, _) => _ = LoadLogsAsync();

        Unloaded += (_, _) => Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StudentList.StudentSelected -= OnStudentSelected;
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────
    //  수업 / 강의실 변경
    // ────────────────────────────────────────────────────

    private async void OnCourseChanged(object? sender, SelectionChangedEventArgs e)
        => await LoadStudentsAsync();

    private async void OnRoomChanged(object? sender, SelectionChangedEventArgs e)
        => await LoadStudentsAsync();

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private async Task LoadStudentsAsync()
    {
        var course = VM.SelectedCourse;
        if (course is null) return;

        try
        {
            using var courseSvc = new CourseService();
            var enrollments = await courseSvc.GetCourseEnrollmentsAsync(
                Settings.SchoolCode.Value, Settings.WorkYear.Value, Settings.WorkSemester.Value, course.No);

            var ids = enrollments.Select(ce => ce.StudentID).ToHashSet();

            using var enrollSvc = new EnrollmentService();
            var all = await enrollSvc.GetEnrollmentsAsync(Settings.SchoolCode.Value, Settings.WorkYear.Value);
            var list = all.Where(e => ids.Contains(e.StudentID))
                          .OrderBy(e => e.Class).ThenBy(e => e.Number).ToList();

            StudentList.LoadStudents(list);
            TxtStudentCount.Text = $"{list.Count}명";
            _selectedStudent = null;
            LogList.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LessonActivityPage] LoadStudents: {ex.Message}");
        }
    }

    private async void OnStudentSelected(object? sender, Enrollment student)
    {
        _selectedStudent = student;
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        if (_selectedStudent is null || VM.SelectedCourse is null) return;

        try
        {
            using var svc = new StudentLogService();
            var logs = await svc.GetStudentLogsAsync(
                _selectedStudent.StudentID, Settings.WorkYear.Value);

            var vms = logs.Select(l => new StudentLogViewModel(l)).ToList();
            LogList.LoadLogs(vms);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LessonActivityPage] LoadLogs: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  버튼
    // ────────────────────────────────────────────────────

    private async void BtnBatchInput_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: 일괄입력 다이얼로그
        await Task.CompletedTask;
    }

    private async void BtnAddLog_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedStudent is null) return;
        var log = new StudentLog
        {
            StudentID = _selectedStudent.StudentID,
            Year      = Settings.WorkYear.Value,
            Semester  = Settings.WorkSemester.Value,
            TeacherID = Settings.User.Value,
            Date      = DateTime.Now,
        };
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;
        var dlg = new Views.Dialogs.StudentLogEditDialog(log.StudentID, _selectedStudent?.Name ?? "", null);
        await dlg.ShowDialog(owner);
        if (dlg.Result != null) await LoadLogsAsync();
    }

    private async void BtnEditLog_Click(object? sender, RoutedEventArgs e)
        => await LogList.EditSelectedLogAsync();

    private async void BtnSaveLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.SaveChangedLogsAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LessonActivityPage] Save: {ex.Message}"); }
    }

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
        => _ = LoadStudentsAsync();

    private async void BtnDeleteLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.DeleteSelectedLogsAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LessonActivityPage] Delete: {ex.Message}"); }
    }
}
