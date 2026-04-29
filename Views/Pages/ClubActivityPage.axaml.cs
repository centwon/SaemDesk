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
/// 동아리 활동 기록 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.ClubActivityPage (WinUI3).
/// 구성: 동아리/카테고리 선택 + 좌측 ListStudent(부원) + 우측 LogListViewer.
/// </summary>
public partial class ClubActivityPage : UserControl, IDisposable
{
    private bool _disposed;
    private ClubActivityPageVM VM => (ClubActivityPageVM)DataContext!;

    private Enrollment? _selectedStudent;

    public ClubActivityPage()
    {
        InitializeComponent();
        DataContext = new ClubActivityPageVM();

        StudentList.StudentSelected += OnStudentSelected;
        LogList.StudentInfoMode = StudentInfoMode.HideAll;
        LogList.Category        = LogCategory.동아리활동;
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
    //  동아리 / 카테고리 변경
    // ────────────────────────────────────────────────────

    private async void OnClubChanged(object? sender, SelectionChangedEventArgs e)
        => await LoadStudentsAsync();

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        LogList.Category = VM.SelectedCategory;
        await LoadLogsAsync();
    }

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private async Task LoadStudentsAsync()
    {
        var club = VM.SelectedClub;
        if (club is null) return;

        try
        {
            using var repo = new SaemDesk.Repositories.ClubEnrollmentRepository(SchoolDatabase.DbPath);
            var enrollments = await repo.GetByClubAsync(club.No);

            // Enrollment 로 변환 (ClubEnrollment → Enrollment)
            var students = enrollments.Select(e => new Enrollment
            {
                StudentID = e.StudentID,
                SchoolCode = Settings.SchoolCode.Value,
            }).ToList();

            StudentList.LoadStudents(students);
            TxtMemberCount.Text = $"{students.Count}명";
            _selectedStudent = null;
            LogList.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClubActivityPage] LoadStudents: {ex.Message}");
        }
    }

    private async void OnStudentSelected(object? sender, Enrollment student)
    {
        _selectedStudent = student;
        TxtSelectedStudent.Text = $"{student.Name} 활동 기록";
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        if (_selectedStudent is null) return;

        try
        {
            using var svc = new StudentLogService();
            var logs = await svc.GetStudentLogsAsync(
                _selectedStudent.StudentID, VM.Year);

            if (VM.SelectedCategory != LogCategory.전체)
                logs = logs.Where(l => l.Category == VM.SelectedCategory).ToList();

            logs = logs.OrderByDescending(l => l.Date).ToList();

            var vms = new List<StudentLogViewModel>();
            foreach (var l in logs) vms.Add(new StudentLogViewModel(l));

            LogList.LoadLogs(vms);
            TxtLogCount.Text = $"{logs.Count}건";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClubActivityPage] LoadLogs: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  버튼
    // ────────────────────────────────────────────────────

    private async void BtnAddLog_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedStudent is null) return;

        var log = new StudentLog
        {
            StudentID = _selectedStudent.StudentID,
            Category  = LogCategory.동아리활동,
            ClubNo    = VM.SelectedClub?.No ?? 0,
            ClubName  = VM.SelectedClub?.ClubName ?? "",
            Year      = VM.Year,
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

    private async void BtnSaveLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.SaveChangedLogsAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ClubActivityPage] Save: {ex.Message}"); }
    }

    private async void BtnDeleteLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.DeleteSelectedLogsAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ClubActivityPage] Delete: {ex.Message}"); }
    }

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
        => _ = LoadStudentsAsync();
}
