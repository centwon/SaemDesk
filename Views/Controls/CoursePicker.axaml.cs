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
///   - 과목은 LoadAsync(year, semester, grade) 로 목록을 로드.
///     grade=0 이면 학년 구분 없이 전체 과목 표시.
///   - 강의실은 선택된 Course.RoomList 에서 파싱 (DB 조회 없음).
///     강의실이 1개이면 자동 선택, 0개이면 CBoxRoom 숨김.
///   - 과목이 확정되면 Course.Type 에 따라 수강생을 조회해서
///     CoursePickerChangedEventArgs.Students 에 담아 이벤트 발생.
///     Class (학급 공통): EnrollmentRepository → 학급 학생 전체
///     Selective / Club  : CourseEnrollmentRepository → 수강 등록 학생
///   - ShowRoom=false 이면 강의실 콤보 숨김 (LessonActivityPage 외에는 불필요).
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

    // ── 현재 선택값 ─────────────────────────────────────
    public Course?  SelectedCourse => CBoxCourse.SelectedItem as Course;
    public string?  SelectedRoom   => (CBoxRoom.SelectedItem as ComboBoxItem)?.Content?.ToString();

    // ── 이벤트 ──────────────────────────────────────────
    public event EventHandler<CoursePickerChangedEventArgs>? SelectionChanged;

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
    /// ClassFilterBar.SelectionChanged 또는 YearSemesterPicker.SelectionChanged 에서 호출.
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
            CBoxRoom.IsVisible = ShowRoom;

            var courses = await FetchCoursesAsync(year, semester, grade);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CBoxCourse.SelectionChanged -= OnCourseChanged;

                CBoxCourse.ItemsSource = courses;
                CBoxCourse.DisplayMemberBinding = new Avalonia.Data.Binding("DisplayName");

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
            var all = await repo.GetByTeacherAsync(
                Settings.User.Value, year, semester);

            return grade > 0
                ? all.Where(c => c.Grade == grade).ToList()
                : all;
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

        if (course == null || !ShowRoom)
        {
            CBoxRoom.IsVisible = false;
            CBoxRoom.SelectionChanged += OnRoomChanged;
            return;
        }

        var rooms = course.RoomList;
        if (rooms.Count == 0)
        {
            CBoxRoom.IsVisible = false;
            CBoxRoom.SelectionChanged += OnRoomChanged;
            return;
        }

        foreach (var r in rooms)
            CBoxRoom.Items.Add(new ComboBoxItem { Content = r });

        CBoxRoom.IsVisible      = true;
        CBoxRoom.SelectedIndex  = 0; // 1개면 자동 선택, 여러 개면 첫 번째 기본

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

        SelectionChanged?.Invoke(this, new CoursePickerChangedEventArgs
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
            if (course.IsClassType)
            {
                // 학급 공통 과목 — Enrollment 에서 학급 전체 조회
                // Course.Grade 기준, 반은 _loadedGrade 맥락에서 특정되지 않으므로
                // 학년 전체를 반환 (페이지가 필요 시 반 필터 적용)
                using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
                return await repo.GetByGradeAsync(
                    Settings.SchoolCode.Value,
                    _loadedYear,
                    _loadedSemester,
                    course.Grade);
            }
            else
            {
                // 선택 과목 / 동아리 — CourseEnrollment 에서 수강 등록 학생 조회
                using var ceRepo = new CourseEnrollmentRepository(SchoolDatabase.DbPath);
                var courseEnrollments = await ceRepo.GetByCourseAsync(course.No);

                if (courseEnrollments.Count == 0)
                    return new List<Enrollment>();

                // StudentID 로 Enrollment 조회
                var studentIds = courseEnrollments.Select(ce => ce.StudentID).ToHashSet();
                using var eRepo = new EnrollmentRepository(SchoolDatabase.DbPath);
                var all = await eRepo.GetByGradeAsync(
                    Settings.SchoolCode.Value, _loadedYear, _loadedSemester, course.Grade);

                return all.Where(e => studentIds.Contains(e.StudentID)).ToList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CoursePicker] 수강생 조회 오류: {ex.Message}");
            return new List<Enrollment>();
        }
    }
}

/// <summary>CoursePicker 변경 이벤트 인자 — 수강생 목록 포함</summary>
public sealed class CoursePickerChangedEventArgs : EventArgs
{
    public Course   Course   { get; init; } = null!;
    public string?  Room     { get; init; }   // null = 강의실 없는 과목
    public IReadOnlyList<Enrollment> Students { get; init; } = Array.Empty<Enrollment>();
}
