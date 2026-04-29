using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 활동 기록(StudentLog) 작성/수정 다이얼로그.
/// </summary>
public partial class StudentLogEditDialog : Window
{
    /// <summary>저장된 기록. 취소·오류 시 null.</summary>
    public StudentLog? Result { get; private set; }

    private readonly StudentLog _log;
    private readonly bool _isNew;
    private bool _isSaving;

    public StudentLogEditDialog() : this(string.Empty, "이름 미상", null) { }

    public StudentLogEditDialog(string studentId, string studentName, StudentLog? existing)
    {
        InitializeComponent();

        _isNew = existing is null;

        _log = existing is not null
            ? new StudentLog
            {
                No              = existing.No,
                StudentID       = existing.StudentID,
                TeacherID       = existing.TeacherID,
                Year            = existing.Year,
                Semester        = existing.Semester,
                Date            = existing.Date,
                Category        = existing.Category,
                CourseNo        = existing.CourseNo,
                SubjectName     = existing.SubjectName,
                Log             = existing.Log,
                Tag             = existing.Tag,
                IsImportant     = existing.IsImportant,
                ActivityName    = existing.ActivityName,
                Topic           = existing.Topic,
                Description     = existing.Description,
                Role            = existing.Role,
                SkillDeveloped  = existing.SkillDeveloped,
                StrengthShown   = existing.StrengthShown,
                ResultOrOutcome = existing.ResultOrOutcome
            }
            : new StudentLog
            {
                StudentID = studentId,
                TeacherID = string.IsNullOrWhiteSpace(Settings.UserName.Value)
                              ? "teacher" : Settings.UserName.Value,
                Year      = Settings.WorkYear.Value,
                Semester  = Settings.WorkSemester.Value,
                Date      = DateTime.Today,
                Category  = LogCategory.교과활동
            };

        // 헤더
        TitleText.Text    = _isNew ? "활동 기록 추가" : "활동 기록 수정";
        SubTitleText.Text = $"{studentName} · {_log.Year}학년도 {_log.Semester}학기";

        // 카테고리 항목
        foreach (var cat in Enum.GetValues<LogCategory>())
        {
            if (cat == LogCategory.전체) continue;
            CategoryCombo.Items.Add(new ComboBoxItem { Content = cat.ToString(), Tag = cat });
        }
        // 선택
        for (int i = 0; i < CategoryCombo.ItemCount; i++)
        {
            if (CategoryCombo.Items[i] is ComboBoxItem item && item.Tag is LogCategory c && c == _log.Category)
            {
                CategoryCombo.SelectedIndex = i;
                break;
            }
        }
        if (CategoryCombo.SelectedIndex < 0 && CategoryCombo.ItemCount > 0)
            CategoryCombo.SelectedIndex = 0;

        DatePickerCtl.SelectedDate = new DateTimeOffset(_log.Date);

        SubjectBox.Text  = _log.SubjectName;
        ActivityBox.Text = _log.ActivityName;
        TopicBox.Text    = _log.Topic;
        DescBox.Text     = _log.Description;
        RoleBox.Text     = _log.Role;
        SkillBox.Text    = _log.SkillDeveloped;
        StrengthBox.Text = _log.StrengthShown;
        ResultBox.Text   = _log.ResultOrOutcome;
        LogBox.Text      = _log.Log;
        ImportantCheck.IsChecked = _log.IsImportant;
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;

        // 카테고리
        LogCategory category = LogCategory.교과활동;
        if (CategoryCombo.SelectedItem is ComboBoxItem item && item.Tag is LogCategory c)
            category = c;

        // 날짜
        DateTime date = DatePickerCtl.SelectedDate?.Date ?? DateTime.Today;

        string activity = (ActivityBox.Text ?? string.Empty).Trim();
        string topic    = (TopicBox.Text    ?? string.Empty).Trim();
        string desc     = (DescBox.Text     ?? string.Empty).Trim();
        string logText  = (LogBox.Text      ?? string.Empty).Trim();

        // 최소 검증: 구조화 필드 또는 단순 메모 중 하나는 있어야 함
        if (string.IsNullOrWhiteSpace(activity) &&
            string.IsNullOrWhiteSpace(topic) &&
            string.IsNullOrWhiteSpace(desc) &&
            string.IsNullOrWhiteSpace(logText))
        {
            ShowError("활동명/주제/내용 또는 단순 메모 중 하나는 입력해주세요.");
            return;
        }

        _isSaving = true;
        SaveButton.IsEnabled = false;

        try
        {
            _log.Category        = category;
            _log.Date            = date;
            _log.SubjectName     = (SubjectBox.Text ?? string.Empty).Trim();
            _log.ActivityName    = activity;
            _log.Topic           = topic;
            _log.Description     = desc;
            _log.Role            = (RoleBox.Text     ?? string.Empty).Trim();
            _log.SkillDeveloped  = (SkillBox.Text    ?? string.Empty).Trim();
            _log.StrengthShown   = (StrengthBox.Text ?? string.Empty).Trim();
            _log.ResultOrOutcome = (ResultBox.Text   ?? string.Empty).Trim();
            _log.Log             = logText;
            _log.IsImportant     = ImportantCheck.IsChecked == true;

            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);
            if (_isNew)
                await repo.CreateAsync(_log);
            else
                await repo.UpdateAsync(_log);

            Result = _log;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"저장 실패: {ex.Message}");
            SaveButton.IsEnabled = true;
            _isSaving = false;
            System.Diagnostics.Debug.WriteLine($"[StudentLogEditDialog] {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void ShowError(string message)
    {
        ErrorText.Text      = message;
        ErrorText.IsVisible = true;
    }
}
