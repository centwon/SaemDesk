using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 학생 누가기록 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.PageStudentLog (WinUI3).
/// 구성: 좌측 ListStudent + 우측 LogListViewer (+ StudentSpecBox).
/// </summary>
public partial class StudentLogPage : UserControl, IDisposable
{
    private bool _disposed;
    private StudentLogPageVM VM => (StudentLogPageVM)DataContext!;

    private Enrollment? _selectedStudent;
    private IReadOnlyList<Enrollment> _currentStudents = Array.Empty<Enrollment>();
    private readonly StudentLogService _logService = new();

    public StudentLogPage()
    {
        InitializeComponent();
        DataContext = new StudentLogPageVM();

        // 학생 선택 이벤트 연결
        StudentList.StudentSelected += OnStudentSelected;

        // 필터 이벤트 연결
        YearSemPicker.YearSemesterChanged += OnYearSemesterChanged;
        ClassFilter.ClassChanged          += OnClassChanged;

        // 슬라이더 초기 레이블
        SldFontSize.Value = 12;

        // 컨텍스트 메뉴 설정
        SetupStudentContextMenu();

        // 초기 설정
        LogList.StudentInfoMode = StudentInfoMode.HideAll;
        LogList.Category        = LogCategory.전체;
        LogList.LogEdited       += (_, log) => _ = LoadLogsAsync();

        Unloaded += (_, _) => Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logService.Dispose();
        StudentList.StudentSelected           -= OnStudentSelected;
        YearSemPicker.YearSemesterChanged     -= OnYearSemesterChanged;
        ClassFilter.ClassChanged              -= OnClassChanged;
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────
    //  학생 목록 로드
    // ────────────────────────────────────────────────────

    // 학년도·학기 변경 → ClassPicker 재로드
    private async void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        await ClassFilter.LoadAsync(e.Year, e.Semester);
    }

    // 학급 변경 — e.Students 직접 사용
    private void OnClassChanged(object? sender, ClassChangedEventArgs e)
    {
        VM.WorkYear      = e.Year;
        VM.SemesterIndex = e.Semester;
        VM.Grade         = e.Grade;
        VM.ClassNum      = e.Class;
        _currentStudents = e.Students;
        StudentList.LoadStudents(e.Students);
        _selectedStudent = null;
        LogList.Clear();
    }

    // ────────────────────────────────────────────────────
    //  카테고리 변경
    // ────────────────────────────────────────────────────

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        LogList.Category = VM.SelectedCategory;
        await LoadLogsAsync();
        UpdateSpecBoxVisibility();
    }

    // ────────────────────────────────────────────────────
    //  학생 선택
    // ────────────────────────────────────────────────────

    private async void OnStudentSelected(object? sender, Enrollment student)
    {
        await CheckUnsavedAsync();
        _selectedStudent = student;
        await LoadLogsAsync();
        UpdateSpecBoxVisibility();
    }

    // ────────────────────────────────────────────────────
    //  버튼 핸들러
    // ────────────────────────────────────────────────────

    private async void BtnNewActLog_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedStudent is null) return;

        var log = new StudentLog
        {
            Category  = VM.SelectedCategory == LogCategory.전체 ? LogCategory.기타 : VM.SelectedCategory,
            TeacherID = Settings.User.Value,
            Year      = VM.WorkYear,
            Semester  = VM.Semester,
            StudentID = _selectedStudent.StudentID,
            Date      = DateTime.Now,
        };

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new Views.Dialogs.StudentLogEditDialog(
            log.StudentID,
            $"{_selectedStudent.Grade}학년 {_selectedStudent.Class}반 {_selectedStudent.Number}번 {_selectedStudent.Name}",
            log);
        await dlg.ShowDialog(owner);
        if (dlg.Result != null) await LoadLogsAsync();
    }

    private async void BtnSaveActLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.SaveChangedLogsAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[StudentLogPage] Save: {ex.Message}"); }
    }

    private async void BtnEditLog_Click(object? sender, RoutedEventArgs e)
        => await LogList.EditSelectedLogAsync();

    private async void BtnPrint_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedStudent is null) return;

        var logs = LogList.Logs.ToList();
        if (logs.Count == 0) return;

        try
        {
            var studentVm = new StudentCardViewModel();
            await studentVm.LoadStudentAsync(_selectedStudent.StudentID);

            var printService = new StudentLogPrintService();
            string filePath = printService.GenerateStudentLogPdf(studentVm, logs);

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentLogPage] Print: {ex.Message}");
        }
    }

    private async void BtnDelActLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.DeleteSelectedLogsAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[StudentLogPage] Delete: {ex.Message}"); }
    }

    private async void BtnAddLogAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentStudents.Count == 0) return;

        // 교과활동·동아리는 일괄 입력 불가 (별도 경로 사용)
        var category = VM.SelectedCategory;
        if (category is LogCategory.전체 or LogCategory.교과활동 or LogCategory.동아리활동)
            category = LogCategory.자율활동;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new Views.Dialogs.StudentLogBatchDialog(
            _currentStudents,
            VM.WorkYear,
            VM.Semester,
            category);

        await dlg.ShowDialog(owner);

        if (dlg.IsSuccess)
        {
            if (_selectedStudent != null) await LoadLogsAsync();
        }
    }

    private async void BtnBatchExport_Click(object? sender, RoutedEventArgs e)
        => await BatchExportAsync();

    private async Task BatchExportAsync()
    {
        if (VM.Grade == 0 || VM.ClassNum == 0) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var filterDlg = new Views.Dialogs.BatchExportFilterDialog();
        await filterDlg.ShowDialog(owner);
        if (!filterDlg.IsSuccess) return;

        var filterCategory = filterDlg.SelectedCategory;
        var filterSemester = filterDlg.SelectedSemester;
        var keyword        = filterDlg.Keyword;
        bool isPdf         = filterDlg.IsPdf;

        try
        {
            using var enrollSvc = new EnrollmentService();
            var enrollments = await enrollSvc.GetClassRosterAsync(
                Settings.SchoolCode.Value, VM.WorkYear, VM.Grade, VM.ClassNum);

            if (enrollments.Count == 0) return;

            using var logSvc = new StudentLogService();
            var studentLogsList =
                new List<(StudentCardViewModel Student, List<StudentLogViewModel> Logs)>();
            int totalLogs = 0;

            foreach (var en in enrollments.OrderBy(e => e.Number))
            {
                List<StudentLog> logs;
                if (filterSemester == 0)
                {
                    var l1 = await logSvc.GetStudentLogsAsync(en.StudentID, VM.WorkYear, 1);
                    var l2 = await logSvc.GetStudentLogsAsync(en.StudentID, VM.WorkYear, 2);
                    logs = l1.Concat(l2).ToList();
                }
                else
                {
                    logs = await logSvc.GetStudentLogsAsync(en.StudentID, VM.WorkYear, filterSemester);
                }

                if (filterCategory != LogCategory.전체)
                    logs = logs.Where(l => l.Category == filterCategory).ToList();

                if (!string.IsNullOrEmpty(keyword))
                    logs = logs.Where(l =>
                        (l.Topic        != null && l.Topic.Contains(keyword,        StringComparison.OrdinalIgnoreCase)) ||
                        (l.ActivityName != null && l.ActivityName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                        (l.Description  != null && l.Description.Contains(keyword,  StringComparison.OrdinalIgnoreCase)) ||
                        (l.Log          != null && l.Log.Contains(keyword,          StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                if (logs.Count == 0) continue;

                logs = logs.OrderByDescending(l => l.Date).ToList();

                var logVms    = logs.Select(l => new StudentLogViewModel(l)).ToList();
                var studentVm = new StudentCardViewModel();
                studentVm.LoadFromEnrollment(en);

                studentLogsList.Add((studentVm, logVms));
                totalLogs += logs.Count;
            }

            if (studentLogsList.Count == 0) return;

            string filePath;
            if (isPdf)
            {
                var printSvc = new StudentLogPrintService();
                filePath = printSvc.GenerateClassLogPdf(VM.WorkYear, VM.Grade, VM.ClassNum, studentLogsList);
            }
            else
            {
                var exportSvc = new StudentLogExportService();
                filePath = exportSvc.ExportClassLogsToExcel(VM.WorkYear, VM.Grade, VM.ClassNum, studentLogsList);
            }

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentLogPage] BatchExport: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  로그 로드
    // ────────────────────────────────────────────────────

    private async Task LoadLogsAsync()
    {
        if (_selectedStudent is null) return;

        try
        {
            using var svc = new StudentLogService();
            var logs = await svc.GetStudentLogsAsync(
                _selectedStudent.StudentID, VM.WorkYear, VM.Semester);

            if (VM.SelectedCategory != LogCategory.전체)
                logs = logs.Where(l => l.Category == VM.SelectedCategory).ToList();

            logs = logs.OrderByDescending(l => l.Date).ToList();

            var vms = new List<StudentLogViewModel>();
            foreach (var l in logs)
                vms.Add(new StudentLogViewModel(l));

            LogList.LoadLogs(vms);
            LogList.StudentInfoMode = StudentInfoMode.HideAll;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentLogPage] LoadLogs: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  SpecBox 가시성
    // ────────────────────────────────────────────────────

    private void UpdateSpecBoxVisibility()
    {
        bool show = _selectedStudent is not null && VM.SelectedCategory switch
        {
            LogCategory.자율활동   => true,
            LogCategory.진로활동   => true,
            LogCategory.종합의견   => true,
            LogCategory.개인별세특 => true,
            _                      => false,
        };
        SpecBox.IsVisible = show;
        if (show) LoadSpecAsync();
    }

    private async void LoadSpecAsync()
    {
        if (_selectedStudent is null) return;
        try
        {
            using var svc = new StudentSpecialService();
            var specials = await svc.GetByStudentAsync(_selectedStudent.StudentID, VM.WorkYear);
            string type  = VM.SelectedCategory.ToString();
            var spec     = specials.FirstOrDefault(s => s.Type == type)
                           ?? new StudentSpecial
                           {
                               StudentID = _selectedStudent.StudentID,
                               Year      = VM.WorkYear,
                               Type      = type,
                               Title     = $"{_selectedStudent.Name} {type}",
                               Date      = DateTime.Now.ToString("yyyy-MM-dd"),
                               TeacherID = Settings.User.Value,
                           };
            SpecBox.Special = spec;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentLogPage] LoadSpec: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  미저장 확인
    // ────────────────────────────────────────────────────

    private async Task CheckUnsavedAsync()
    {
        if (LogList.SelectedCount == 0) return;
        try { await LogList.SaveChangedLogsAsync(); }
        catch { /* 무시 */ }
    }

    // ────────────────────────────────────────────────────
    //  글꼴 크기 슬라이더
    // ────────────────────────────────────────────────────

    private void SldFontSize_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (LogList == null) return;
        LogList.LogFontSize = e.NewValue;   // LogListViewer.LogFontSizeProperty → TextBox FontSize 바인딩
        if (TbSize != null) TbSize.Text = e.NewValue.ToString("F0");
    }

    // ────────────────────────────────────────────────────
    //  컨텍스트 메뉴
    // ────────────────────────────────────────────────────

    private void SetupStudentContextMenu()
    {
        var menu = new ContextMenu();

        var miAddLog = new MenuItem { Header = "누가기록 작성" };
        miAddLog.Click += ContextMenu_AddLog_Click;

        var miViewInfo = new MenuItem { Header = "학생 정보 보기" };
        miViewInfo.Click += ContextMenu_ViewStudentInfo_Click;

        menu.Items.Add(miAddLog);
        menu.Items.Add(new Separator());
        menu.Items.Add(miViewInfo);

        StudentList.ItemContextFlyout = menu;
    }

    private async void ContextMenu_AddLog_Click(object? sender, RoutedEventArgs e)
    {
        var student = StudentList.SelectedStudent;
        if (student is null || VM.WorkYear <= 0) return;

        var log = new StudentLog
        {
            StudentID = student.StudentID,
            Year      = VM.WorkYear,
            Semester  = VM.Semester,
            TeacherID = Settings.User.Value,
            Date      = DateTime.Now,
        };

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new Views.Dialogs.StudentLogEditDialog(
            log.StudentID,
            $"{student.Grade}학년 {student.Class}반 {student.Number}번 {student.Name}",
            null);
        await dlg.ShowDialog(owner);
        if (dlg.Result != null) await LoadLogsAsync();
    }

    private async void ContextMenu_ViewStudentInfo_Click(object? sender, RoutedEventArgs e)
    {
        var student = StudentList.SelectedStudent;
        if (student is null) return;

        var dlg = new Views.Dialogs.StudentDetailDialog(
            student.StudentID,
            student.GetClassInfo(),
            student.Name,
            VM.WorkYear);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null) await dlg.ShowDialog(owner);
    }
}
