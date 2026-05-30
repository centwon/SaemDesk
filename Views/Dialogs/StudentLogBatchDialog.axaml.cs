using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 활동 기록 일괄 입력 다이얼로그.
///
/// 구조:
///   좌측 — ListStudent (체크박스 모드): 헤더 체크박스로 전체 선택/해제, 개별 체크박스로 선택
///   우측 — StudentLogBox: 기록 내용 입력 (폼 컨트롤 재사용)
///
/// 저장 시 동작:
///   체크된 학생마다 LogBox.BuildLog(studentId) 를 호출해 개별 StudentLog 를 생성하고
///   StudentLogRepository.CreateAsync() 로 각각 INSERT 한다.
/// </summary>
public partial class StudentLogBatchDialog : Window
{
    public IReadOnlyList<StudentLog> SavedLogs { get; private set; } = Array.Empty<StudentLog>();
    public bool IsSuccess { get; private set; }

    private readonly int         _year;
    private readonly int         _semester;
    private readonly LogCategory _defaultCategory;
    private bool _isSaving;

    // ── 생성자 ───────────────────────────────────────────

    public StudentLogBatchDialog() : this(
        Array.Empty<Enrollment>(), 0, 0, LogCategory.기타) { }

    /// <summary>
    /// students: ClassFilterBar.SelectionChanged 의 e.Students 를 그대로 넘긴다.
    /// defaultCategory: 현재 페이지에서 선택된 카테고리.
    /// </summary>
    public StudentLogBatchDialog(
        IReadOnlyList<Enrollment> students,
        int year,
        int semester,
        LogCategory defaultCategory = LogCategory.기타,
        DateTime? date = null)
    {
        InitializeComponent();

        _year            = year;
        _semester        = semester;
        _defaultCategory = defaultCategory;

        // 헤더
        SubTitleText.Text = students.Count > 0
            ? $"{year}학년도 {semester}학기  ·  {students[0].Grade}학년 {students[0].Class}반"
            : $"{year}학년도 {semester}학기";

        // 학생 목록 로드
        StudentList.LoadStudents(students.ToList());
        StudentList.ShowCheckBox = true;

        // 체크박스 모드에서는 SelectionChanged 로 카운터 갱신
        // (StudentSelected 는 단일선택 모드 전용이라 다중선택 모드에서 발화 안 됨)
        StudentList.SelectionChangedNotify += OnStudentSelectionChanged;

        // 폼 초기화
        LogBox.SetContext(year, semester, defaultCategory, date);

        UpdateSaveHint(0);
    }

    // ── 이벤트 ───────────────────────────────────────────

    private void OnStudentSelectionChanged(object? sender, int selectedCount)
        => UpdateSaveHint(selectedCount);

    private void UpdateSaveHint(int count)
    {
        TxtSelectedCount.Text = $"{count}명 선택됨";
        TxtSaveHint.Text      = count > 0
            ? $"선택된 {count}명에게 동일한 내용으로 저장됩니다."
            : "저장할 학생을 선택해주세요.";
        SaveButton.IsEnabled  = count > 0;
    }

    // ── 저장 ─────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;

        if (!LogBox.Validate(out _)) return;

        var selected = StudentList.GetSelectedStudents().ToList();
        if (selected.Count == 0)
        {
            LogBox.ShowError("저장할 학생을 선택해주세요.");
            return;
        }

        _isSaving = true;
        SaveButton.IsEnabled = false;

        var saved = new List<StudentLog>();

        try
        {
            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);

            foreach (var student in selected)
            {
                var log = LogBox.BuildLog(student.StudentID);
                await repo.CreateAsync(log);
                saved.Add(log);
            }

            SavedLogs = saved.AsReadOnly();
            IsSuccess = true;
            Close();
        }
        catch (Exception ex)
        {
            LogBox.ShowError($"저장 실패: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[StudentLogBatchDialog] {ex.Message}");
            SaveButton.IsEnabled = true;
            _isSaving = false;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
