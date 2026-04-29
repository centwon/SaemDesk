using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 수강생 관리 다이얼로그.
/// 원본: NewSchool.Dialogs.CourseEnrollmentDialog (WinUI3 ContentDialog).
/// 좌측 학급 학생 ↔ 우측 수강생, 일괄 배치, 강의실 필터.
/// </summary>
public partial class CourseEnrollmentDialog : Window
{
    private readonly Course              _course;
    private readonly List<Enrollment>    _all         = new();
    private readonly List<CourseEnrollment> _original  = new();
    private readonly Dictionary<string, string> _enrolledRooms = new();
    private readonly Dictionary<string, string> _toAdd   = new();
    private readonly HashSet<string>     _toRemove    = new();

    private bool _loaded;

    public bool IsSuccess { get; private set; }

    public CourseEnrollmentDialog(Course course)
    {
        InitializeComponent();
        _course = course;
        Title   = $"수강생 관리 — {course.Subject}";
        Opened += async (_, _) => await OnOpened();
    }

    // ────────────────────────────────────────────────────
    //  로드
    // ────────────────────────────────────────────────────

    private async Task OnOpened()
    {
        InitCourseInfo();
        await LoadDataAsync();
        InitLeftFilters();
        InitRightFilters();
        InitBulkPanel();
        _loaded = true;
        RefreshLists();
    }

    private void InitCourseInfo()
    {
        TxtSubject.Text   = _course.Subject;
        TxtType.Text      = _course.TypeDisplay;
        TxtGradeInfo.Text = $"{_course.Grade}학년";
    }

    private async Task LoadDataAsync()
    {
        _all.Clear();
        using var svc = new EnrollmentService();
        if (_course.EffectiveType == CourseTypes.Club)
        {
            for (int g = 1; g <= 3; g++)
            {
                var s = await svc.GetEnrollmentsAsync(Settings.SchoolCode.Value, Settings.WorkYear.Value, 0, g);
                _all.AddRange(s);
            }
        }
        else
        {
            var s = await svc.GetEnrollmentsAsync(Settings.SchoolCode.Value, Settings.WorkYear.Value, 0, _course.Grade);
            _all.AddRange(s);
        }

        _original.Clear();
        _enrolledRooms.Clear();
        using var repo = new CourseEnrollmentRepository(SchoolDatabase.DbPath);
        var enrolled = await repo.GetByCourseAsync(_course.No);
        foreach (var e in enrolled)
        {
            _original.Add(e);
            _enrolledRooms[e.StudentID] = e.Room ?? "";
        }
    }

    private void InitLeftFilters()
    {
        var grades = _all.Select(s => s.Grade).Distinct().OrderBy(g => g).ToList();
        var items  = new List<ComboBoxItem>();
        if (_course.EffectiveType == CourseTypes.Club || grades.Count > 1)
            items.Add(new ComboBoxItem { Content = "전체", Tag = 0 });
        foreach (var g in grades)
            items.Add(new ComboBoxItem { Content = $"{g}학년", Tag = g });
        CBoxGradeFilter.ItemsSource = items;
        CBoxGradeFilter.SelectedIndex = _course.EffectiveType == CourseTypes.Club ? 0
            : Math.Max(0, items.FindIndex(i => i.Tag is int t && t == _course.Grade));
        RefreshClassFilter();
    }

    private void RefreshClassFilter()
    {
        int g = GetGradeFilter();
        var classes = _all.Where(s => g == 0 || s.Grade == g).Select(s => s.Class)
                          .Distinct().OrderBy(c => c).ToList();
        var items = new List<ComboBoxItem> { new ComboBoxItem { Content = "전체", Tag = 0 } };
        foreach (var c in classes) items.Add(new ComboBoxItem { Content = $"{c}반", Tag = c });
        CBoxClassFilter.ItemsSource = items;
        CBoxClassFilter.SelectedIndex = 0;
    }

    private void InitRightFilters()
    {
        var items = new List<ComboBoxItem> { new ComboBoxItem { Content = "전체", Tag = "" } };
        foreach (var room in _course.RoomList ?? new List<string>())
            items.Add(new ComboBoxItem { Content = room, Tag = room });
        CBoxRoomFilter.ItemsSource = items;
        CBoxRoomFilter.SelectedIndex = 0;
    }

    private void InitBulkPanel()
    {
        if (!_course.IsClassType) return;
        BulkAssignPanel.IsVisible = true;
        var classes = _all.Where(s => s.Grade == _course.Grade).Select(s => s.Class)
                          .Distinct().OrderBy(c => c).ToList();
        var examples = classes.Take(3).Select(c => $"{_course.Grade}-{c}").ToList();
        string ex    = string.Join(", ", examples) + (classes.Count > 3 ? ", ..." : "");
        TxtBulkDesc.Text = $"{_course.Grade}학년 전체 학생을 학급별 강의실로 배정 (예: {ex})";
    }

    // ────────────────────────────────────────────────────
    //  목록 갱신
    // ────────────────────────────────────────────────────

    private void RefreshLists()
    {
        var enrolledIds = _enrolledRooms.Keys.Union(_toAdd.Keys).Except(_toRemove).ToHashSet();
        int gf  = GetGradeFilter();
        int cf  = GetClassFilter();
        string rf   = GetRoomFilter();
        string search = TxtSearch.Text?.Trim().ToLower() ?? "";

        var avail = _all
            .Where(s => !enrolledIds.Contains(s.StudentID))
            .Where(s => gf == 0 || s.Grade == gf)
            .Where(s => cf == 0 || s.Class  == cf)
            .OrderBy(s => s.Grade).ThenBy(s => s.Class).ThenBy(s => s.Number).ToList();
        ListAvailable.LoadStudents(avail);

        var reg = _all
            .Where(s => enrolledIds.Contains(s.StudentID))
            .Where(s => string.IsNullOrEmpty(rf) || GetRoom(s.StudentID) == rf)
            .Where(s => string.IsNullOrEmpty(search) || s.Name.ToLower().Contains(search))
            .OrderBy(s => s.Grade).ThenBy(s => s.Class).ThenBy(s => s.Number).ToList();
        ListEnrolled.LoadStudents(reg);

        TxtAvailableCount.Text  = $"({avail.Count}명)";
        TxtRegisteredCount.Text = $"({reg.Count}명)";
        TxtEnrolledCount.Text   = $"등록: {enrolledIds.Count}명";
    }

    private string GetRoom(string id)
    {
        if (_toAdd.TryGetValue(id, out var r)) return r;
        if (_enrolledRooms.TryGetValue(id, out var r2)) return r2;
        return "";
    }

    // ────────────────────────────────────────────────────
    //  필터 이벤트
    // ────────────────────────────────────────────────────

    private void CBoxGradeFilter_SelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        RefreshClassFilter();
        RefreshLists();
    }

    private void CBoxClassFilter_SelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (_loaded) RefreshLists();
    }

    private void CBoxRoomFilter_SelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (_loaded) RefreshLists();
    }

    private void TxtSearch_TextChanged(object? s, TextChangedEventArgs e)
    {
        if (_loaded) RefreshLists();
    }

    // ────────────────────────────────────────────────────
    //  추가 / 제거
    // ────────────────────────────────────────────────────

    private void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        var selected = ListAvailable.GetSelectedStudents().ToList();
        if (!selected.Any()) return;
        string room = GetRoomFilter();
        foreach (var s in selected)
        {
            _toRemove.Remove(s.StudentID);
            if (!_original.Any(o => o.StudentID == s.StudentID))
                _toAdd[s.StudentID] = room;
        }
        ListAvailable.DeselectAll();
        RefreshLists();
    }

    private void BtnRemove_Click(object? sender, RoutedEventArgs e)
    {
        var selected = ListEnrolled.GetSelectedStudents().ToList();
        if (!selected.Any()) return;
        foreach (var s in selected)
        {
            _toAdd.Remove(s.StudentID);
            if (_original.Any(o => o.StudentID == s.StudentID))
                _toRemove.Add(s.StudentID);
        }
        ListEnrolled.DeselectAll();
        RefreshLists();
    }

    private void BtnBulkAssign_Click(object? sender, RoutedEventArgs e)
    {
        var students = _all.Where(s => s.Grade == _course.Grade).ToList();
        if (!students.Any()) { ShowInfo("배정할 학생이 없습니다.", false); return; }

        foreach (var s in students)
        {
            string room = $"{s.Grade}-{s.Class}";
            _toRemove.Remove(s.StudentID);
            if (_original.Any(o => o.StudentID == s.StudentID))
                _enrolledRooms[s.StudentID] = room;
            else
                _toAdd[s.StudentID] = room;
        }
        RefreshLists();
        ShowInfo($"{_course.Grade}학년 전체 {students.Count}명 배치 완료", true);
    }

    // ────────────────────────────────────────────────────
    //  저장 / 취소
    // ────────────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var repo = new CourseEnrollmentRepository(SchoolDatabase.DbPath);

            foreach (var kvp in _toAdd)
                await repo.CreateAsync(new CourseEnrollment
                {
                    StudentID = kvp.Key,
                    CourseNo  = _course.No,
                    Status    = CourseEnrollmentStatus.Active,
                    Room      = kvp.Value,
                });

            foreach (var id in _toRemove)
            {
                var orig = _original.FirstOrDefault(o => o.StudentID == id);
                if (orig is not null) await repo.DeleteAsync(orig.No);
            }

            foreach (var orig in _original)
            {
                if (_toRemove.Contains(orig.StudentID)) continue;
                if (_enrolledRooms.TryGetValue(orig.StudentID, out var newRoom)
                    && newRoom != (orig.Room ?? ""))
                {
                    orig.Room      = newRoom;
                    orig.UpdatedAt = DateTime.Now;
                    await repo.UpdateAsync(orig);
                }
            }

            IsSuccess = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowInfo($"저장 실패: {ex.Message}", false);
            Debug.WriteLine($"[CourseEnrollment] Save: {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    // ────────────────────────────────────────────────────
    //  헬퍼
    // ────────────────────────────────────────────────────

    private int GetGradeFilter()
    {
        if (CBoxGradeFilter.SelectedItem is ComboBoxItem i && i.Tag is int g) return g;
        return 0;
    }

    private int GetClassFilter()
    {
        if (CBoxClassFilter.SelectedItem is ComboBoxItem i && i.Tag is int c) return c;
        return 0;
    }

    private string GetRoomFilter()
    {
        if (CBoxRoomFilter.SelectedItem is ComboBoxItem i && i.Tag != null) return i.Tag.ToString() ?? "";
        return "";
    }

    private void ShowInfo(string msg, bool success)
    {
        InfoBar.IsVisible        = true;
        InfoBar.Background       = success
            ? SolidColorBrush.Parse("#E8F5E9")
            : SolidColorBrush.Parse("#FFF3E0");
        InfoBarText.Text         = msg;
        InfoBarText.Foreground   = success
            ? SolidColorBrush.Parse("#2E7D32")
            : SolidColorBrush.Parse("#E65100");
    }
}
