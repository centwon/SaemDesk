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
/// 과목(Course) · 강의실(Room) 선택 필터.
///
/// 확정 규칙:
///   - LoadAsync(year, semester, grade=0) 로 과목 목록을 (재)로드.
///     grade=0 이면 학년 구분 없이 현재 교사의 전 과목.
///     YearSemesterPicker 또는 ClassPicker.ClassChanged 에서 호출.
///   - 강의실은 선택된 Course.RoomList 에서 파싱 (DB 조회 없음).
///     강의실이 1개 이상이면 자동 표시, 0개이면 CBoxRoom 숨김.
///   - ShowRoom=false 이면 강의실 콤보 항상 숨김.
///   - 과목/강의실이 확정되면 수강생 조회 후 CourseChangedEventArgs 로 이벤트 발생.
///     유형과 무관하게 CourseEnrollmentRepository(수강자 명단)에서 조회하고
///     선택된 강의실(Room)로 필터. 강의실 미선택(전체)이면 과목 전체 수강자.
/// </summary>
public partial class CoursePicker : UserControl
{
    // ── 상태 ────────────────────────────────────────────
    private bool _initialized;
    private bool _updating;
    private int  _loadedYear;
    private int  _loadedSemester;
    private int  _loadedGrade;

    // ── 옵션 ────────────────────────────────────────────
    /// <summary>강의실 콤보 표시 여부 (기본 true)</summary>
    public bool ShowRoom { get; set; } = true;

    /// <summary>강의실 목록에 "전체" 항목 포함 여부 (기본 false)</summary>
    public bool IncludeAllRoom { get; set; } = false;

    // ── 현재 선택값 ─────────────────────────────────────
    public Course?  SelectedCourse => CBoxCourse.SelectedItem as Course;
    public string?  SelectedRoom   => (CBoxRoom.SelectedItem as ComboBoxItem)?.Tag as string;

    // ── 이벤트 ──────────────────────────────────────────
    public event EventHandler<CourseChangedEventArgs>? CourseChanged;

    // ── 생성자 ──────────────────────────────────────────
    public CoursePicker()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── 초기화 ──────────────────────────────────────────

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        CBoxRoom.IsVisible = ShowRoom;
        await LoadAsync(Settings.WorkYear.Value, Settings.WorkSemester.Value);
    }

    /// <summary>
    /// 과목 목록을 (재)로드.
    /// YearSemesterPicker.YearSemesterChanged 또는 ClassPicker.ClassChanged 에서 호출.
    /// grade=0 이면 학년 구분 없이 현재 교사의 전 과목.
    /// </summary>
    public async Task LoadAsync(int year, int semester, int grade = 0)
    {
        _loadedYear     = year;
        _loadedSemester = semester;
        _loadedGrade    = grade;

        _updating = true;
        try
        {
            var courses = await FetchCoursesAsync(year, semester, grade);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CBoxCourse.SelectionChanged -= OnCourseChanged;

                CBoxCourse.ItemsSource = courses;

                // 이전 선택 과목 유지 시도
                var prev = SelectedCourse;
                if (prev != null)
                {
                    var match = courses.FirstOrDefault(c => c.No == prev.No);
                    CBoxCourse.SelectedItem = match;
                }
                if (CBoxCourse.SelectedItem is null && courses.Count > 0)
                    CBoxCourse.SelectedIndex = 0;

                CBoxCourse.SelectionChanged += OnCourseChanged;
            });

            RefreshRoomCombo(SelectedCourse);
            _initialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CoursePicker] LoadAsync 오류: {ex.Message}");
        }
        finally { _updating = false; }

        await RaiseChangedAsync();
    }

    // ── 과목 목록 조회 ───────────────────────────────────

    private static async Task<List<Course>> FetchCoursesAsync(int year, int semester, int grade)
    {
        try
        {
            using var repo = new CourseRepository(SchoolDatabase.DbPath);
            var all = await repo.GetByTeacherAsync(Settings.User.Value, year, semester);
            return grade > 0 ? all.Where(c => c.Grade == grade).ToList() : all;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CoursePicker] 과목 조회 오류: {ex.Message}");
            return new List<Course>();
        }
    }

    // ── 강의실 목록 구성 ─────────────────────────────────

    private void RefreshRoomCombo(Course? course)
    {
        CBoxRoom.SelectionChanged -= OnRoomChanged;
        CBoxRoom.Items.Clear();

        if (!ShowRoom || course == null || course.RoomList.Count == 0)
        {
            CBoxRoom.IsVisible = false;
            CBoxRoom.SelectionChanged += OnRoomChanged;
            return;
        }

        // 강의실이 2개 이상이고 IncludeAllRoom=true 이면 "전체" 첫 항목 추가
        if (course.RoomList.Count > 1 && IncludeAllRoom)
            CBoxRoom.Items.Add(new ComboBoxItem { Content = "전체", Tag = (string?)null });

        foreach (var r in course.RoomList)
            CBoxRoom.Items.Add(new ComboBoxItem { Content = r, Tag = r });

        CBoxRoom.IsVisible     = true;
        CBoxRoom.SelectedIndex = 0;

        CBoxRoom.SelectionChanged += OnRoomChanged;
    }

    // ── ComboBox 이벤트 ──────────────────────────────────

    private async void OnCourseChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updating) return;
        RefreshRoomCombo(SelectedCourse);
        await RaiseChangedAsync();
    }

    private async void OnRoomChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updating) return;
        await RaiseChangedAsync();
    }

    // ── 수강생 조회 & 이벤트 발생 ───────────────────────

    private async Task RaiseChangedAsync()
    {
        var course = SelectedCourse;
        if (course == null) return;

        var students = await FetchStudentsAsync(course);

        CourseChanged?.Invoke(this, new CourseChangedEventArgs
        {
            Course   = course,
            Room     = SelectedRoom,
            Students = students.AsReadOnly(),
        });
    }

    private async Task<List<Enrollment>> FetchStudentsAsync(Course course)
    {
        try
        {
            // 수강자 명단은 유형과 무관하게 CourseEnrollment 가 단일 원천.
            // (학급공통 과목도 일괄배치로 Room="{Grade}-{Class}" 행이 생성됨)
            using var ceRepo = new CourseEnrollmentRepository(SchoolDatabase.DbPath);
            var courseEnrollments = await ceRepo.GetByCourseAsync(course.No);
            if (courseEnrollments.Count == 0) return new List<Enrollment>();

            // 강의실 필터: null(전체)이면 필터 없음, 특정 강의실이면 해당 강의실만
            var room = SelectedRoom;
            if (room != null)
                courseEnrollments = courseEnrollments.Where(ce => ce.Room == room).ToList();

            var studentIds = courseEnrollments.Select(ce => ce.StudentID).ToHashSet();
            using var eRepo = new EnrollmentRepository(SchoolDatabase.DbPath);
            var all = await eRepo.GetBySchoolAndYearAsync(
                Settings.SchoolCode.Value, _loadedYear);
            return all.Where(e => studentIds.Contains(e.StudentID)).ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CoursePicker] 수강생 조회 오류: {ex.Message}");
            return new List<Enrollment>();
        }
    }
}

/// <summary>CoursePicker 변경 이벤트 인자 — 수강생 목록 포함</summary>
public sealed class CourseChangedEventArgs : EventArgs
{
    public Course   Course   { get; init; } = null!;
    public string?  Room     { get; init; }
    public IReadOnlyList<Enrollment> Students { get; init; } = Array.Empty<Enrollment>();
}
