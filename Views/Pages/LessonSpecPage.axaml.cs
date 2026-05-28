using System;
using System.Collections.Generic;
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

/// <summary>
/// 수업용 학생부 관리 페이지.
/// CoursePicker 과목/강의실 필터, 카테고리 교과활동 고정.
/// </summary>
public partial class LessonSpecPage : UserControl, IDisposable
{
    private bool _disposed;
    private LessonSpecPageVM VM => (LessonSpecPageVM)DataContext!;

    private readonly StudentSpecialService _specialService = new();
    private Course? _currentCourse;
    private IReadOnlyList<Enrollment> _currentStudents = Array.Empty<Enrollment>();

    public LessonSpecPage()
    {
        InitializeComponent();
        DataContext = new LessonSpecPageVM();

        YearSemPicker.YearSemesterChanged += OnYearSemesterChanged;
        CoursePicker.CourseChanged += OnCourseChanged;

        SpecListViewer.Category        = LogCategory.교과활동;
        SpecListViewer.StudentInfoMode = StudentInfoMode.NumName;

        Unloaded += (_, _) => Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _specialService.Dispose();
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────
    //  학년도/학기 변경 → CoursePicker 재로드
    // ────────────────────────────────────────────────────

    private async void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        VM.WorkYear = e.Year;
        await CoursePicker.LoadAsync(e.Year, e.Semester);
    }

    // ────────────────────────────────────────────────────
    //  CoursePicker 변경
    // ────────────────────────────────────────────────────

    private async void OnCourseChanged(object? sender, CourseChangedEventArgs e)
    {
        _currentCourse   = e.Course;
        _currentStudents = e.Students;
        await LoadSpecsAsync(e.Students);
    }

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private async Task LoadSpecsAsync(IReadOnlyList<Enrollment> students)
    {
        if (!students.Any())
        {
            SpecListViewer.LoadSpecs(new List<StudentSpecial>());
            return;
        }

        var lookup = students.ToDictionary(
            s => s.StudentID,
            s => (Grade: s.Grade, ClassNum: s.Class, Number: s.Number, Name: s.Name));

        var allSpecs = new List<StudentSpecial>();

        foreach (var student in students.OrderBy(s => s.Grade).ThenBy(s => s.Class).ThenBy(s => s.Number))
        {
            var specs = await _specialService.GetByTypeAsync(
                student.StudentID, VM.WorkYear, LogCategory.교과활동.ToString());

            // 현재 과목(CourseNo)에 해당하는 기록만 필터
            if (_currentCourse != null)
                specs = specs.Where(s => s.CourseNo == _currentCourse.No).ToList();

            if (specs.Any())
                allSpecs.AddRange(specs);
            else
                allSpecs.Add(CreateEmptySpec(student.StudentID));
        }

        SpecListViewer.LoadSpecs(allSpecs, lookup);
        SpecListViewer.Category        = LogCategory.교과활동;
        SpecListViewer.StudentInfoMode = StudentInfoMode.NumName;
    }

    // ────────────────────────────────────────────────────
    //  버튼
    // ────────────────────────────────────────────────────

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var selected = SpecListViewer.SelectedSpecs.ToList();
        if (!selected.Any()) return;

        try
        {
            foreach (var vm in selected)
            {
                if (vm.Special.No > 0)
                    await _specialService.UpdateAsync(vm.Special);
                else
                    vm.Special.No = await _specialService.CreateAsync(vm.Special);

                vm.MarkAsSaved();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LessonSpecPage] Save: {ex.Message}");
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        var selected = SpecListViewer.SelectedSpecs.ToList();
        if (!selected.Any()) return;

        try
        {
            foreach (var vm in selected.Where(s => s.Special.No > 0))
                await _specialService.DeleteAsync(vm.Special.No);

            await LoadSpecsAsync(_currentStudents);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LessonSpecPage] Delete: {ex.Message}");
        }
    }

    private async void OnBatchExportClick(object? sender, RoutedEventArgs e)
    {
        if (_currentCourse is null || !_currentStudents.Any())
        {
            await DialogService.ShowInfoAsync("과목과 학생을 먼저 선택해주세요.");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new Views.Dialogs.SpecExportFilterDialog();
        await dlg.ShowDialog(owner);
        if (!dlg.IsSuccess) return;

        var statusFilter = dlg.StatusFilter;
        bool excludeEmpty = dlg.ExcludeEmpty;
        bool isPdf = dlg.IsPdf;

        try
        {
            var studentSpecsList = new List<(int Number, string Name, List<StudentSpecial> Specs)>();

            foreach (var student in _currentStudents.OrderBy(s => s.Grade).ThenBy(s => s.Class).ThenBy(s => s.Number))
            {
                var specs = await _specialService.GetByTypeAsync(
                    student.StudentID, VM.WorkYear, LogCategory.교과활동.ToString());

                specs = specs.Where(s => s.CourseNo == _currentCourse.No).ToList();

                if (statusFilter == "draft")
                    specs = specs.Where(s => !s.IsFinalized).ToList();
                else if (statusFilter == "finalized")
                    specs = specs.Where(s => s.IsFinalized).ToList();

                if (excludeEmpty)
                    specs = specs.Where(s => !string.IsNullOrWhiteSpace(s.Content)).ToList();

                if (specs.Count == 0) continue;
                studentSpecsList.Add((student.Number, student.Name, specs));
            }

            if (studentSpecsList.Count == 0)
            {
                await DialogService.ShowInfoAsync("조건에 맞는 기록이 없습니다.");
                return;
            }

            int grade = _currentStudents.First().Grade;
            int classNo = _currentStudents.First().Class;

            string filePath;
            if (isPdf)
                filePath = new StudentSpecPrintService()
                    .GenerateClassSpecPdf(VM.WorkYear, grade, classNo, studentSpecsList);
            else
                filePath = new StudentSpecExportService()
                    .ExportClassSpecsToExcel(VM.WorkYear, grade, classNo, studentSpecsList);

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync($"출력 중 오류가 발생했습니다: {ex.Message}");
        }
    }

    private void SldFontSize_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (SpecListViewer == null) return;
        SpecListViewer.SpecFontSize = e.NewValue;
        if (TbSize != null) TbSize.Text = e.NewValue.ToString("F0");
    }

    // ────────────────────────────────────────────────────
    //  헬퍼
    // ────────────────────────────────────────────────────

    private StudentSpecial CreateEmptySpec(string studentId) => new()
    {
        StudentID   = studentId,
        Year        = VM.WorkYear,
        Type        = LogCategory.교과활동.ToString(),
        Date        = DateTime.Now.ToString("yyyy-MM-dd"),
        TeacherID   = Settings.User.Value,
        CourseNo    = _currentCourse?.No ?? 0,
        SubjectName = _currentCourse?.Subject ?? string.Empty,
    };
}
