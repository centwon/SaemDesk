using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 활동 기록 개별 작성/수정 다이얼로그.
/// 폼 UI는 StudentLogBox 컨트롤에 위임.
/// </summary>
public partial class StudentLogEditDialog : Window
{
    public StudentLog? Result { get; private set; }

    private readonly StudentLog? _sourceLog;
    private readonly string      _studentId;
    private bool _isSaving;

    // ── 기본 생성자 (디자이너용) ─────────────────────────
    public StudentLogEditDialog() : this(string.Empty, "미상", null) { }

    // ── 신규 작성 ────────────────────────────────────────
    /// <summary>
    /// 신규 작성.
    /// displayName: "N학년 N반 N번 이름" 형식으로 호출부에서 넘긴다.
    /// </summary>
    public StudentLogEditDialog(
        string studentId,
        string displayName,
        StudentLog? existing)
    {
        InitializeComponent();

        _studentId = studentId;
        _sourceLog = existing;

        bool isNew = existing is null || existing.No <= 0;

        TitleText.Text    = isNew ? "활동 기록 추가" : "활동 기록 수정";
        SubTitleText.Text = displayName;

        if (isNew)
        {
            // 기본 학년도·학기는 Settings, 카테고리는 기타
            int year = existing?.Year > 0
                ? existing.Year
                : Settings.WorkYear.Value;
            int sem = existing?.Semester > 0
                ? existing.Semester
                : Settings.WorkSemester.Value;
            LogCategory cat = existing?.Category is LogCategory c && c != LogCategory.전체
                ? c
                : LogCategory.기타;

            LogBox.SetContext(year, sem, cat);
        }
        else
        {
            LogBox.LoadLog(existing!);
        }
    }

    // ── 저장 ─────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;
        if (!LogBox.Validate(out _)) return;

        _isSaving = true;
        SaveButton.IsEnabled = false;

        try
        {
            var log = LogBox.BuildLog(_studentId, _sourceLog);

            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);
            if (log.No > 0) await repo.UpdateAsync(log);
            else            await repo.CreateAsync(log);

            Result = log;
            Close();
        }
        catch (Exception ex)
        {
            LogBox.ShowError($"저장 실패: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[StudentLogEditDialog] {ex.Message}");
            SaveButton.IsEnabled = true;
            _isSaving = false;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
