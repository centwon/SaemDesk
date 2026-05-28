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
/// 좌측 학급 학생 ↔ 우측 수강생, 일괄 배치, 강의실 필터.
/// </summary>
public partial class CourseEnrollmentDialog : Window
{
    // ── InfoBar 메시지 수준 ──────────────────────────────
    private enum InfoLevel { Success, Warning, Error }

    // ── 데이터 ──────────────────────────────────────────
    private readonly Course                     _course;
    private readonly List<Enrollment>           _all        = new();
    private readonly List<CourseEnrollment>     _original   = new();
    private readonly Dictionary<string, string> _enrolledRooms = new();
    private readonly Dictionary<string, string> _toAdd      = new();
    private readonly HashSet<string>            _toRemove   = new();

    private bool _loaded;

    public bool IsSuccess { get; private set; }

    // ── 생성자 ──────────────────────────────────────────

    public CourseEnrollmentDialog(Course course)
    {
        InitializeComponent();
        _course = course;
        Title   = $"수강생 관리 — {course.Subject}";
        Opened += async (_, _) => await OnOpenedAsync();
    }

    // ── 초기화 ──────────────────────────────────────────

    private async Task OnOpenedAsync()
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
                var s = await svc.GetEnrollmentsAsync(
                    Settings.SchoolCode.Value, Settings.WorkYear.Value, 0, g);
                _all.AddRange(s);
            }
        }
        else
        {
            var s = await svc.GetEnrollmentsAsync(
                Settings.SchoolCode.Value, Settings.WorkYear.Value, 0, _course.Grade);
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

        // 동아리(전 학년) 또는 학년이 2개 이상일 때만 '전체' 추가
        if (_course.EffectiveType == CourseTypes.Club || grades.Count > 1)
            items.Add(new ComboBoxItem { Content = "전체", Tag = 0 });

        foreach (var g in grades)
            items.Add(new ComboBoxItem { Content = $"{g}학년", Tag = g });

        CBoxGradeFilter.ItemsSource   = items;
        CBoxGradeFilter.SelectedIndex = _course.EffectiveType == CourseTypes.Club ? 0
            : Math.Max(0, items.FindIndex(i => i.Tag is int t && t == _course.Grade));

        RefreshClassFilter();
    }

    private void RefreshClassFilter()
    {
        int g       = GetGradeFilter();
        var classes = _all
            .Where(s => g == 0 || s.Grade == g)
            .Select(s => s.Class).Distinct().OrderBy(c => c).ToList();

        // '전체' 없이 실제 반 목록만
        var items = classes
            .Select(c => new ComboBoxItem { Content = $"{c}반", Tag = (object)c })
            .ToList();

        CBoxClassFilter.ItemsSource   = items;
        CBoxClassFilter.SelectedIndex = items.Count > 0 ? 0 : -1;
    }

    private void InitRightFilters()
    {
        // '전체 강의실' 없이 과목의 실제 강의실 목록만
        var items = _course.RoomList
            .Select(room => new ComboBoxItem { Content = room, Tag = (object)room })
            .ToList();

        CBoxRoomFilter.ItemsSource   = items;
        CBoxRoomFilter.SelectedIndex = items.Count > 0 ? 0 : -1;
    }

    private void InitBulkPanel()
    {
        if (!_course.IsClassType) return;

        BulkAssignPanel.IsVisible = true;

        var classes  = _all
            .Where(s => s.Grade == _course.Grade)
            .Select(s => s.Class).Distinct().OrderBy(c => c).ToList();
        var examples = classes.Take(3).Select(c => $"{_course.Grade}-{c}").ToList();
        string ex    = string.Join(", ", examples) + (classes.Count > 3 ? ", …" : "");
        TxtBulkDesc.Text = $"{_course.Grade}학년 전체 학생을 학급별 강의실로 배정 (예: {ex})";
    }

    // ── 목록 갱신 ────────────────────────────────────────

    private void RefreshLists()
    {
        var enrolledIds = _enrolledRooms.Keys
            .Union(_toAdd.Keys)
            .Except(_toRemove)
            .ToHashSet();

        int gf     = GetGradeFilter();
        int cf     = GetClassFilter();
        string rf  = GetRoomFilter();
        string kw  = TxtSearch.Text?.Trim().ToLower() ?? "";

        // 좌측: 미등록 학생
        var avail = _all
            .Where(s => !enrolledIds.Contains(s.StudentID))
            .Where(s => gf == 0 || s.Grade == gf)
            .Where(s => cf == 0 || s.Class  == cf)
            .OrderBy(s => s.Grade).ThenBy(s => s.Class).ThenBy(s => s.Number)
            .ToList();

        ListAvailable.LoadStudents(avail);
        EmptyAvailable.IsVisible = avail.Count == 0;

        // 우측: 등록된 학생
        var reg = _all
            .Where(s => enrolledIds.Contains(s.StudentID))
            .Where(s => string.IsNullOrEmpty(rf) || GetRoom(s.StudentID) == rf)
            .Where(s => string.IsNullOrEmpty(kw)  || s.Name.ToLower().Contains(kw))
            .OrderBy(s => s.Grade).ThenBy(s => s.Class).ThenBy(s => s.Number)
            .ToList();

        ListEnrolled.LoadStudents(reg);
        EmptyEnrolled.IsVisible = reg.Count == 0;

        TxtAvailableCount.Text  = $"({avail.Count}명)";
        TxtRegisteredCount.Text = $"({reg.Count}명)";
        TxtEnrolledCount.Text   = $"등록: {enrolledIds.Count}명";
    }

    private string GetRoom(string id)
    {
        if (_toAdd.TryGetValue(id, out var r))        return r;
        if (_enrolledRooms.TryGetValue(id, out var r2)) return r2;
        return "";
    }

    // ── 필터 이벤트 ──────────────────────────────────────

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

    // ── 추가 / 제거 ──────────────────────────────────────

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

    // ── 일괄 배치 (Step 3: 결과 메시지 상세화) ───────────

    private void BtnBulkAssign_Click(object? sender, RoutedEventArgs e)
    {
        var students = _all.Where(s => s.Grade == _course.Grade).ToList();
        if (!students.Any())
        {
            ShowInfo("배정할 학생이 없습니다.", InfoLevel.Warning);
            return;
        }

        int newCount      = 0;
        int updatedCount  = 0;
        var classSet      = new HashSet<int>();

        foreach (var s in students)
        {
            string room = $"{s.Grade}-{s.Class}";
            classSet.Add(s.Class);
            _toRemove.Remove(s.StudentID);

            if (_original.Any(o => o.StudentID == s.StudentID))
            {
                // 기존 등록 — 강의실만 갱신
                if (_enrolledRooms.TryGetValue(s.StudentID, out var existing) && existing != room)
                {
                    _enrolledRooms[s.StudentID] = room;
                    updatedCount++;
                }
            }
            else
            {
                _toAdd[s.StudentID] = room;
                newCount++;
            }
        }

        RefreshLists();

        var roomList = string.Join(", ",
            classSet.OrderBy(c => c).Select(c => $"{_course.Grade}-{c}"));
        ShowInfo(
            $"{_course.Grade}학년 전체 {students.Count}명 배치 완료 ({roomList})" +
            $" — 신규: {newCount}명, 강의실 변경: {updatedCount}명",
            InfoLevel.Success);
    }

    // ── 저장 (Step 4: BulkCreateAsync 활용) ─────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var repo = new CourseEnrollmentRepository(SchoolDatabase.DbPath);

            // 1. 신규 등록 — BulkCreateAsync 로 트랜잭션 일괄 처리
            if (_toAdd.Count > 0)
            {
                var now  = DateTime.Now;
                var bulk = _toAdd.Select(kvp => new CourseEnrollment
                {
                    StudentID = kvp.Key,
                    CourseNo  = _course.No,
                    Status    = CourseEnrollmentStatus.Active,
                    Room      = kvp.Value,
                    CreatedAt = now,
                    UpdatedAt = now,
                }).ToList();

                await repo.BulkCreateAsync(bulk);
            }

            // 2. 등록 해제
            foreach (var id in _toRemove)
            {
                var orig = _original.FirstOrDefault(o => o.StudentID == id);
                if (orig is not null)
                    await repo.DeleteAsync(orig.No);
            }

            // 3. 강의실 변경 (기존 등록 학생)
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
            ShowInfo($"저장 실패: {ex.Message}", InfoLevel.Error);
            Debug.WriteLine($"[CourseEnrollment] Save: {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    // ── InfoBar 헬퍼 (Step 2: 3단계 Severity) ───────────

    private void ShowInfo(string msg, InfoLevel level)
    {
        InfoBar.IsVisible  = true;
        InfoBarText.Text   = msg;

        (InfoBar.Background, InfoBarText.Foreground) = level switch
        {
            InfoLevel.Success => (SolidColorBrush.Parse("#E8F5E9"), SolidColorBrush.Parse("#2E7D32")),
            InfoLevel.Warning => (SolidColorBrush.Parse("#FFF3E0"), SolidColorBrush.Parse("#E65100")),
            _                 => (SolidColorBrush.Parse("#FFEBEE"), SolidColorBrush.Parse("#C62828")),
        };
    }

    // ── 헬퍼 ────────────────────────────────────────────

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
        if (CBoxRoomFilter.SelectedItem is ComboBoxItem i && i.Tag != null)
            return i.Tag.ToString() ?? "";
        return "";
    }
}
