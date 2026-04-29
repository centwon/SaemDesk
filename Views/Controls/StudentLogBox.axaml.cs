using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using SaemDesk.Models;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학생 활동 기록(StudentLog) 입력 폼 컨트롤.
///
/// 책임 범위:
///   - 카테고리·날짜·기록내용·구조화항목·자동생성·태그 입력 UI
///   - LoadLog()   : 기존 로그 편집 시 폼에 데이터 바인딩
///   - SetContext() : 신규 작성 시 학년도·학기·카테고리 설정
///   - BuildLog()  : 현재 폼 내용으로 StudentLog 생성 (저장 버튼 없음)
///   - Validate()  : 최소 유효성 검사
///
/// 저장·취소 버튼은 없음 — 각 다이얼로그가 담당.
/// 학생 정보 표시는 없음 — 각 다이얼로그 헤더가 담당.
/// </summary>
public partial class StudentLogBox : UserControl
{
    // ── 상태 ─────────────────────────────────────────────
    private int  _year;
    private int  _semester;
    private string _generatedText = string.Empty;

    // ── 생성자 ───────────────────────────────────────────
    public StudentLogBox()
    {
        InitializeComponent();
        InitCategoryCombo();
    }

    // ── 공개 API ─────────────────────────────────────────

    /// <summary>기존 로그 편집: 폼에 데이터를 채운다.</summary>
    public void LoadLog(StudentLog log)
    {
        _year     = log.Year;
        _semester = log.Semester;

        TxtYear.Text     = $"{log.Year}학년도";
        TxtSemester.Text = $"{log.Semester}학기";

        SelectCategory(log.Category);
        UpdateSubjectPanelVisibility(log.Category);
        TxtSubjectName.Text = log.SubjectName ?? string.Empty;

        DatePickerLog.SelectedDate = log.Date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(log.Date, DateTimeKind.Local)
            : log.Date;

        ChkIsImportant.IsChecked = log.IsImportant;
        TxtLog.Text              = log.Log             ?? string.Empty;
        TxtTag.Text              = log.Tag             ?? string.Empty;
        TxtActivityName.Text     = log.ActivityName    ?? string.Empty;
        TxtTopic.Text            = log.Topic           ?? string.Empty;
        TxtDescription.Text      = log.Description     ?? string.Empty;
        TxtRole.Text             = log.Role            ?? string.Empty;
        TxtSkillDeveloped.Text   = log.SkillDeveloped  ?? string.Empty;
        TxtStrengthShown.Text    = log.StrengthShown   ?? string.Empty;
        TxtResultOrOutcome.Text  = log.ResultOrOutcome ?? string.Empty;

        if (HasStructuredData())
            ExpanderStructured.IsExpanded = true;

        UpdateByteInfo();
        HideError();
    }

    /// <summary>
    /// 신규 작성: 학년도·학기·카테고리를 외부에서 주입하고 폼을 초기화.
    /// 개별 다이얼로그는 FilterBar 값을, 일괄 다이얼로그도 동일하게 넘긴다.
    /// </summary>
    public void SetContext(int year, int semester, LogCategory defaultCategory = LogCategory.기타)
    {
        _year     = year;
        _semester = semester;

        TxtYear.Text     = $"{year}학년도";
        TxtSemester.Text = $"{semester}학기";

        SelectCategory(defaultCategory);
        UpdateSubjectPanelVisibility(defaultCategory);

        DatePickerLog.SelectedDate = DateTime.Today;
        ChkIsImportant.IsChecked   = false;
        TxtSubjectName.Text = TxtLog.Text = TxtTag.Text = string.Empty;
        TxtActivityName.Text = TxtTopic.Text = TxtDescription.Text = string.Empty;
        TxtRole.Text = TxtSkillDeveloped.Text = TxtStrengthShown.Text = TxtResultOrOutcome.Text = string.Empty;
        ExpanderStructured.IsExpanded = false;
        _generatedText = string.Empty;
        BtnCopyToClipboard.IsEnabled = false;
        TxtGeneratedText.Text = "여기에 생성된 요약 또는 초안이 표시됩니다.";

        UpdateByteInfo();
        HideError();
    }

    /// <summary>
    /// 현재 폼 내용으로 StudentLog를 생성해서 반환.
    /// 저장 버튼을 가진 다이얼로그에서 호출한다.
    /// studentId: 개별 모드는 특정 학생 ID, 일괄 모드는 각 학생 ID를 반복 호출.
    /// sourceLog: 기존 수정 시 No·TeacherID 등 메타를 보존하기 위해 넘긴다.
    /// </summary>
    public StudentLog BuildLog(string studentId, StudentLog? sourceLog = null)
    {
        var cat = SelectedCategory();

        return new StudentLog
        {
            No              = sourceLog?.No ?? 0,
            StudentID       = studentId,
            TeacherID       = sourceLog?.TeacherID ?? Settings.User.Value,
            Year            = _year,
            Semester        = _semester,
            Date            = DatePickerLog.SelectedDate?.Date ?? DateTime.Today,
            Category        = cat,
            SubjectName     = (TxtSubjectName.Text ?? string.Empty).Trim(),
            IsImportant     = ChkIsImportant.IsChecked == true,
            Log             = (TxtLog.Text          ?? string.Empty).Trim(),
            Tag             = (TxtTag.Text          ?? string.Empty).Trim(),
            ActivityName    = (TxtActivityName.Text    ?? string.Empty).Trim(),
            Topic           = (TxtTopic.Text           ?? string.Empty).Trim(),
            Description     = (TxtDescription.Text     ?? string.Empty).Trim(),
            Role            = (TxtRole.Text            ?? string.Empty).Trim(),
            SkillDeveloped  = (TxtSkillDeveloped.Text  ?? string.Empty).Trim(),
            StrengthShown   = (TxtStrengthShown.Text   ?? string.Empty).Trim(),
            ResultOrOutcome = (TxtResultOrOutcome.Text ?? string.Empty).Trim(),
        };
    }

    /// <summary>최소 유효성 검사. false 시 error 메시지를 ShowError()로 표시.</summary>
    public bool Validate(out string error)
    {
        bool hasLog        = !string.IsNullOrWhiteSpace(TxtLog.Text);
        bool hasStructured = !string.IsNullOrWhiteSpace(TxtActivityName.Text) ||
                             !string.IsNullOrWhiteSpace(TxtTopic.Text)        ||
                             !string.IsNullOrWhiteSpace(TxtDescription.Text);

        if (!hasLog && !hasStructured)
        {
            error = "기록 내용 또는 활동 상세 항목 중 하나는 입력해주세요.";
            ShowError(error);
            return false;
        }

        error = string.Empty;
        HideError();
        return true;
    }

    /// <summary>카테고리를 외부에서 고정할 때 사용 (교과활동·동아리 일괄 모드).</summary>
    public void LockCategory(LogCategory category)
    {
        SelectCategory(category);
        CBoxCategory.IsEnabled = false;
        UpdateSubjectPanelVisibility(category);
    }

    /// <summary>과목명을 외부에서 주입하고 잠글 때 사용 (교과활동 일괄 모드).</summary>
    public void SetSubjectName(string name, bool locked = true)
    {
        PanelSubject.IsVisible   = true;
        TxtSubjectName.Text      = name;
        TxtSubjectName.IsReadOnly = locked;
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────

    private void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        var cat = SelectedCategory();
        UpdateSubjectPanelVisibility(cat);
    }

    private void OnLogTextChanged(object? sender, TextChangedEventArgs e)
        => UpdateByteInfo();

    private void OnStructuredFieldChanged(object? sender, TextChangedEventArgs e)
    {
        bool has = !string.IsNullOrWhiteSpace(TxtActivityName.Text) ||
                   !string.IsNullOrWhiteSpace(TxtTopic.Text)        ||
                   !string.IsNullOrWhiteSpace(TxtDescription.Text);
        BtnGenerateSummary.IsEnabled = has;
        BtnGenerateDraft.IsEnabled   = has;
    }

    private void OnGenerateSummaryClick(object? sender, RoutedEventArgs e)
    {
        var tmp = BuildLog("__preview__");
        _generatedText = tmp.Summary;
        TxtGeneratedText.Text        = _generatedText;
        BtnCopyToClipboard.IsEnabled = true;
    }

    private void OnGenerateDraftClick(object? sender, RoutedEventArgs e)
    {
        var tmp = BuildLog("__preview__");
        _generatedText = tmp.DraftSummary;
        TxtGeneratedText.Text        = _generatedText;
        BtnCopyToClipboard.IsEnabled = true;
    }

    private async void OnCopyToClipboardClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_generatedText)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(_generatedText);
    }

    // ── 헬퍼 ─────────────────────────────────────────────

    private void InitCategoryCombo()
    {
        CBoxCategory.Items.Clear();
        foreach (var cat in Enum.GetValues<LogCategory>())
        {
            if (cat == LogCategory.전체) continue;
            CBoxCategory.Items.Add(new ComboBoxItem { Content = cat.ToString(), Tag = cat });
        }
        if (CBoxCategory.Items.Count > 0) CBoxCategory.SelectedIndex = 0;
    }

    private void SelectCategory(LogCategory category)
    {
        for (int i = 0; i < CBoxCategory.Items.Count; i++)
        {
            if (CBoxCategory.Items[i] is ComboBoxItem ci && ci.Tag is LogCategory c && c == category)
            {
                CBoxCategory.SelectedIndex = i;
                return;
            }
        }
        if (CBoxCategory.Items.Count > 0) CBoxCategory.SelectedIndex = 0;
    }

    private LogCategory SelectedCategory()
    {
        if (CBoxCategory.SelectedItem is ComboBoxItem ci && ci.Tag is LogCategory cat)
            return cat;
        return LogCategory.기타;
    }

    private void UpdateSubjectPanelVisibility(LogCategory cat)
    {
        PanelSubject.IsVisible = cat is LogCategory.교과활동 or LogCategory.개인별세특;
        if (!PanelSubject.IsVisible) TxtSubjectName.Text = string.Empty;
    }

    private bool HasStructuredData()
        => !string.IsNullOrWhiteSpace(TxtActivityName.Text)  ||
           !string.IsNullOrWhiteSpace(TxtTopic.Text)         ||
           !string.IsNullOrWhiteSpace(TxtDescription.Text)   ||
           !string.IsNullOrWhiteSpace(TxtRole.Text)          ||
           !string.IsNullOrWhiteSpace(TxtSkillDeveloped.Text)||
           !string.IsNullOrWhiteSpace(TxtStrengthShown.Text) ||
           !string.IsNullOrWhiteSpace(TxtResultOrOutcome.Text);

    private void UpdateByteInfo()
    {
        string text  = TxtLog.Text ?? string.Empty;
        int    bytes = text.Sum(c => c >= 0xAC00 && c <= 0xD7A3 ? 3 : c >= 0x3000 ? 3 : 1);
        TxtLogByteInfo.Text = $"{bytes} Byte / {text.Length} 자";
    }

    public void ShowError(string msg)
    {
        TxtError.Text      = msg;
        TxtError.IsVisible = true;
    }

    public void HideError()
    {
        TxtError.IsVisible = false;
    }
}
