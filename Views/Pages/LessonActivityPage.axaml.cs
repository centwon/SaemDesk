using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class LessonActivityPage : UserControl, IDisposable
{
    private bool _disposed;
    private LessonActivityPageVM VM => (LessonActivityPageVM)DataContext!;

    private Enrollment? _selectedStudent;
    private Course?     _currentCourse;

    public LessonActivityPage()
    {
        InitializeComponent();
        DataContext = new LessonActivityPageVM();

        CoursePicker.CourseChanged  += OnCoursePickerChanged;
        StudentList.StudentSelected += OnStudentSelected;

        LogList.StudentInfoMode = StudentInfoMode.HideAll;
        LogList.Category        = LogCategory.교과활동;
        LogList.LogEdited       += (_, _) => _ = LoadLogsAsync();

        SldFontSize.Value = 12;

        Unloaded += (_, _) => Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CoursePicker.CourseChanged  -= OnCoursePickerChanged;
        StudentList.StudentSelected -= OnStudentSelected;
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────
    //  CoursePicker 변경
    // ────────────────────────────────────────────────────

    private void OnCoursePickerChanged(object? sender, CourseChangedEventArgs e)
    {
        _currentCourse   = e.Course;
        _selectedStudent = null;
        LogList.Clear();

        StudentList.LoadStudents(e.Students);
    }

    // ────────────────────────────────────────────────────
    //  학생 선택
    // ────────────────────────────────────────────────────

    private async void OnStudentSelected(object? sender, Enrollment student)
    {
        _selectedStudent = student;
        await LoadLogsAsync();
    }

    // ────────────────────────────────────────────────────
    //  로그 로드 — 해당 과목(CourseNo) 기록만 필터링
    // ────────────────────────────────────────────────────

    private async Task LoadLogsAsync()
    {
        if (_selectedStudent is null || _currentCourse is null) return;

        try
        {
            using var svc = new StudentLogService();
            var logs = await svc.GetStudentLogsAsync(
                _selectedStudent.StudentID, Settings.WorkYear.Value);

            var filtered = logs
                .Where(l => l.CourseNo == _currentCourse.No)
                .OrderByDescending(l => l.Date)
                .ToList();

            var vms = filtered.Select(l => new StudentLogViewModel(l)).ToList();
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
        if (_selectedStudent is null || _currentCourse is null) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var seed = new StudentLog
        {
            StudentID   = _selectedStudent.StudentID,
            Year        = Settings.WorkYear.Value,
            Semester    = Settings.WorkSemester.Value,
            TeacherID   = Settings.User.Value,
            Date        = DateTime.Now,
            Category    = LogCategory.교과활동,
            CourseNo    = _currentCourse.No,
            SubjectName = _currentCourse.Subject,
        };

        var dlg = new Views.Dialogs.StudentLogEditDialog(
            _selectedStudent.StudentID,
            $"{_selectedStudent.Grade}학년 {_selectedStudent.Class}반 {_selectedStudent.Number}번 {_selectedStudent.Name}",
            seed);
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

    private async void BtnDeleteLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.DeleteSelectedLogsAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LessonActivityPage] Delete: {ex.Message}"); }
    }

    // ────────────────────────────────────────────────────
    //  글꼴 크기 슬라이더
    // ────────────────────────────────────────────────────

    private void SldFontSize_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (LogList == null) return;
        LogList.LogFontSize = e.NewValue;
        if (TbSize != null) TbSize.Text = e.NewValue.ToString("F0");
    }
}
