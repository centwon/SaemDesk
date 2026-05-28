using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 명렬표 HTML 테이블 삽입 다이얼로그.
/// 원본: NewSchool.Dialogs.RosterTableDialog (WinUI3 ContentDialog).
/// 학급/수업/동아리 기준으로 학생 명렬표를 HTML 테이블로 생성해 반환.
/// ClassPicker / CoursePicker 컨트롤을 재사용.
/// </summary>
public partial class RosterTableDialog : Window
{
    public string GeneratedHtml { get; private set; } = string.Empty;
    public string TableTitle    { get; private set; } = string.Empty;
    public bool   IsSuccess     { get; private set; }

    // ClassPicker / CoursePicker 의 마지막 이벤트 인자 캐시
    private ClassChangedEventArgs?  _lastClass;
    private CourseChangedEventArgs? _lastCourse;

    private List<Club> _clubs = new();

    public RosterTableDialog()
    {
        InitializeComponent();

        ScopeComboBox.SelectionChanged       += ScopeComboBox_SelectionChanged;
        TheYearSemesterPicker.YearSemesterChanged += OnYearSemesterChanged;
        TheClassPicker.ClassChanged              += OnClassChanged;
        TheCoursePicker.CourseChanged            += OnCourseChanged;
    }

    public void SetScope(string scopeType)
    {
        foreach (var obj in ScopeComboBox.Items)
            if (obj is ComboBoxItem ci && ci.Tag?.ToString() == scopeType)
            {
                ScopeComboBox.SelectedItem = ci;
                return;
            }
    }

    // ────────────────────────────────────────────────────
    //  스코프 전환
    // ────────────────────────────────────────────────────

    private async void ScopeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ScopeComboBox.SelectedItem is not ComboBoxItem sel) return;
        string scope = sel.Tag?.ToString() ?? "Class";

        ClassPanel.IsVisible  = scope == "Class";
        CoursePanel.IsVisible = scope == "Course";
        ClubPanel.IsVisible   = scope == "Club";

        try
        {
            // CoursePicker 는 Loaded 시 자동 초기화되므로 별도 로드 불필요.
            // 동아리만 지연 로드 유지.
            if (scope == "Club" && _clubs.Count == 0)
            {
                using var svc = new ClubService();
                _clubs = await svc.GetAllClubsAsync(Settings.SchoolCode.Value, Settings.WorkYear.Value);
                ClubComboBox.ItemsSource = _clubs;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[RosterTableDialog] ScopeChange: {ex.Message}"); }
    }

    private async void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        await TheCoursePicker.LoadAsync(e.Year, e.Semester);
    }

    // ────────────────────────────────────────────────────
    //  ClassPicker / CoursePicker 이벤트
    // ────────────────────────────────────────────────────

    private void OnClassChanged(object? sender, ClassChangedEventArgs e)
    {
        _lastClass = e;
        // 전체 반(Class=0) 일 때만 레이아웃 옵션 표시
        ClassLayoutPanel.IsVisible = e.IsAllClass;
    }

    private void OnCourseChanged(object? sender, CourseChangedEventArgs e)
    {
        _lastCourse = e;
        // IncludeAllRoom=true 일 때 강의실 "전체"(선택값 null)이면 레이아웃 패널 표시
        CourseLayoutPanel.IsVisible = e.Room == null && e.Course.RoomList.Count > 1;
    }

    // ────────────────────────────────────────────────────
    //  삽입 / 취소
    // ────────────────────────────────────────────────────

    private async void OnInsert(object? sender, RoutedEventArgs e)
    {
        ErrorBar.IsVisible = false;

        if (string.IsNullOrWhiteSpace(ColumnsBox.Text))
        {
            ShowError("컬럼을 입력하세요.");
            return;
        }

        try
        {
            string html = await GenerateTableAsync();
            if (string.IsNullOrEmpty(html)) return;

            GeneratedHtml = html;
            TableTitle    = TableTitleBox.Text?.Trim() ?? string.Empty;
            IsSuccess     = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"오류: {ex.Message}");
            Debug.WriteLine($"[RosterTableDialog] Insert: {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    // ────────────────────────────────────────────────────
    //  테이블 생성
    // ────────────────────────────────────────────────────

    private async Task<string> GenerateTableAsync()
    {
        string scope = (ScopeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Class";
        var columns  = (ColumnsBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => !string.IsNullOrEmpty(c)).ToList();

        if (columns.Count == 0) { ShowError("컬럼을 하나 이상 입력하세요."); return string.Empty; }

        string title = TableTitleBox.Text?.Trim() ?? string.Empty;

        switch (scope)
        {
            case "Class":
            {
                if (_lastClass == null) { ShowError("학급을 선택하세요."); return string.Empty; }
                // 전체 반 → 그룹 테이블
                if (_lastClass.IsAllClass)
                    return await GenerateClassGroupedAsync(title, columns);
                // 단일 반
                var students = _lastClass.Students
                    .OrderBy(e => e.Number).Select(e => (e.Number, e.Name)).ToList();
                if (!students.Any()) { ShowError("학생이 없습니다."); return string.Empty; }
                return BuildSingleTable(title,
                    $"{_lastClass.Year}학년도 {_lastClass.Grade}학년 {_lastClass.Class}반",
                    columns, students);
            }
            case "Course":
            {
                if (_lastCourse == null) { ShowError("수업을 선택하세요."); return string.Empty; }
                // 전체 강의실(Room==null이고 강의실 여러 개) → 그룹 테이블
                if (_lastCourse.Room == null && _lastCourse.Course.RoomList.Count > 1)
                    return await GenerateCourseGroupedAsync(title, columns);
                // 단일 수업
                string scopeLabel = $"{TheYearSemesterPicker.Year}학년도 {TheYearSemesterPicker.Semester}학기 {_lastCourse.Course.DisplayName}";
                if (_lastCourse.Room != null) scopeLabel += $" ({_lastCourse.Room})";
                if (_lastCourse.Course.IsClassType)
                {
                    // 학급형: 번호·이름만
                    var students = _lastCourse.Students
                        .OrderBy(e => e.Number).Select(e => (e.Number, e.Name)).ToList();
                    if (!students.Any()) { ShowError("수강생이 없습니다."); return string.Empty; }
                    return BuildSingleTable(title, scopeLabel, columns, students);
                }
                else
                {
                    // 이동수업(선택형): 학년·학급·번호·이름
                    var rows = _lastCourse.Students
                        .OrderBy(e => e.Grade).ThenBy(e => e.Class).ThenBy(e => e.Number)
                        .Select(e => (Lead: new[] { e.Grade.ToString(), e.Class.ToString() }, e.Number, e.Name))
                        .ToList();
                    if (!rows.Any()) { ShowError("수강생이 없습니다."); return string.Empty; }
                    return BuildGroupedVerticalTable(title, scopeLabel, new[] { "학년", "학급" }, rows, columns);
                }
            }
            case "Club":
            {
                if (ClubComboBox.SelectedItem is not Club club) { ShowError("동아리를 선택하세요."); return string.Empty; }
                var students = await LoadClubStudentsDetailedAsync(club.No);
                if (!students.Any()) { ShowError("부원이 없습니다."); return string.Empty; }
                var rows = students.Select(s => (Lead: new[] { s.Grade.ToString(), s.Class.ToString() }, s.Number, s.Name)).ToList();
                return BuildGroupedVerticalTable(title,
                    $"{Settings.WorkYear.Value}학년도 {club.ClubName}",
                    new[] { "학년", "학급" }, rows, columns);
            }
        }
        return string.Empty;
    }

    private async Task<string> GenerateClassGroupedAsync(string title, List<string> columns)
    {
        int grade = _lastClass!.Grade;
        var classMap = await LoadGradeStudentsAsync(grade);
        if (!classMap.Any()) { ShowError($"{grade}학년에 학생이 없습니다."); return string.Empty; }

        string scopeLabel = $"{_lastClass.Year}학년도 {grade}학년 전체";
        bool horizontal   = RbClassHorizontal.IsChecked == true;

        if (horizontal)
            return BuildGroupedHorizontalTable(title, scopeLabel, columns,
                classMap.ToDictionary(kv => $"{grade}학년 {kv.Key}반", kv => kv.Value));

        var rows = classMap.SelectMany(kv =>
            kv.Value.Select(s => (Lead: new[] { kv.Key.ToString() }, s.Number, s.Name))).ToList();
        return BuildGroupedVerticalTable(title, scopeLabel, new[] { "학급" }, rows, columns);
    }

    private async Task<string> GenerateCourseGroupedAsync(string title, List<string> columns)
    {
        var course = _lastCourse!.Course;
        var detailedRooms = await LoadCourseStudentsByRoomDetailedAsync(course.No);
        if (!detailedRooms.Any()) { ShowError("수강생이 없습니다."); return string.Empty; }

        string scopeLabel = $"{TheYearSemesterPicker.Year}학년도 {TheYearSemesterPicker.Semester}학기 {course.DisplayName} 전체";
        bool horizontal   = RbCourseHorizontal.IsChecked == true;

        if (horizontal)
            return BuildGroupedHorizontalTable(title, scopeLabel, columns,
                detailedRooms.ToDictionary(kv => kv.Key, kv => kv.Value.Select(s => (s.Number, s.Name)).ToList()));

        var rows = detailedRooms.SelectMany(kv =>
            kv.Value.Select(s => (Lead: new[] { kv.Key, s.Grade.ToString(), s.Class.ToString() }, s.Number, s.Name))).ToList();
        return BuildGroupedVerticalTable(title, scopeLabel, new[] { "강의실", "학년", "학급" }, rows, columns);
    }

    // ────────────────────────────────────────────────────
    //  학생 데이터 로드
    // ────────────────────────────────────────────────────

    private async Task<SortedDictionary<int, List<(int Number, string Name)>>> LoadGradeStudentsAsync(int grade)
    {
        using var svc = new EnrollmentService();
        var all = await svc.GetEnrollmentsAsync(Settings.SchoolCode.Value, Settings.WorkYear.Value);
        var result = new SortedDictionary<int, List<(int, string)>>();
        foreach (var e in all.Where(e => e.Grade == grade).OrderBy(e => e.Class).ThenBy(e => e.Number))
        {
            if (!result.ContainsKey(e.Class)) result[e.Class] = new();
            result[e.Class].Add((e.Number, e.Name));
        }
        return result;
    }

    private async Task<Dictionary<string, List<(int Grade, int Class, int Number, string Name)>>>
        LoadCourseStudentsByRoomDetailedAsync(int courseNo)
    {
        using var courseSvc = new CourseService();
        var ces = await courseSvc.GetCourseEnrollmentsAsync(
            Settings.SchoolCode.Value, Settings.WorkYear.Value, Settings.WorkSemester.Value, courseNo);
        var ids = ces.Select(c => c.StudentID).ToHashSet();
        using var enrollSvc = new EnrollmentService();
        var all = await enrollSvc.GetEnrollmentsAsync(Settings.SchoolCode.Value, Settings.WorkYear.Value);
        var map = all.Where(e => ids.Contains(e.StudentID)).ToDictionary(e => e.StudentID);
        var result = new Dictionary<string, List<(int Grade, int Class, int Number, string Name)>>();
        foreach (var ce in ces.OrderBy(c => c.Room))
        {
            string room = string.IsNullOrWhiteSpace(ce.Room) ? "미지정" : ce.Room;
            if (!result.ContainsKey(room)) result[room] = new();
            if (map.TryGetValue(ce.StudentID, out var s)) result[room].Add((s.Grade, s.Class, s.Number, s.Name));
        }
        foreach (var list in result.Values)
            list.Sort((a, b) => { int c = a.Grade.CompareTo(b.Grade); if (c != 0) return c; c = a.Class.CompareTo(b.Class); return c != 0 ? c : a.Number.CompareTo(b.Number); });
        return result;
    }

    private async Task<List<(int Grade, int Class, int Number, string Name)>> LoadClubStudentsDetailedAsync(int clubNo)
    {
        using var repo = new ClubEnrollmentRepository(SchoolDatabase.DbPath);
        var ces = await repo.GetByClubAsync(clubNo);
        var ids = ces.Select(c => c.StudentID).ToHashSet();
        using var svc = new EnrollmentService();
        var all = await svc.GetEnrollmentsAsync(Settings.SchoolCode.Value, Settings.WorkYear.Value);
        return all.Where(e => ids.Contains(e.StudentID)).OrderBy(e => e.Grade).ThenBy(e => e.Class).ThenBy(e => e.Number)
                  .Select(e => (e.Grade, e.Class, e.Number, e.Name)).ToList();
    }

    // ────────────────────────────────────────────────────
    //  HTML 빌더
    // ────────────────────────────────────────────────────

    private static string BuildSingleTable(string title, string scope, List<string> cols, List<(int Number, string Name)> students)
    {
        int total = 2 + cols.Count;
        var sb = new StringBuilder();
        sb.Append("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;width:100%;text-align:center;\">");
        if (!string.IsNullOrEmpty(title)) sb.Append($"<tr><th colspan=\"{total}\" style=\"font-size:16px;padding:8px;background:#e8f0fe;\">{Esc(title)}</th></tr>");
        sb.Append($"<tr><th colspan=\"{total}\" style=\"font-size:13px;padding:6px;background:#f8f9fa;text-align:right;\">{Esc(scope)}</th></tr>");
        AppendHeader(sb, cols);
        foreach (var (n, nm) in students) AppendRow(sb, n, nm, cols.Count);
        sb.Append("</table>");
        return sb.ToString();
    }

    private static string BuildGroupedVerticalTable(string title, string scope, string[] leads, List<(string[] Lead, int Number, string Name)> rows, List<string> cols)
    {
        int total = leads.Length + 2 + cols.Count;
        var sb = new StringBuilder();
        sb.Append("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;width:100%;text-align:center;\">");
        if (!string.IsNullOrEmpty(title)) sb.Append($"<tr><th colspan=\"{total}\" style=\"font-size:16px;padding:8px;background:#e8f0fe;\">{Esc(title)}</th></tr>");
        sb.Append($"<tr><th colspan=\"{total}\" style=\"font-size:13px;padding:6px;background:#f8f9fa;text-align:right;\">{Esc(scope)}</th></tr>");
        sb.Append("<tr style=\"background:#d0e0f0;\">");
        foreach (var h in leads) sb.Append($"<th style=\"width:90px;\">{Esc(h)}</th>");
        sb.Append("<th style=\"width:50px;\">번호</th><th style=\"width:80px;\">이름</th>");
        foreach (var c in cols) sb.Append($"<th>{Esc(c)}</th>");
        sb.Append("</tr>");
        foreach (var (lead, n, nm) in rows)
        {
            sb.Append("<tr>");
            foreach (var v in lead) sb.Append($"<td>{Esc(v)}</td>");
            sb.Append($"<td>{n}</td><td>{Esc(nm)}</td>");
            for (int i = 0; i < cols.Count; i++) sb.Append("<td></td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    private static string BuildGroupedHorizontalTable(string title, string scope, List<string> cols, Dictionary<string, List<(int Number, string Name)>> groups)
    {
        var gKeys = groups.Keys.ToList();
        int perGroup = 2 + cols.Count;
        int total    = perGroup * gKeys.Count;
        int maxRows  = groups.Values.Max(s => s.Count);
        var sb = new StringBuilder();
        sb.Append("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;width:100%;text-align:center;\">");
        if (!string.IsNullOrEmpty(title)) sb.Append($"<tr><th colspan=\"{total}\" style=\"font-size:16px;padding:8px;background:#e8f0fe;\">{Esc(title)}</th></tr>");
        sb.Append($"<tr><th colspan=\"{total}\" style=\"font-size:13px;padding:6px;background:#f8f9fa;text-align:right;\">{Esc(scope)}</th></tr>");
        sb.Append("<tr>");
        foreach (var g in gKeys) sb.Append($"<th colspan=\"{perGroup}\" style=\"font-size:14px;padding:6px;background:#fff3cd;\">{Esc(g)}</th>");
        sb.Append("</tr><tr style=\"background:#d0e0f0;\">");
        foreach (var _ in gKeys)
        {
            sb.Append("<th style=\"width:40px;\">번호</th><th style=\"width:60px;\">이름</th>");
            foreach (var c in cols) sb.Append($"<th>{Esc(c)}</th>");
        }
        sb.Append("</tr>");
        for (int row = 0; row < maxRows; row++)
        {
            sb.Append("<tr>");
            foreach (var g in gKeys)
            {
                var list = groups[g];
                if (row < list.Count)
                {
                    sb.Append($"<td>{list[row].Number}</td><td>{Esc(list[row].Name)}</td>");
                    for (int i = 0; i < cols.Count; i++) sb.Append("<td></td>");
                }
                else for (int i = 0; i < perGroup; i++) sb.Append("<td></td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, List<string> cols)
    {
        sb.Append("<tr style=\"background:#d0e0f0;\"><th style=\"width:50px;\">번호</th><th style=\"width:80px;\">이름</th>");
        foreach (var c in cols) sb.Append($"<th>{Esc(c)}</th>");
        sb.Append("</tr>");
    }

    private static void AppendRow(StringBuilder sb, int n, string nm, int colCount)
    {
        sb.Append($"<tr><td>{n}</td><td>{Esc(nm)}</td>");
        for (int i = 0; i < colCount; i++) sb.Append("<td></td>");
        sb.Append("</tr>");
    }

    private static string Esc(string t) => t.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private void ShowError(string msg) { ErrorText.Text = msg; ErrorBar.IsVisible = true; }
}
