using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.ViewModels;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 상세 보기 다이얼로그 (읽기 전용).
/// 탭 1: StudentCard (학생 정보 + 상세 정보) — IsEnabled=False
/// 탭 2: LogListViewer (활동 기록 목록) — 편집 없음
/// </summary>
public partial class StudentDetailDialog : Window
{
    private readonly string _studentId;
    private readonly int _year;

    public StudentDetailDialog() : this(string.Empty, string.Empty, string.Empty, 0) { }

    /// <param name="studentId">학생 ID</param>
    /// <param name="classInfo">학년 반 번 문자열 (예: "2학년 3반 15번")</param>
    /// <param name="studentName">학생 이름</param>
    /// <param name="year">학년도 (0이면 Settings.WorkYear 사용)</param>
    public StudentDetailDialog(string studentId, string classInfo, string studentName, int year = 0)
    {
        InitializeComponent();

        _studentId = studentId ?? string.Empty;
        _year      = year > 0 ? year : Settings.WorkYear.Value;

        TitleText.Text    = string.IsNullOrWhiteSpace(studentName) ? "이름 미상" : studentName;
        SubTitleText.Text = string.IsNullOrWhiteSpace(classInfo)   ? $"학번 {studentId}" : classInfo;

        // LogListViewer: 특정 학생 보기이므로 학생 컬럼 전부 숨김
        LogViewer.StudentInfoMode = StudentInfoMode.HideAll;

        Opened += async (_, _) => await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        await LoadCardAsync();
        await LoadLogsAsync();
    }

    // ── StudentCard ──────────────────────────────────────

    private async Task LoadCardAsync()
    {
        if (string.IsNullOrEmpty(_studentId)) return;
        try
        {
            if (CardControl.ViewModel is StudentCardViewModel vm)
                await vm.LoadStudentAsync(_studentId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentDetailDialog] 카드 로드 오류: {ex.Message}");
        }
    }

    // ── LogListViewer ────────────────────────────────────

    private async Task LoadLogsAsync()
    {
        if (string.IsNullOrEmpty(_studentId)) return;
        try
        {
            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);
            var list = await repo.GetByStudentAsync(_studentId, _year, 0);

            var vmList = new List<StudentLogViewModel>();
            foreach (var log in list)
                vmList.Add(await StudentLogViewModel.CreateAsync(log));

            LogViewer.LoadLogs(vmList);
            LogCountText.Text = $"{_year}학년도 · 총 {vmList.Count}건";
        }
        catch (Exception ex)
        {
            LogCountText.Text = $"불러오기 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[StudentDetailDialog] 로그 로드 오류: {ex.Message}");
        }
    }

    // ── 닫기 ─────────────────────────────────────────────

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
