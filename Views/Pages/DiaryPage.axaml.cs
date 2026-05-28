using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;
using SaemDesk.Views.Dialogs;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 학급일지 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.ClassDiaryPage (WinUI3).
/// 구성: 좌측 ListStudent + 우측 상단 ClassDiaryBox + 우측 하단 LogListViewer.
/// </summary>
public partial class DiaryPage : UserControl
{
    private DiaryPageVM VM => (DiaryPageVM)DataContext!;

    private DateTime _currentDate  = DateTime.Today;
    private int _currentYear;
    private int _currentGrade;
    private int _currentClass;
    private IReadOnlyList<Enrollment> _currentStudents = Array.Empty<Enrollment>();

    public DiaryPage()
    {
        InitializeComponent();
        DataContext = new DiaryPageVM();

        // 날짜 초기화
        DatePicker.SelectedDate = DateTime.Today;

        // 필터 이벤트 연결
        YearSemPicker.YearSemesterChanged += OnYearSemesterChanged;
        ClassFilter.ClassChanged          += OnClassFilterChanged;

        // 당일 기록 설정
        DailyLogList.StudentInfoMode = StudentInfoMode.NumName;
        DailyLogList.Category        = LogCategory.전체;

        // 컨텍스트 메뉴
        SetupStudentContextMenu();
    }

    // ────────────────────────────────────────────────────
    //  조회 버튼
    // ────────────────────────────────────────────────────

    private async void BtnLoad_Click(object? sender, RoutedEventArgs e)
    {
        // 조회 버튼: 현재 캐시된 학생 목록 재사용
        StudentList.LoadStudents(_currentStudents);
        await RefreshAllAsync();
    }

    private void OnFilterChanged(object? sender, ClassChangedEventArgs e) { }

    // 학년도·학기 변경 → ClassPicker 재로드
    private async void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        await ClassFilter.LoadAsync(e.Year, e.Semester);
    }

    // 학급 변경 — e.Students 직접 사용
    private async void OnClassFilterChanged(object? sender, ClassChangedEventArgs e)
    {
        VM.WorkYear  = e.Year;
        VM.Grade     = e.Grade;
        VM.ClassNum  = e.Class;
        _currentYear  = e.Year;
        _currentGrade = e.Grade;
        _currentClass = e.Class;
        _currentStudents = e.Students;
        StudentList.LoadStudents(e.Students);
        await RefreshAllAsync();
    }

    // ────────────────────────────────────────────────────
    //  날짜 변경
    // ────────────────────────────────────────────────────

    private async void DatePicker_DateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DatePicker.SelectedDate is not DateTime newDate) return;

        await DiaryBox.SaveDiaryAsync();
        _currentDate = newDate;
        await RefreshAllAsync();
    }

    private void BtnDatePrev_Click(object? sender, RoutedEventArgs e)
    {
        if (DatePicker.SelectedDate is DateTime d)
            DatePicker.SelectedDate = d.AddDays(-1);
    }

    private void BtnDateNext_Click(object? sender, RoutedEventArgs e)
    {
        if (DatePicker.SelectedDate is DateTime d)
            DatePicker.SelectedDate = d.AddDays(1);
    }

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private async Task LoadStudentsAsync()
    {
        // 예약: 외부에서 학생 목록을 주입하는 경우 사용가능
        // 현재는 OnFilterBarChanged 가 직접 체워주므로 도달 편에서만 호운다
        StudentList.LoadStudents(_currentStudents);
    }

    private async Task RefreshAllAsync()
    {
        await LoadDiaryAsync();
        await LoadDailyLogsAsync();
    }

    private async Task LoadDiaryAsync()
    {
        if (_currentGrade == 0 || _currentClass == 0 || _currentYear == 0) return;
        await DiaryBox.LoadDiaryAsync(_currentGrade, _currentClass, _currentDate);
    }

    private async Task LoadDailyLogsAsync()
    {
        if (_currentYear == 0 || _currentGrade == 0 || _currentClass == 0)
        {
            DailyLogList.Clear();
            TxtDailyLogCount.Text = "";
            return;
        }

        try
        {
            var logs = await StudentLogService.GetByClassAsync(
                Settings.SchoolCode.Value, _currentYear, _currentGrade, _currentClass, _currentDate);

            var vms = new List<StudentLogViewModel>();
            foreach (var l in logs)
                vms.Add(await StudentLogViewModel.CreateAsync(l));

            DailyLogList.LoadLogs(vms);
            TxtDailyLogTitle.Text  = $"{_currentDate:M월 d일} 학생 기록";
            TxtDailyLogCount.Text  = logs.Count > 0 ? $"{logs.Count}건" : "기록 없음";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiaryPage] LoadDailyLogs: {ex.Message}");
            DailyLogList.Clear();
            TxtDailyLogCount.Text = "로드 실패";
        }
    }

    // ────────────────────────────────────────────────────
    //  당일 기록 버튼
    // ────────────────────────────────────────────────────

    private async void BtnAddDailyLog_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentYear == 0 || _currentGrade == 0 || _currentClass == 0) return;

        var log = new StudentLog
        {
            Category  = LogCategory.기타,
            Year      = _currentYear,
            Semester  = Settings.WorkSemester.Value,
            TeacherID = Settings.User.Value,
            Date      = _currentDate,
        };

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new Views.Dialogs.StudentLogEditDialog(log.StudentID, "", null);
        await dlg.ShowDialog(owner);
        if (dlg.Result != null) await LoadDailyLogsAsync();
    }

    private async void BtnRefreshLogs_Click(object? sender, RoutedEventArgs e)
        => await LoadDailyLogsAsync();

    // ────────────────────────────────────────────────────
    //  일지 목록 보기
    // ────────────────────────────────────────────────────

    private async void BtnViewDiaryList_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentYear == 0 || _currentGrade == 0 || _currentClass == 0) return;

        var win = new ClassDiaryListWin(_currentYear, Settings.WorkSemester.Value, _currentGrade, _currentClass);

        win.DiarySelected += async (_, diary) =>
        {
            await DiaryBox.SaveDiaryAsync();
            DatePicker.SelectedDate = diary.Date;
        };

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null) win.Show(owner);
    }

    // ────────────────────────────────────────────────────
    //  컨텍스트 메뉴
    // ────────────────────────────────────────────────────

    private void SetupStudentContextMenu()
    {
        var menu = new ContextMenu();

        var miAddLog = new MenuItem { Header = "누가기록 작성" };
        miAddLog.Click += ContextMenu_AddLog_Click;

        var miViewLogs = new MenuItem { Header = "오늘의 기록 보기" };
        miViewLogs.Click += ContextMenu_ViewTodayLogs_Click;

        var miViewInfo = new MenuItem { Header = "학생 정보 보기" };
        miViewInfo.Click += ContextMenu_ViewStudentInfo_Click;

        menu.Items.Add(miAddLog);
        menu.Items.Add(miViewLogs);
        menu.Items.Add(new Separator());
        menu.Items.Add(miViewInfo);

        StudentList.ItemContextFlyout = menu;
    }

    private async void ContextMenu_AddLog_Click(object? sender, RoutedEventArgs e)
    {
        var student = StudentList.SelectedStudent;
        if (student is null || _currentYear <= 0) return;

        var log = new StudentLog
        {
            StudentID = student.StudentID,
            Year      = _currentYear,
            Semester  = Settings.WorkSemester.Value,
            TeacherID = Settings.User.Value,
            Date      = _currentDate,
        };

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new Views.Dialogs.StudentLogEditDialog(log.StudentID, student.Name, null);
        await dlg.ShowDialog(owner);
        if (dlg.Result != null) await LoadDailyLogsAsync();
    }

    private async void ContextMenu_ViewTodayLogs_Click(object? sender, RoutedEventArgs e)
    {
        var student = StudentList.SelectedStudent;
        if (student is null) return;

        try
        {
            var logs = await StudentLogService.GetByClassAsync(
                Settings.SchoolCode.Value, _currentYear, _currentGrade, _currentClass, _currentDate);

            var filtered = new List<StudentLogViewModel>();
            foreach (var l in logs)
                if (l.StudentID == student.StudentID)
                    filtered.Add(await StudentLogViewModel.CreateAsync(l));

            DailyLogList.LoadLogs(filtered);
            TxtDailyLogTitle.Text = $"{_currentDate:M월 d일} {student.Name} 기록";
            TxtDailyLogCount.Text = filtered.Count > 0 ? $"{filtered.Count}건" : "기록 없음";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiaryPage] ViewTodayLogs: {ex.Message}");
        }
    }

    private async void ContextMenu_ViewStudentInfo_Click(object? sender, RoutedEventArgs e)
    {
        var student = StudentList.SelectedStudent;
        if (student is null) return;

        var dlg = new Views.Dialogs.StudentDetailDialog(
            student.StudentID,
            student.GetClassInfo(),
            student.Name,
            _currentYear);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null) await dlg.ShowDialog(owner);
    }
}
