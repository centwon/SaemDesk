using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaemDesk.Helpers;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생부 특기사항 일괄 입력 창.
/// 원본: NewSchool.Dialogs.StudentSpecBatchDialog (WinUI3 Window).
/// 구성: 좌측 ListStudent + 우측 에디터 + 누가기록 참조 패널 + 초안 자동 생성.
/// </summary>
public partial class StudentSpecBatchDialog : Window
{
    // ────────────────────────────────────────────────────
    //  Fields
    // ────────────────────────────────────────────────────

    private readonly int _year;
    private readonly int _semester;
    private readonly int _grade;
    private readonly int _classNum;

    private string _selectedType = string.Empty;
    private Enrollment? _currentStudent;
    private string _currentOriginalContent = string.Empty;
    private bool _isRefPanelOpen;

    private readonly Dictionary<string, StudentSpecial> _specCache  = new();
    private readonly Dictionary<string, List<string>>   _logCache   = new();
    private readonly HashSet<string>                    _modified   = new();

    public ObservableCollection<Course> Courses { get; } = new();

    // ────────────────────────────────────────────────────
    //  Constructor
    // ────────────────────────────────────────────────────

    public StudentSpecBatchDialog(int year, int semester, int grade, int classNum, string? defaultType = null)
    {
        InitializeComponent();
        DataContext = this;

        _year = year; _semester = semester; _grade = grade; _classNum = classNum;
        Title           = $"학생부 특기사항 일괄 입력 — {grade}학년 {classNum}반";
        TxtClassInfo.Text = $"{grade}학년 {classNum}반";

        InitTypeCombo(defaultType);
        StudentList.ShowCheckBox  = false;
        StudentList.StudentSelected += OnStudentSelected;

        Closed += (_, _) => StudentList.StudentSelected -= OnStudentSelected;

        _ = LoadStudentsAsync();
        _ = LoadCoursesAsync();
    }

    // ────────────────────────────────────────────────────
    //  초기화
    // ────────────────────────────────────────────────────

    private void InitTypeCombo(string? defaultType)
    {
        var types = new List<string>
        {
            "교과활동", "개인별세특", "자율활동", "동아리활동", "진로활동", "종합의견"
        };
        CBoxType.ItemsSource = types;
        CBoxType.SelectedIndex = types.Contains(defaultType ?? "") ? types.IndexOf(defaultType!) : 0;
    }

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private async Task LoadStudentsAsync()
    {
        try
        {
            using var svc = new EnrollmentService();
            var list = (await svc.GetClassRosterAsync(
                Settings.SchoolCode.Value, _year, _grade, _classNum))
                .OrderBy(s => s.Number).ToList();

            StudentList.LoadStudents(list);
            TxtClassInfo.Text = $"{_grade}학년 {_classNum}반 ({list.Count}명)";

            if (list.Count > 0) StudentList.SelectStudent(list[0].StudentID);
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentSpecBatch] LoadStudents: {ex.Message}"); }
    }

    private async Task LoadCoursesAsync()
    {
        try
        {
            using var svc = new CourseService();
            var list = await svc.GetMyCoursesAsync();
            Courses.Clear();
            foreach (var c in list) Courses.Add(c);
            if (Courses.Count > 0) CBoxCourse.SelectedIndex = 0;
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentSpecBatch] LoadCourses: {ex.Message}"); }
    }

    private async Task<StudentSpecial> GetOrLoadSpecAsync(string studentId)
    {
        if (_specCache.TryGetValue(studentId, out var cached)) return cached;

        using var svc  = new StudentSpecialService();
        var list       = await svc.GetByStudentAsync(studentId, _year);
        StudentSpecial? spec;

        if (_selectedType is "교과활동" or "개인별세특")
        {
            string subject = (CBoxCourse.SelectedItem as Course)?.Subject ?? "";
            spec = list.FirstOrDefault(s => s.Type == _selectedType && s.SubjectName == subject);
        }
        else
            spec = list.FirstOrDefault(s => s.Type == _selectedType);

        spec ??= CreateEmpty(studentId);
        _specCache[studentId] = spec;
        return spec;
    }

    private StudentSpecial CreateEmpty(string studentId)
    {
        var course = CBoxCourse.SelectedItem as Course;
        bool isCourse = _selectedType is "교과활동" or "개인별세특";
        return new StudentSpecial
        {
            StudentID   = studentId,
            Year        = _year,
            Type        = _selectedType,
            Date        = DateTime.Now.ToString("yyyy-MM-dd"),
            TeacherID   = Settings.User.Value,
            CourseNo    = isCourse ? (course?.No ?? 0) : 0,
            SubjectName = isCourse ? (course?.Subject ?? "") : "",
        };
    }

    // ────────────────────────────────────────────────────
    //  학생 선택
    // ────────────────────────────────────────────────────

    private async void OnStudentSelected(object? sender, Enrollment student)
    {
        SaveCurrentToCache();
        _currentStudent = student;

        TxtStudentName.Text = student.Name;
        TxtStudentNum.Text  = $"{student.Number}번";

        var spec = await GetOrLoadSpecAsync(student.StudentID);
        _currentOriginalContent = spec.Content ?? "";
        TxtContent.Text    = spec.Content ?? "";
        TxtContent.IsEnabled = !spec.IsFinalized;
        IconModified.IsVisible = _modified.Contains(student.StudentID);

        UpdateByteInfo();
        UpdateProgress();

        if (_isRefPanelOpen) await LoadLogDraftsAsync(student.StudentID);
    }

    private void SaveCurrentToCache()
    {
        if (_currentStudent is null) return;
        if (!_specCache.TryGetValue(_currentStudent.StudentID, out var spec)) return;
        string newContent = TxtContent.Text ?? "";
        spec.Content = newContent;
        if (newContent != _currentOriginalContent) _modified.Add(_currentStudent.StudentID);
    }

    // ────────────────────────────────────────────────────
    //  필터 변경
    // ────────────────────────────────────────────────────

    private void CBoxType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CBoxType.SelectedItem is not string type) return;
        SaveCurrentToCache();
        _selectedType = type;
        bool showCourse = type is "교과활동" or "개인별세특";
        CoursePanel.IsVisible = showCourse;
        ClearCache();
        if (_currentStudent is not null) _ = ReloadCurrentAsync();
    }

    private void CBoxCourse_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SaveCurrentToCache();
        ClearCache();
        if (_currentStudent is not null) _ = ReloadCurrentAsync();
    }

    private void ClearCache()
    {
        _specCache.Clear();
        _modified.Clear();
        _logCache.Clear();
        _currentOriginalContent = "";
    }

    private async Task ReloadCurrentAsync()
    {
        if (_currentStudent is null) return;
        var spec = await GetOrLoadSpecAsync(_currentStudent.StudentID);
        _currentOriginalContent = spec.Content ?? "";
        TxtContent.Text = spec.Content ?? "";
        TxtContent.IsEnabled = !spec.IsFinalized;
        IconModified.IsVisible = false;
        UpdateByteInfo();
        UpdateProgress();
    }

    // ────────────────────────────────────────────────────
    //  에디터 변경
    // ────────────────────────────────────────────────────

    private void TxtContent_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateByteInfo();
        if (_currentStudent is not null)
            IconModified.IsVisible = TxtContent.Text != _currentOriginalContent;
    }

    // ────────────────────────────────────────────────────
    //  저장 / 닫기
    // ────────────────────────────────────────────────────

    private async void BtnSaveAll_Click(object? sender, RoutedEventArgs e)
    {
        SaveCurrentToCache();
        if (_modified.Count == 0) { TxtSaveStatus.Text = "변경된 항목이 없습니다."; return; }

        try
        {
            BtnSaveAll_Click_Inner();  // just to avoid direct await in expression
            using var svc  = new StudentSpecialService();
            int saved = 0, fail = 0;

            foreach (var id in _modified.ToList())
            {
                if (!_specCache.TryGetValue(id, out var spec)) continue;
                try
                {
                    if (spec.No > 0) await svc.UpdateAsync(spec);
                    else spec.No = await svc.CreateAsync(spec);
                    saved++;
                }
                catch { fail++; }
            }

            _modified.Clear();
            if (_currentStudent is not null && _specCache.TryGetValue(_currentStudent.StudentID, out var cur))
                _currentOriginalContent = cur.Content ?? "";
            IconModified.IsVisible = false;
            UpdateProgress();
            TxtSaveStatus.Text = fail > 0 ? $"{saved}건 저장, {fail}건 실패" : $"{saved}건 저장 완료";
        }
        catch (Exception ex) { TxtSaveStatus.Text = $"저장 실패: {ex.Message}"; }
    }

    // 단순 호출 분리용 — 실제 저장은 위에서
    private static void BtnSaveAll_Click_Inner() { }

    private async void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        SaveCurrentToCache();
        if (_modified.Count > 0)
        {
            var dlg = new ConfirmDialog($"{_modified.Count}건의 미저장 변경사항이 있습니다. 저장하고 닫을까요?", "저장 후 닫기", "그냥 닫기");
            bool save = await dlg.ShowDialogAsync(this);
            if (save) BtnSaveAll_Click(sender, new RoutedEventArgs());
        }
        Close();
    }

    private void BtnSpellCheck_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName  = "https://nara-speller.co.kr/speller",
                UseShellExecute = true
            });
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentSpecBatch] SpellCheck: {ex.Message}"); }
    }

    // ────────────────────────────────────────────────────
    //  누가기록 참조 + 초안 자동 생성
    // ────────────────────────────────────────────────────

    private async void BtnToggleRef_Changed(object? sender, RoutedEventArgs e)
    {
        _isRefPanelOpen  = BtnToggleRef.IsChecked == true;
        RefPanel.IsVisible = _isRefPanelOpen;
        if (_isRefPanelOpen && _currentStudent is not null)
            await LoadLogDraftsAsync(_currentStudent.StudentID);
    }

    private async Task LoadLogDraftsAsync(string studentId)
    {
        try
        {
            if (!_logCache.TryGetValue(studentId, out var drafts))
            {
                using var svc = new StudentLogService();
                var logs = await svc.GetStudentLogsAsync(studentId, _year);

                if (!string.IsNullOrEmpty(_selectedType) && _selectedType != "전체")
                    if (Enum.TryParse<LogCategory>(_selectedType, out var cat))
                        logs = logs.Where(l => l.Category == cat).ToList();

                drafts = logs
                    .Where(l => !string.IsNullOrWhiteSpace(l.DraftSummary))
                    .Select(l => l.DraftSummary!)
                    .ToList();
                _logCache[studentId] = drafts;
            }

            TxtLogReference.Text = drafts.Count > 0
                ? string.Join("\n\n", drafts.Select((d, i) => $"[{i + 1}] {d}"))
                : "해당 영역의 누가기록 초안이 없습니다.";
        }
        catch (Exception ex)
        {
            TxtLogReference.Text = $"로드 실패: {ex.Message}";
        }
    }

    private async void BtnAutoGenerate_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentStudent is null) { TxtSaveStatus.Text = "학생을 먼저 선택하세요."; return; }

        if (!string.IsNullOrWhiteSpace(TxtContent.Text))
        {
            var dlg = new ConfirmDialog("기존 내용 뒤에 추가할까요? (취소하면 덮어쓰기)", "뒤에 추가", "덮어쓰기");
            bool append = await dlg.ShowDialogAsync(this);
            var draft = await GenerateDraftAsync(_currentStudent.StudentID);
            if (string.IsNullOrEmpty(draft)) { TxtSaveStatus.Text = "생성할 초안이 없습니다."; return; }
            TxtContent.Text = append ? TxtContent.Text.TrimEnd() + " " + draft : draft;
        }
        else
        {
            var draft = await GenerateDraftAsync(_currentStudent.StudentID);
            if (string.IsNullOrEmpty(draft)) { TxtSaveStatus.Text = "생성할 초안이 없습니다."; return; }
            TxtContent.Text = draft;
        }
        TxtSaveStatus.Text = "초안이 생성되었습니다. 확인 후 저장하세요.";
    }

    private async Task<string> GenerateDraftAsync(string studentId)
    {
        if (!_logCache.TryGetValue(studentId, out var drafts))
        {
            await LoadLogDraftsAsync(studentId);
            _logCache.TryGetValue(studentId, out drafts);
        }
        if (drafts is null || drafts.Count == 0) return string.Empty;
        return string.Join(" ", drafts);
    }

    // ────────────────────────────────────────────────────
    //  UI 헬퍼
    // ────────────────────────────────────────────────────

    private void UpdateByteInfo()
    {
        string text    = TxtContent.Text ?? "";
        int current    = NeisHelper.CountByte(text);
        int max        = NeisHelper.GetMaxBytes(_selectedType);
        TxtByteInfo.Text = $"{current} / {max} Byte ({text.Length}자)";
    }

    private void UpdateProgress()
    {
        int total  = StudentList.Students.Count;
        int filled = _specCache.Values.Count(s => !string.IsNullOrWhiteSpace(s.Content));
        TxtProgress.Text = $"입력: {filled}/{total}명"
                           + (_modified.Count > 0 ? $" | 미저장: {_modified.Count}건" : "");
    }
}
