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
/// 학년도 / 학기(선택) / 학년(필수) / 반(전체 허용) 선택 필터.
///
/// 확정 규칙:
///   - 학년 : 전체 없음. 항상 1 이상 선택.
///   - 반   : IncludeAllClass=true 면 "전체(0)" 항목 포함.
///   - 반까지 확정되면 EnrollmentRepository 로 학생 목록을 조회해서
///     FilterChangedEventArgs.Students 에 담아 이벤트 발생.
///   - 학년도·학기를 외부에서 주입하려면 LoadAsync(year, semester) 호출.
///     호출 전까지는 Settings.WorkYear / WorkSemester 로 자동 초기화.
/// </summary>
public partial class ClassFilterBar : UserControl
{
    // ── 상태 ────────────────────────────────────────────
    private bool _initialized;
    private bool _updating;

    // ── 옵션 ────────────────────────────────────────────
    /// <summary>학기 콤보 표시 여부 (기본 false)</summary>
    public bool ShowSemester { get; set; } = false;
    /// <summary>반 목록에 "전체(0)" 항목 포함 여부 (기본 true)</summary>
    public bool IncludeAllClass { get; set; } = true;

    // ── 현재 선택값 ─────────────────────────────────────
    public int Year     => GetTag(CBoxYear);
    public int Semester => GetTag(CBoxSemester);
    public int Grade    => GetTag(CBoxGrade);
    public int ClassNum => GetTag(CBoxClass);

    // ── 이벤트 ──────────────────────────────────────────
    public event EventHandler<FilterChangedEventArgs>? SelectionChanged;

    // ── 생성자 ──────────────────────────────────────────
    public ClassFilterBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── 초기화 ──────────────────────────────────────────

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        await InitializeAsync(Settings.WorkYear.Value, Settings.WorkSemester.Value);
    }

    /// <summary>
    /// 외부에서 학년도·학기를 주입해 목록을 (재)로드.
    /// 페이지의 YearSemesterPicker.SelectionChanged 에서 호출.
    /// </summary>
    public async Task LoadAsync(int year, int semester)
    {
        await InitializeAsync(year, semester);
    }

    private async Task InitializeAsync(int year, int semester)
    {
        _updating = true;
        try
        {
            CBoxSemester.IsVisible = ShowSemester;

            if (ShowSemester) InitSemesterCombo(semester);
            await InitYearComboAsync(year);
            SelectByTag(CBoxYear, year);                        // ← 먼저 학년도 선택
            if (ShowSemester) SelectByTag(CBoxSemester, semester);

            await InitGradeComboAsync(year);
            ApplyDefaultGrade();                                // ← 학년 선택

            await InitClassComboAsync(year, GetTag(CBoxGrade)); // ← 이제 grade > 0
            ApplyDefaultClass();                                // ← 반 선택

            // 이벤트 연결 (중복 방지: 제거 후 재연결)
            CBoxYear.SelectionChanged     -= OnYearChanged;
            CBoxSemester.SelectionChanged -= OnSemesterChanged;
            CBoxGrade.SelectionChanged    -= OnGradeChanged;
            CBoxClass.SelectionChanged    -= OnClassChanged;

            CBoxYear.SelectionChanged     += OnYearChanged;
            CBoxSemester.SelectionChanged += OnSemesterChanged;
            CBoxGrade.SelectionChanged    += OnGradeChanged;
            CBoxClass.SelectionChanged    += OnClassChanged;

            _initialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassFilterBar] 초기화 오류: {ex.Message}");
        }
        finally
        {
            _updating = false;
        }

        await RaiseChangedAsync();
    }

    // ── 콤보 구성 ────────────────────────────────────────

    private void InitSemesterCombo(int selected)
    {
        CBoxSemester.Items.Clear();
        CBoxSemester.Items.Add(new ComboBoxItem { Content = "1학기", Tag = 1 });
        CBoxSemester.Items.Add(new ComboBoxItem { Content = "2학기", Tag = 2 });
        SelectByTag(CBoxSemester, selected > 0 ? selected : 1);
    }

    private async Task InitYearComboAsync(int selected)
    {
        var years = new HashSet<int> { DateTime.Today.Year };
        if (Settings.WorkYear.Value > 0) years.Add(Settings.WorkYear.Value);
        if (selected > 0) years.Add(selected);

        try
        {
            using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
            foreach (var y in await repo.GetEnrollmentYearsAsync(Settings.SchoolCode.Value))
                years.Add(y);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassFilterBar] 학년도 조회 오류: {ex.Message}");
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CBoxYear.Items.Clear();
            foreach (var y in years.OrderByDescending(y => y))
                CBoxYear.Items.Add(new ComboBoxItem { Content = $"{y}학년도", Tag = y });
        });
    }

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
            Debug.WriteLine($"[ClassFilterBar] 학년 조회 오류: {ex.Message}");
        }

        if (grades.Count == 0) grades = new HashSet<int> { 1, 2, 3 };

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CBoxGrade.Items.Clear();
            // 학년은 전체 없음 — 항상 1 이상만
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
            Debug.WriteLine($"[ClassFilterBar] 반 조회 오류: {ex.Message}");
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
            SelectByTag(CBoxClass, 0); // 전체 기본
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

    private async void OnYearChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updating) return;
        _updating = true;
        try
        {
            await InitGradeComboAsync(Year);
            ApplyDefaultGrade();
            await InitClassComboAsync(Year, GetTag(CBoxGrade));
            ApplyDefaultClass();
        }
        finally { _updating = false; }
        await RaiseChangedAsync();
    }

    private async void OnSemesterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updating) return;
        await RaiseChangedAsync();
    }

    private async void OnGradeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updating) return;
        _updating = true;
        try
        {
            await InitClassComboAsync(Year, GetTag(CBoxGrade));
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
        int year    = Year;
        int sem     = Semester;
        int grade   = Grade;
        int classNo = ClassNum;

        // 학년 미선택이면 이벤트 보류
        if (grade <= 0) return;

        // ShowSemester=false 이면 CBoxSemester 가 숨겨져 sem=0
        // → Settings.WorkSemester 로 fallback (학기 무관 조회가 의도인 경우 0 유지)
        int effectiveSem = (sem == 0 && !ShowSemester)
            ? Settings.WorkSemester.Value
            : sem;

        List<Enrollment> students;
        try
        {
            using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
            if (classNo == 0)
                // 반 전체 — 해당 학년 전체 학생
                students = await repo.GetByGradeAsync(
                    Settings.SchoolCode.Value, year, effectiveSem, grade);
            else
                // 특정 반
                students = await repo.GetByClassAsync(
                    Settings.SchoolCode.Value, year, grade, classNo, effectiveSem);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassFilterBar] 학생 조회 오류: {ex.Message}");
            students = new List<Enrollment>();
        }

        SelectionChanged?.Invoke(this, new FilterChangedEventArgs
        {
            Year     = year,
            Semester = effectiveSem,
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
            await InitClassComboAsync(Year, grade);
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

/// <summary>필터 변경 이벤트 인자 — 학생 목록 포함</summary>
public sealed class FilterChangedEventArgs : EventArgs
{
    public int Year     { get; init; }
    public int Semester { get; init; }
    public int Grade    { get; init; }   // 항상 1 이상
    public int Class    { get; init; }   // 0 = 전체 반
    public IReadOnlyList<Enrollment> Students { get; init; } = Array.Empty<Enrollment>();

    public bool IsAllClass => Class == 0;
}
