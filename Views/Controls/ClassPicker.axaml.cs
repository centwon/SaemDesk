using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학년 · 반 선택 필터.
///
/// 확정 규칙:
///   - IncludeAllClass=true 이면 반 목록에 "전체(0)" 항목 포함 (기본 true).
///   - LoadAsync(year, semester) 로 학년/반 목록을 (재)로드.
///     YearSemesterPicker.YearSemesterChanged 에서 호출하거나,
///     단독 사용 시 Loaded 에서 Settings.WorkYear/WorkSemester 로 자동 초기화.
///   - 반까지 확정되면 학생 목록을 조회해서 ClassChangedEventArgs.Students 에 담아 이벤트 발생.
/// </summary>
public partial class ClassPicker : UserControl
{
    // ── 상태 ────────────────────────────────────────────
    private bool _initialized;
    private bool _updating;
    private int  _loadedYear;
    private int  _loadedSemester;

    // ── 옵션 ────────────────────────────────────────────
    /// <summary>반 목록에 "전체(0)" 항목 포함 여부 (기본 true)</summary>
    public bool IncludeAllClass { get; set; } = true;

    // ── 현재 선택값 ─────────────────────────────────────
    public int Grade    => GetTag(CBoxGrade);
    public int ClassNum => GetTag(CBoxClass);

    // ── 이벤트 ──────────────────────────────────────────
    public event EventHandler<ClassChangedEventArgs>? ClassChanged;

    // ── 생성자 ──────────────────────────────────────────
    public ClassPicker()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── 초기화 ──────────────────────────────────────────

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        // YearSemesterPicker 없이 단독 사용 시 Settings 값으로 자동 초기화
        await LoadAsync(Settings.WorkYear.Value, Settings.WorkSemester.Value);
    }

    /// <summary>
    /// 학년/반 목록을 (재)로드.
    /// YearSemesterPicker.YearSemesterChanged 에서 호출.
    /// </summary>
    public async Task LoadAsync(int year, int semester)
    {
        _loadedYear     = year;
        _loadedSemester = semester;

        _updating = true;
        try
        {
            await InitGradeComboAsync(year);
            ApplyDefaultGrade();

            await InitClassComboAsync(year, GetTag(CBoxGrade));
            ApplyDefaultClass();

            CBoxGrade.SelectionChanged -= OnGradeChanged;
            CBoxClass.SelectionChanged -= OnClassChanged;
            CBoxGrade.SelectionChanged += OnGradeChanged;
            CBoxClass.SelectionChanged += OnClassChanged;

            _initialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassPicker] 초기화 오류: {ex.Message}");
        }
        finally
        {
            _updating = false;
        }

        await RaiseChangedAsync();
    }

    // ── 콤보 구성 ────────────────────────────────────────

    private async Task InitGradeComboAsync(int year)
    {
        var grades = new HashSet<int>();
        try
        {
            if (year > 0)
            {
                using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
                foreach (var g in await repo.GetGradesByYearAsync(Settings.SchoolCode.Value, year))
                    grades.Add(g);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassPicker] 학년 조회 오류: {ex.Message}");
        }

        if (grades.Count == 0) grades = new HashSet<int> { 1, 2, 3 };

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CBoxGrade.Items.Clear();
            foreach (var g in grades.OrderBy(x => x))
                CBoxGrade.Items.Add(new ComboBoxItem { Content = $"{g}학년", Tag = g });
        });
    }

    private async Task InitClassComboAsync(int year, int grade)
    {
        var classes = new HashSet<int>();
        try
        {
            if (year > 0 && grade > 0)
            {
                using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
                foreach (var c in await repo.GetClassListByGradeAsync(Settings.SchoolCode.Value, year, grade))
                    classes.Add(c);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassPicker] 반 조회 오류: {ex.Message}");
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CBoxClass.Items.Clear();
            if (IncludeAllClass)
                CBoxClass.Items.Add(new ComboBoxItem { Content = "전체", Tag = 0 });
            foreach (var c in classes.OrderBy(x => x))
                CBoxClass.Items.Add(new ComboBoxItem { Content = $"{c}반", Tag = c });
        });
    }

    private void ApplyDefaultGrade()
    {
        int preferred = Settings.HomeGrade.Value;
        if (preferred > 0) SelectByTag(CBoxGrade, preferred);
        if (CBoxGrade.SelectedItem is null && CBoxGrade.Items.Count > 0)
            CBoxGrade.SelectedIndex = 0;
    }

    private void ApplyDefaultClass()
    {
        if (IncludeAllClass)
        {
            SelectByTag(CBoxClass, 0);
        }
        else
        {
            int preferred = Settings.HomeRoom.Value;
            if (preferred > 0) SelectByTag(CBoxClass, preferred);
        }
        if (CBoxClass.SelectedItem is null && CBoxClass.Items.Count > 0)
            CBoxClass.SelectedIndex = 0;
    }

    // ── ComboBox 이벤트 ──────────────────────────────────

    private async void OnGradeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updating) return;
        _updating = true;
        try
        {
            await InitClassComboAsync(_loadedYear, GetTag(CBoxGrade));
            ApplyDefaultClass();
        }
        finally { _updating = false; }
        await RaiseChangedAsync();
    }

    private async void OnClassChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updating) return;
        await RaiseChangedAsync();
    }

    // ── 학생 조회 & 이벤트 발생 ─────────────────────────

    private async Task RaiseChangedAsync()
    {
        int year    = _loadedYear;
        int sem     = _loadedSemester;
        int grade   = Grade;
        int classNo = ClassNum;

        if (grade <= 0) return;

        List<Enrollment> students;
        try
        {
            using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
            if (classNo == 0)
                students = await repo.GetByGradeAsync(
                    Settings.SchoolCode.Value, year, sem, grade);
            else
                students = await repo.GetByClassAsync(
                    Settings.SchoolCode.Value, year, grade, classNo, sem);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassPicker] 학생 조회 오류: {ex.Message}");
            students = new List<Enrollment>();
        }

        ClassChanged?.Invoke(this, new ClassChangedEventArgs
        {
            Year     = year,
            Semester = sem,
            Grade    = grade,
            Class    = classNo,
            Students = students.AsReadOnly(),
        });
    }

    // ── 공개 메서드 ──────────────────────────────────────

    /// <summary>외부에서 학년·반을 강제 지정 후 이벤트 발생</summary>
    public async Task SetSelectionAsync(int grade, int classNum = 0)
    {
        _updating = true;
        try
        {
            SelectByTag(CBoxGrade, grade);
            await InitClassComboAsync(_loadedYear, grade);
            SelectByTag(CBoxClass, classNum);
        }
        finally { _updating = false; }
        await RaiseChangedAsync();
    }

    // ── 헬퍼 ────────────────────────────────────────────

    private static int GetTag(ComboBox cb)
    {
        if (cb.SelectedItem is ComboBoxItem ci && ci.Tag is int v) return v;
        return 0;
    }

    private static void SelectByTag(ComboBox cb, int tag)
    {
        foreach (var item in cb.Items)
        {
            if (item is ComboBoxItem ci && ci.Tag is int v && v == tag)
            {
                cb.SelectedItem = ci;
                return;
            }
        }
        if (cb.Items.Count > 0) cb.SelectedIndex = 0;
    }
}

/// <summary>학급 필터 변경 이벤트 인자 — 학생 목록 포함</summary>
public sealed class ClassChangedEventArgs : EventArgs
{
    public int Year     { get; init; }
    public int Semester { get; init; }
    public int Grade    { get; init; }
    public int Class    { get; init; }
    public IReadOnlyList<Enrollment> Students { get; init; } = Array.Empty<Enrollment>();

    public bool IsAllClass => Class == 0;
}
