using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 기록 입력/편집 다이얼로그.
/// 원본: NewSchool.Dialogs.StudentLogDialog (WinUI3 Window).
/// 4가지 모드:
///   1. 단일 학생 편집 (기존 로그)
///   2. 단일 학생 신규 작성
///   3. 학급별 일괄 입력
///   4. 교과활동 과목별 / 동아리활동별 입력
/// </summary>
public partial class StudentLogDialog : Window
{
    // ────────────────────────────────────────────────────
    //  Fields
    // ────────────────────────────────────────────────────

    private LogCategory _category = LogCategory.전체;
    private int _year;
    private int _semester;
    private int _selectedGrade;
    private int _selectedClass;
    private int _selectedCourseNo;
    private int _selectedClubNo;

    private bool _isSingleStudentMode;
    private bool _isInitializing = true;
    private string _singleStudentId = string.Empty;

    public ObservableCollection<Course> Courses { get; } = new();
    public ObservableCollection<Club>   Clubs   { get; } = new();

    public List<StudentLog> SavedLogs { get; } = new();
    public bool IsSuccess { get; private set; }

    // ────────────────────────────────────────────────────
    //  생성자 — 모드별
    // ────────────────────────────────────────────────────

    /// <summary>모드 1: 단일 학생 기존 로그 편집</summary>
    public StudentLogDialog(StudentLog log)
    {
        InitializeComponent();
        DataContext = this;

        _isSingleStudentMode = true;
        _category = log.Category;
        _year     = log.Year;
        _semester = log.Semester;
        _singleStudentId = log.StudentID;

        InitCommon();
        HideAllFilters();

        ListStudents.IsVisible   = false;

        LogBox.LoadLog(log);
        TxtStudentInfo.Text = "학생 정보 로드 중...";
        Title = $"{log.Category} 기록 편집";

        Opened += async (_, _) =>
        {
            try
            {
                using var svc  = new StudentService(SchoolDatabase.DbPath);
                var student    = await svc.GetBasicInfoAsync(log.StudentID);
                TxtStudentInfo.Text = student is not null ? $"학생: {student.Name}" : "학생 정보 없음";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StudentLogDialog] {ex.Message}");
                TxtStudentInfo.Text = "학생 정보 없음";
            }
            _isInitializing = false;
        };
    }

    /// <summary>모드 2: 단일 학생 신규 작성</summary>
    public StudentLogDialog(Enrollment student, int year, int semester)
    {
        InitializeComponent();
        DataContext = this;

        _isSingleStudentMode = true;
        _year            = year;
        _semester        = semester;
        _singleStudentId = student.StudentID;

        InitCommon();
        HideAllFilters();
        ListStudents.IsVisible = false;

        TxtStudentInfo.Text = $"학생: {student.Name} ({student.Grade}학년 {student.Class}반 {student.Number}번)";
        LogBox.SetContext(year, semester, LogCategory.기타);
        Title           = $"학생 기록 작성 — {student.Name}";
        _isInitializing = false;
    }

    /// <summary>모드 3: 학급 일괄 입력</summary>
    public StudentLogDialog(LogCategory category, int year, int semester, int grade, int classNum)
    {
        InitializeComponent();
        DataContext = this;

        _category      = category;
        _year          = year;
        _semester      = semester;
        _selectedGrade = grade;
        _selectedClass = classNum;

        InitCommon();
        SetupBatchMode();
        HideAllFilters();

        TxtStudentInfo.Text = $"{year}학년도 {semester}학기 ▸ {grade}학년 {classNum}반";
        LogBox.LockCategory(category);
        Title = $"{category} 기록 일괄 입력 — {grade}학년 {classNum}반";

        Opened += async (_, _) =>
        {
            await LoadClassStudentsAsync(year, semester, grade, classNum);
            _isInitializing = false;
        };
    }

    /// <summary>모드 4a: 교과활동 과목별 입력</summary>
    public StudentLogDialog(LogCategory category, int year, int semester, int courseNo, string teacherId)
    {
        InitializeComponent();
        DataContext = this;

        _category        = LogCategory.교과활동;
        _year            = year;
        _semester        = semester;
        _selectedCourseNo = courseNo;

        InitCommon();
        SetupBatchMode();
        HideAllFilters();
        CoursePanel.IsVisible = true;

        LogBox.LockCategory(LogCategory.교과활동);
        LogBox.SetSubjectName("", locked: false);
        TxtStudentInfo.Text = $"{year}학년도 {semester}학기";
        Title = $"교과활동 기록 일괄 입력 — {year}학년도";

        Opened += async (_, _) =>
        {
            await LoadCoursesAsync(year, semester, teacherId);
            _isInitializing = false;
        };
    }

    /// <summary>모드 4b: 동아리활동별 입력</summary>
    public StudentLogDialog(int year, int semester, int clubNo, string schoolCode)
    {
        InitializeComponent();
        DataContext = this;

        _category      = LogCategory.동아리활동;
        _year          = year;
        _semester      = semester;
        _selectedClubNo = clubNo;

        InitCommon();
        SetupBatchMode();
        HideAllFilters();
        ClubPanel.IsVisible = true;

        LogBox.LockCategory(LogCategory.동아리활동);
        TxtStudentInfo.Text = $"{year}학년도 {semester}학기";
        Title = $"동아리활동 기록 일괄 입력 — {year}학년도";

        Opened += async (_, _) =>
        {
            await LoadClubsAsync(year, schoolCode);
            _isInitializing = false;
        };
    }

    // ────────────────────────────────────────────────────
    //  초기화
    // ────────────────────────────────────────────────────

    private void InitCommon()
    {
        CBoxCategory.ItemsSource = Enum.GetValues<LogCategory>()
            .Where(c => c != LogCategory.전체).Cast<object>().ToList();
    }

    private void SetupBatchMode()
    {
        _isSingleStudentMode = false;
        ListStudents.ShowCheckBox = true;
        LogBox.SetContext(_year, _semester, _category);
    }

    private void HideAllFilters()
    {
        CategoryPanel.IsVisible = false;
        CoursePanel.IsVisible   = false;
        ClubPanel.IsVisible     = false;
        GradePanel.IsVisible    = false;
        ClassPanel.IsVisible    = false;
    }

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private async Task LoadClassStudentsAsync(int year, int semester, int grade, int cls)
    {
        try
        {
            using var svc = new EnrollmentService();
            var list = await svc.GetEnrollmentsAsync(Settings.SchoolCode.Value, year, 0, grade, cls);
            ListStudents.LoadStudents(list.OrderBy(e => e.Number));
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentLogDialog] LoadClass: {ex.Message}"); }
    }

    private async Task LoadCoursesAsync(int year, int semester, string teacherId)
    {
        try
        {
            using var svc = new CourseService();
            var list = await svc.GetByTeacherAsync(teacherId, year, semester);
            Courses.Clear();
            foreach (var c in list) Courses.Add(c);

            var target = Courses.FirstOrDefault(c => c.No == _selectedCourseNo) ?? Courses.FirstOrDefault();
            if (target is not null) CBoxCourse.SelectedItem = target;
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentLogDialog] LoadCourses: {ex.Message}"); }
    }

    private async Task LoadClubsAsync(int year, string schoolCode)
    {
        try
        {
            using var svc = new ClubService();
            var list = await svc.GetAllClubsAsync(schoolCode, year);
            Clubs.Clear();
            foreach (var c in list) Clubs.Add(c);

            var target = Clubs.FirstOrDefault(c => c.No == _selectedClubNo) ?? Clubs.FirstOrDefault();
            if (target is not null) CBoxClub.SelectedItem = target;
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentLogDialog] LoadClubs: {ex.Message}"); }
    }

    private async Task LoadCourseStudentsAsync(int courseNo)
    {
        try
        {
            using var ceRepo = new CourseEnrollmentRepository(SchoolDatabase.DbPath);
            var ceList = await ceRepo.GetByCourseAsync(courseNo);
            if (!ceList.Any()) { ListStudents.ClearStudents(); return; }

            var ids = ceList.Select(ce => ce.StudentID).ToHashSet();
            using var enrollSvc = new EnrollmentService();
            var all = await enrollSvc.GetEnrollmentsAsync(Settings.SchoolCode.Value, _year);
            ListStudents.LoadStudents(all.Where(e => ids.Contains(e.StudentID))
                                        .OrderBy(e => e.Class).ThenBy(e => e.Number));
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentLogDialog] LoadCourseStudents: {ex.Message}"); }
    }

    private async Task LoadClubStudentsAsync(int clubNo)
    {
        try
        {
            using var ceRepo = new ClubEnrollmentRepository(SchoolDatabase.DbPath);
            var ceList = await ceRepo.GetByClubAsync(clubNo);
            if (!ceList.Any()) { ListStudents.ClearStudents(); return; }

            var ids = ceList.Select(ce => ce.StudentID).ToHashSet();
            using var enrollSvc = new EnrollmentService();
            var all = await enrollSvc.GetEnrollmentsAsync(Settings.SchoolCode.Value, _year);
            ListStudents.ViewMode = ListStudent.View.ClassNumName;
            ListStudents.LoadStudents(all.Where(e => ids.Contains(e.StudentID))
                                        .OrderBy(e => e.Class).ThenBy(e => e.Number));
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentLogDialog] LoadClubStudents: {ex.Message}"); }
    }

    // ────────────────────────────────────────────────────
    //  필터 이벤트
    // ────────────────────────────────────────────────────

    private void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || CBoxCategory.SelectedItem is not LogCategory cat) return;
        _category = cat;

        CoursePanel.IsVisible = cat is LogCategory.교과활동 or LogCategory.개인별세특;
        ClubPanel.IsVisible   = cat == LogCategory.동아리활동;

        bool showGradeClass = _selectedGrade == 0 && _selectedClass == 0
            && cat is not (LogCategory.교과활동 or LogCategory.개인별세특 or LogCategory.동아리활동);
        GradePanel.IsVisible  = showGradeClass;
        ClassPanel.IsVisible  = showGradeClass;
    }

    private async void OnCourseChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CBoxCourse.SelectedItem is not Course course) return;
        _selectedCourseNo = course.No;
        TxtStudentInfo.Text = $"{_year}학년도 {_semester}학기 ▸ {course.Subject}";
        LogBox.SetSubjectName(course.Subject, locked: true);
        await LoadCourseStudentsAsync(course.No);
    }

    private async void OnClubChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CBoxClub.SelectedItem is not Club club) return;
        _selectedClubNo = club.No;
        TxtStudentInfo.Text = $"{_year}학년도 {_semester}학기 ▸ {club.ClubName}";
        await LoadClubStudentsAsync(club.No);
    }

    private async void OnGradeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CBoxGrade.SelectedItem is not int grade) return;
        _selectedGrade = grade;
        CBoxClass.Items.Clear();
        try
        {
            using var svc = new EnrollmentService();
            var classes = await svc.GetClassListAsync(Settings.SchoolCode.Value, _year, grade);
            foreach (var c in classes) CBoxClass.Items.Add(c);
            if (CBoxClass.Items.Count > 0) CBoxClass.SelectedIndex = 0;
        }
        catch (Exception ex) { Debug.WriteLine($"[StudentLogDialog] OnGradeChanged: {ex.Message}"); }
    }

    private async void OnClassChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CBoxClass.SelectedItem is not int cls) return;
        _selectedClass = cls;
        TxtStudentInfo.Text = $"{_year}학년도 {_semester}학기 ▸ {_selectedGrade}학년 {cls}반";
        await LoadClassStudentsAsync(_year, _semester, _selectedGrade, cls);
    }

    // ────────────────────────────────────────────────────
    //  저장
    // ────────────────────────────────────────────────────

    private bool _isSaving;

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;
        if (!LogBox.Validate(out _)) return;

        _isSaving = true;
        SaveButton.IsEnabled = false;

        try
        {
            if (_isSingleStudentMode)
                await SaveSingleAsync();
            else
                await SaveBatchAsync();

            IsSuccess = true;
            Close();
        }
        catch (Exception ex)
        {
            LogBox.ShowError($"저장 실패: {ex.Message}");
            Debug.WriteLine($"[StudentLogDialog] Save: {ex.Message}");
            SaveButton.IsEnabled = true;
            _isSaving = false;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private async Task SaveSingleAsync()
    {
        // 단일 모드: TxtStudentInfo 에서 studentId 를 얻어올 수 없으므로
        // 생성자에서 _singleStudentId 를 보존해야 함
        var log = LogBox.BuildLog(_singleStudentId);
        using var svc = new StudentLogService();
        if (log.No > 0) await svc.UpdateAsync(log);
        else            await svc.InsertAsync(log);
        SavedLogs.Clear();
        SavedLogs.Add(log);
    }

    private async Task SaveBatchAsync()
    {
        SavedLogs.Clear();
        var selected = ListStudents.GetSelectedStudents().ToList();
        if (!selected.Any())
        {
            LogBox.ShowError("저장할 학생을 선택해주세요.");
            return;
        }

        int courseNo = _category is LogCategory.교과활동 or LogCategory.개인별세특
            ? _selectedCourseNo : 0;

        foreach (var enrollment in selected)
        {
            var log = LogBox.BuildLog(enrollment.StudentID);
            log.CourseNo = courseNo;

            // 동아리는 동아리 이름을 ActivityName으로
            if (_category == LogCategory.동아리활동 && CBoxClub.SelectedItem is Club club)
                log.ActivityName = club.ClubName;

            using var svc = new StudentLogService();
            await svc.InsertAsync(log);
            SavedLogs.Add(log);
        }
    }
}
