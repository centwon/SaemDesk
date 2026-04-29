using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학급일지 작성/수정 다이얼로그.
/// 기존 일지를 전달하면 수정 모드, null이면 신규 작성 모드.
/// </summary>
public partial class DiaryEditDialog : Window
{
    // ── 결과 프로퍼티 ─────────────────────────────────────
    /// <summary>저장된 일지. 취소하거나 오류 시 null.</summary>
    public ClassDiary? Result { get; private set; }

    private readonly ClassDiary _diary;
    private bool _isSaving;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    /// <summary>
    /// 신규 작성 모드 (오늘 날짜로 빈 일지 초기화).
    /// </summary>
    public DiaryEditDialog() : this(null) { }

    /// <summary>
    /// 수정 모드 (기존 일지를 불러와 채운다).
    /// </summary>
    public DiaryEditDialog(ClassDiary? existing)
    {
        InitializeComponent();

        bool isNew = existing is null;
        var date   = existing?.Date ?? DateTime.Today;

        // 제목
        TitleText.Text    = isNew ? "오늘 학급일지 작성" : $"{date:M월 d일} 학급일지 수정";
        SubTitleText.Text = $"{Settings.HomeGrade}학년 {Settings.HomeRoom}반 · {date:yyyy년 M월 d일}";

        // 편집용 복사본 생성 (새 일지면 기본값 채움)
        _diary = existing?.Clone() ?? new ClassDiary(
            schoolCode : Settings.SchoolCode.Value,
            year       : Settings.WorkYear.Value,
            semester   : Settings.WorkSemester.Value,
            grade      : Settings.HomeGrade.Value,
            classNum   : Settings.HomeRoom.Value,
            date       : DateTime.Today,
            teacherId  : string.IsNullOrWhiteSpace(Settings.UserName.Value)
                           ? "teacher"
                           : Settings.UserName.Value);

        // 폼 초기값
        AbsentBox.Text     = _diary.Absent;
        LateBox.Text       = _diary.Late;
        LeaveEarlyBox.Text = _diary.LeaveEarly;
        MemoBox.Text       = _diary.Memo;
        NoticeBox.Text     = _diary.Notice;
    }

    // ────────────────────────────────────────────────────
    //  이벤트 핸들러
    // ────────────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;
        _isSaving = true;
        SaveButton.IsEnabled = false;

        try
        {
            _diary.Absent     = AbsentBox.Text     ?? string.Empty;
            _diary.Late       = LateBox.Text       ?? string.Empty;
            _diary.LeaveEarly = LeaveEarlyBox.Text ?? string.Empty;
            _diary.Memo       = MemoBox.Text       ?? string.Empty;
            _diary.Notice     = NoticeBox.Text     ?? string.Empty;
            _diary.UpdatedAt  = DateTime.Now;

            using var svc = new ClassDiaryService(SchoolDatabase.DbPath);
            var saved = await svc.CreateOrUpdateAsync(_diary);
            Result    = saved;
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiaryEditDialog] 저장 오류: {ex.Message}");
            SaveButton.IsEnabled = true;
            _isSaving = false;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
