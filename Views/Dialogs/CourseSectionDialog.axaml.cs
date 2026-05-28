using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 소단원 추가/편집 다이얼로그.
/// 원본: NewSchool.Dialogs.CourseSectionDialog (WinUI3 ContentDialog).
/// </summary>
public partial class CourseSectionDialog : Window
{
    private readonly Course         _course;
    private readonly CourseSection? _existing;
    private readonly bool           _isEdit;

    public bool IsSuccess { get; private set; }

    public CourseSectionDialog(Course course, CourseSection? section = null)
    {
        InitializeComponent();
        _course   = course;
        _existing = section;
        _isEdit   = section is not null;

        TxtCourseName.Text = course.Subject;
        TxtCourseInfo.Text = $"{course.Grade}학년 · {course.TypeDisplay}";
        Title              = _isEdit ? "소단원 편집" : "소단원 추가";

        if (_isEdit && _existing is not null) LoadSectionData(_existing);
    }

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private void LoadSectionData(CourseSection s)
    {
        NumUnitNo.Value         = s.UnitNo;
        TxtUnitName.Text        = s.UnitName;
        NumChapterNo.Value      = s.ChapterNo;
        TxtChapterName.Text     = s.ChapterName;
        NumSectionNo.Value      = s.SectionNo;
        TxtSectionName.Text     = s.SectionName;
        NumStartPage.Value      = s.StartPage;
        NumEndPage.Value        = s.EndPage;
        NumEstimatedHours.Value = s.EstimatedHours;

        CmbSectionType.SelectedIndex = s.SectionType switch
        {
            "Exam"       => 1,
            "Assessment" => 2,
            "Event"      => 3,
            _            => 0,
        };

        if (s.PinnedDate.HasValue)
            DpPinnedDate.SelectedDate = s.PinnedDate.Value;

        TxtLearningObjective.Text = s.LearningObjective;
        TxtLessonPlan.Text        = s.LessonPlan;
        TxtMaterialPath.Text      = s.MaterialPath;
        TxtMaterialUrl.Text       = s.MaterialUrl;
        TxtMemo.Text              = s.Memo;
    }

    private CourseSection BuildSection()
    {
        var s = _isEdit && _existing is not null ? _existing : new CourseSection { Course = _course.No };

        s.UnitNo            = (int)(NumUnitNo.Value         ?? 1);
        s.UnitName          = TxtUnitName.Text?.Trim()       ?? "";
        s.ChapterNo         = (int)(NumChapterNo.Value       ?? 1);
        s.ChapterName       = TxtChapterName.Text?.Trim()    ?? "";
        s.SectionNo         = (int)(NumSectionNo.Value       ?? 1);
        s.SectionName       = TxtSectionName.Text?.Trim()    ?? "";
        s.StartPage         = (int)(NumStartPage.Value       ?? 0);
        s.EndPage           = (int)(NumEndPage.Value         ?? 0);
        s.EstimatedHours    = (int)(NumEstimatedHours.Value  ?? 1);
        s.LearningObjective = TxtLearningObjective.Text?.Trim() ?? "";
        s.LessonPlan        = TxtLessonPlan.Text?.Trim()    ?? "";
        s.MaterialPath      = TxtMaterialPath.Text?.Trim()  ?? "";
        s.MaterialUrl       = TxtMaterialUrl.Text?.Trim()   ?? "";
        s.Memo              = TxtMemo.Text?.Trim()          ?? "";

        string typeTag = (CmbSectionType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Normal";
        s.SectionType = typeTag;
        s.IsPinned    = typeTag is "Exam" or "Assessment";
        s.PinnedDate  = s.IsPinned ? DpPinnedDate.SelectedDate : null;

        return s;
    }

    // ────────────────────────────────────────────────────
    //  이벤트
    // ────────────────────────────────────────────────────

    private void OnSectionTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // InitializeComponent() 완료 전에 SelectionChanged가 발화할 수 있으므로 null 가드
        if (PinnedDatePanel is null) return;

        string tag = (CmbSectionType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Normal";
        PinnedDatePanel.IsVisible = tag is "Exam" or "Assessment";
    }

    private async void OnBrowseMaterial(object? sender, RoutedEventArgs e)
    {
        try
        {
            var tl    = TopLevel.GetTopLevel(this)!;
            var files = await tl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title          = "자료 파일 선택",
                AllowMultiple  = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("문서/프레젠테이션")
                    {
                        Patterns = new[] { "*.pptx", "*.ppt", "*.pdf", "*.docx", "*.doc", "*.hwp", "*.hwpx", "*.*" }
                    }
                }
            });
            if (files.Count > 0)
                TxtMaterialPath.Text = files[0].TryGetLocalPath() ?? files[0].Name;
        }
        catch (Exception ex) { Debug.WriteLine($"[CourseSectionDialog] Browse: {ex.Message}"); }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;

        if (string.IsNullOrWhiteSpace(TxtSectionName.Text))
        {
            ErrorText.Text      = "소단원명을 입력해 주세요.";
            ErrorText.IsVisible = true;
            return;
        }

        int sp = (int)(NumStartPage.Value ?? 0);
        int ep = (int)(NumEndPage.Value   ?? 0);
        if (sp > 0 && ep > 0 && sp > ep)
        {
            ErrorText.Text      = "시작 페이지가 끝 페이지보다 클 수 없습니다.";
            ErrorText.IsVisible = true;
            return;
        }

        string typeTag = (CmbSectionType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Normal";
        if (typeTag is "Exam" or "Assessment" && DpPinnedDate.SelectedDate is null)
        {
            ErrorText.Text      = "평가 단원은 고정 날짜를 설정해 주세요.";
            ErrorText.IsVisible = true;
            return;
        }

        try
        {
            var section = BuildSection();
            using var repo = new CourseSectionRepository(SchoolDatabase.DbPath);

            if (_isEdit)
            {
                await repo.UpdateAsync(section);
            }
            else
            {
                var existing = await repo.GetByCourseAsync(_course.No);
                section.SortOrder = existing.Count > 0 ? existing.Max(s => s.SortOrder) + 1 : 1;
                await repo.CreateAsync(section);
            }

            IsSuccess = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text      = $"저장 오류: {ex.Message}";
            ErrorText.IsVisible = true;
            Debug.WriteLine($"[CourseSectionDialog] Save: {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
