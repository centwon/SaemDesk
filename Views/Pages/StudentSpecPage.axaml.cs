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

        // 반 선택 시 자동 로드
        FilterBar.SelectionChanged += OnFilterBarChanged;
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

    private async void OnFilterBarChanged(object? sender, FilterChangedEventArgs e)
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

    private void OnBatchInputClick(object? sender, RoutedEventArgs e)
    {
        // TODO: StudentSpecBatchDialog
    }

    private void OnBatchExportClick(object? sender, RoutedEventArgs e)
    {
        // TODO: 일괄 출력 (PDF/Excel)
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
