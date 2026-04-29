using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 수업 시간표 배치 다이얼로그.
/// 원본: NewSchool.Dialogs.CourseScheduleDialog (WinUI3 ContentDialog).
/// Lesson 테이블에서 정기 수업을 로드하고 요일/교시/강의실 추가/삭제 후 저장.
/// </summary>
public partial class CourseScheduleDialog : Window
{
    private readonly Course                      _course;
    private readonly ObservableCollection<Lesson> _lessons = new();

    public bool IsSuccess { get; private set; }

    public CourseScheduleDialog(Course course)
    {
        InitializeComponent();
        _course = course;

        ScheduleList.ItemsSource = _lessons;
        LoadCourseInfo();
        Opened += async (_, _) => await LoadSchedulesAsync();
    }

    // ────────────────────────────────────────────────────
    //  로드
    // ────────────────────────────────────────────────────

    private void LoadCourseInfo()
    {
        TxtCourseName.Text = _course.Subject;
        TxtCourseInfo.Text = $"{_course.Grade}학년 · {_course.TypeDisplay} · 주당 {_course.Unit}시간";

        // 강의실 목록 추가
        CBoxRoom.Items.Clear();
        foreach (var room in _course.RoomList ?? new System.Collections.Generic.List<string>())
            CBoxRoom.Items.Add(room);
    }

    private async Task LoadSchedulesAsync()
    {
        try
        {
            using var repo = new LessonRepository(SchoolDatabase.DbPath);
            var list = (await repo.GetByCourseAsync(_course.No))
                .Where(l => l.IsRecurring)
                .OrderBy(l => l.DayOfWeek).ThenBy(l => l.Period)
                .ToList();

            _lessons.Clear();
            foreach (var l in list) _lessons.Add(l);
            UpdateUI();
        }
        catch (Exception ex)
        {
            ShowError($"시간표 조회 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  추가 / 제거
    // ────────────────────────────────────────────────────

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        ErrorBar.IsVisible = false;

        if (CBoxDay.SelectedItem is null    ) { ShowError("요일을 선택해 주세요.");    return; }
        if (CBoxPeriod.SelectedItem is null ) { ShowError("교시를 선택해 주세요.");    return; }
        if (string.IsNullOrWhiteSpace(CBoxRoom.Text)) { ShowError("강의실을 입력하세요."); return; }

        int day    = int.Parse(((ComboBoxItem)CBoxDay.SelectedItem).Tag!.ToString()!);
        int period = int.Parse(((ComboBoxItem)CBoxPeriod.SelectedItem).Tag!.ToString()!);

        if (_lessons.Any(l => l.DayOfWeek == day && l.Period == period))
        {
            ShowError("이미 추가된 시간표입니다.");
            return;
        }

        var lesson = new Lesson
        {
            Course      = _course.No,
            Teacher     = _course.TeacherID,
            Year        = _course.Year,
            Semester    = _course.Semester,
            DayOfWeek   = day,
            Period      = period,
            Grade       = _course.Grade,
            Room        = CBoxRoom.Text.Trim(),
            IsRecurring = true,
        };

        _lessons.Add(lesson);

        // 정렬
        var sorted = _lessons.OrderBy(l => l.DayOfWeek).ThenBy(l => l.Period).ToList();
        _lessons.Clear();
        foreach (var item in sorted) _lessons.Add(item);

        CBoxDay.SelectedItem    = null;
        CBoxPeriod.SelectedItem = null;
        CBoxRoom.Text           = string.Empty;

        UpdateUI();
    }

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Lesson lesson)
        {
            _lessons.Remove(lesson);
            UpdateUI();
        }
    }

    // ────────────────────────────────────────────────────
    //  저장 / 취소
    // ────────────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var repo = new LessonRepository(SchoolDatabase.DbPath);

            await repo.DeleteByCourseAsync(_course.No);

            foreach (var lesson in _lessons)
            {
                lesson.Course      = _course.No;
                lesson.Teacher     = _course.TeacherID;
                lesson.Year        = _course.Year;
                lesson.Semester    = _course.Semester;
                lesson.Grade       = _course.Grade;
                lesson.IsRecurring = true;
                await repo.CreateAsync(lesson);
            }

            IsSuccess = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"저장 오류: {ex.Message}");
            Debug.WriteLine($"[CourseSchedule] Save: {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    // ────────────────────────────────────────────────────
    //  UI 헬퍼
    // ────────────────────────────────────────────────────

    private void UpdateUI()
    {
        bool has = _lessons.Count > 0;
        EmptyState.IsVisible   = !has;
        ScheduleList.IsVisible = has;
    }

    private void ShowError(string msg)
    {
        ErrorBar.IsVisible = true;
        ErrorBarText.Text  = msg;
    }
}
