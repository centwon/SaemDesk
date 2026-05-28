using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 학생부 특기사항 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.StudentSpecPage (WinUI3).
/// 구성: 필터 영역 + SpecListViewer 컨트롤.
/// </summary>
public partial class StudentSpecPage : UserControl, IDisposable
{
    private bool _disposed;
    private StudentSpecPageVM VM => (StudentSpecPageVM)DataContext!;

    private readonly StudentSpecialService _specialService = new();
    private IReadOnlyList<Enrollment> _currentStudents = Array.Empty<Enrollment>();

    public StudentSpecPage()
    {
        InitializeComponent();
        DataContext = new StudentSpecPageVM();
        Unloaded += (_, _) => Dispose();

        // 필터 이벤트 연결
        YearSemPicker.YearSemesterChanged += OnYearSemesterChanged;
        ClassFilter.ClassChanged          += OnClassFilterChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _specialService.Dispose();
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────
    //  이벤트 핸들러
    // ────────────────────────────────────────────────────

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        SpecListViewer.Category = VM.SelectedCategory;
        await LoadSpecsAsync(_currentStudents);
    }

    private async void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        await ClassFilter.LoadAsync(e.Year, e.Semester);
    }

    private async void OnClassFilterChanged(object? sender, ClassChangedEventArgs e)
    {
        VM.WorkYear  = e.Year;
        VM.Grade     = e.Grade;
        VM.ClassNum  = e.Class;
        _currentStudents = e.Students;
        await LoadSpecsAsync(e.Students);
    }

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
            System.Diagnostics.Debug.WriteLine($"[StudentSpecPage] Save: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"[StudentSpecPage] Delete: {ex.Message}");
        }
    }

    private async void OnBatchInputClick(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        string? defaultType = VM.SelectedCategory == LogCategory.전체
            ? null
            : VM.SelectedCategory.ToString();

        var dlg = new Views.Dialogs.StudentSpecBatchDialog(
            VM.WorkYear, Settings.WorkSemester.Value, VM.Grade, VM.ClassNum, defaultType);

        await dlg.ShowDialog(owner);
        await LoadSpecsAsync(_currentStudents);
    }

    private async void OnBatchExportClick(object? sender, RoutedEventArgs e)
    {
        if (!_currentStudents.Any() || VM.Grade == 0 || VM.ClassNum == 0)
        {
            await DialogService.ShowInfoAsync("학년, 반을 먼저 선택해주세요.");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new Views.Dialogs.SpecExportFilterDialog();
        await dlg.ShowDialog(owner);
        if (!dlg.IsSuccess) return;

        var filterType  = dlg.SelectedType;
        var statusFilter = dlg.StatusFilter;
        bool excludeEmpty = dlg.ExcludeEmpty;
        bool isPdf = dlg.IsPdf;

        try
        {
            var studentSpecsList = new List<(int Number, string Name, List<StudentSpecial> Specs)>();

            foreach (var student in _currentStudents.OrderBy(s => s.Number))
            {
                List<StudentSpecial> specs;

                if (!string.IsNullOrEmpty(filterType))
                    specs = await _specialService.GetByTypeAsync(student.StudentID, VM.WorkYear, filterType);
                else
                    specs = await _specialService.GetByStudentAsync(student.StudentID, VM.WorkYear);

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

            string filePath;
            if (isPdf)
                filePath = new StudentSpecPrintService()
                    .GenerateClassSpecPdf(VM.WorkYear, VM.Grade, VM.ClassNum, studentSpecsList);
            else
                filePath = new StudentSpecExportService()
                    .ExportClassSpecsToExcel(VM.WorkYear, VM.Grade, VM.ClassNum, studentSpecsList);

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync($"출력 중 오류가 발생했습니다: {ex.Message}");
        }
    }

    private void SldFontSize_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (SpecListViewer == null) return;
        SpecListViewer.SpecFontSize = e.NewValue;
        if (TbSize != null) TbSize.Text = e.NewValue.ToString("F0");
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

        foreach (var student in students)
        {
            List<StudentSpecial> specs;

            if (VM.SelectedCategory == LogCategory.전체)
                specs = await _specialService.GetByStudentAsync(student.StudentID, VM.WorkYear);
            else
                specs = await _specialService.GetByTypeAsync(
                    student.StudentID, VM.WorkYear, VM.SelectedCategory.ToString());

            if (specs.Any())
                allSpecs.AddRange(specs);
            else if (IsAutoCreateCategory(VM.SelectedCategory))
                allSpecs.Add(CreateEmptySpec(student.StudentID, VM.WorkYear));
        }

        SpecListViewer.LoadSpecs(allSpecs, lookup);
        SpecListViewer.StudentInfoMode = StudentInfoMode.NumName;
        SpecListViewer.Category        = VM.SelectedCategory;
    }

    // ────────────────────────────────────────────────────
    //  헬퍼
    // ────────────────────────────────────────────────────

    private static bool IsAutoCreateCategory(LogCategory cat) => cat switch
    {
        LogCategory.자율활동   => true,
        LogCategory.진로활동   => true,
        LogCategory.종합의견   => true,
        LogCategory.개인별세특 => true,
        _ => false,
    };

    private StudentSpecial CreateEmptySpec(string studentId, int year) => new()
    {
        StudentID = studentId,
        Year      = year,
        Type      = VM.SelectedCategory.ToString(),
        Date      = DateTime.Now.ToString("yyyy-MM-dd"),
        TeacherID = Settings.User.Value,
    };
}
