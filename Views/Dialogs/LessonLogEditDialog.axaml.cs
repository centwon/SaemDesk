using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 수업 차시 기록(LessonLog) 추가/편집 다이얼로그.
/// NewSchool LessonLogEditDialog 포팅 — Course 사전 선택 / 단원 선택은 단순 텍스트로 대체.
/// </summary>
public partial class LessonLogEditDialog : Window
{
    private LessonLog? _existing;

    /// <summary>저장된 LessonLog (성공 시 채워짐).</summary>
    public LessonLog? Result { get; private set; }
    /// <summary>삭제 여부.</summary>
    public bool Deleted { get; private set; }

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public LessonLogEditDialog() : this(null) { }

    /// <summary>기존 기록 편집.</summary>
    public LessonLogEditDialog(LessonLog? existing)
    {
        InitializeComponent();
        _existing = existing;

        if (existing is null)
        {
            HeaderText.Text            = "수업 기록 추가";
            Title                      = "수업 기록 추가";
            LogDatePicker.SelectedDate = new DateTimeOffset(DateTime.Today, TimeSpan.Zero);
            CBoxPeriod.SelectedIndex   = 0;
            BtnDelete.IsVisible        = false;
        }
        else
        {
            HeaderText.Text     = "수업 기록 수정";
            Title               = "수업 기록 수정";
            BtnDelete.IsVisible = true;
            Opened += (_, _) => LoadFrom(existing);
        }
    }

    /// <summary>
    /// 새 기록 생성 — 오늘의 수업 클릭 시 교시·학급·과목을 미리 채우는 팩토리 메서드.
    /// </summary>
    public static LessonLogEditDialog CreateNew(
        string subject = "",
        string room    = "",
        int    grade   = 0,
        int    cls     = 0,
        int    period  = 0)
    {
        var dlg = new LessonLogEditDialog(null);

        // 팩토리로 전달받은 초기값 덮어쓰기
        dlg.TxtSubject.Text = subject;
        dlg.TxtRoom.Text    = room;

        if (grade > 0) dlg.NumGrade.Value = grade;
        if (cls   > 0) dlg.NumClass.Value = cls;

        if (period >= 1 && period <= 7)
        {
            for (int i = 0; i < dlg.CBoxPeriod.ItemCount; i++)
            {
                if (dlg.CBoxPeriod.Items[i] is ComboBoxItem item &&
                    int.TryParse(item.Tag?.ToString(), out var p) && p == period)
                {
                    dlg.CBoxPeriod.SelectedIndex = i;
                    break;
                }
            }
        }

        return dlg;
    }

    // ────────────────────────────────────────────────────
    //  내부 메서드
    // ────────────────────────────────────────────────────

    private void LoadFrom(LessonLog l)
    {
        LogDatePicker.SelectedDate = new DateTimeOffset(l.Date.Date, TimeSpan.Zero);

        for (int i = 0; i < CBoxPeriod.ItemCount; i++)
        {
            if (CBoxPeriod.Items[i] is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out var p) && p == l.Period)
            {
                CBoxPeriod.SelectedIndex = i;
                break;
            }
        }

        TxtSubject.Text     = l.Subject;
        NumGrade.Value      = l.Grade;
        NumClass.Value      = l.Class;
        TxtRoom.Text        = l.Room;
        TxtSectionName.Text = l.SectionName;
        TxtTopic.Text       = l.Topic;
        TxtContent.Text     = l.Content;
        TxtNote.Text        = l.Note;
    }

    // ────────────────────────────────────────────────────
    //  이벤트 핸들러
    // ────────────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            string subject = (TxtSubject.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(subject))
            {
                StatusText.Text = "과목을 입력해 주세요.";
                return;
            }
            string topic = (TxtTopic.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(topic))
            {
                StatusText.Text = "주제를 입력해 주세요.";
                return;
            }
            if (LogDatePicker.SelectedDate is not DateTimeOffset dto)
            {
                StatusText.Text = "날짜를 선택해 주세요.";
                return;
            }

            int period = 1;
            if (CBoxPeriod.SelectedItem is ComboBoxItem pi &&
                int.TryParse(pi.Tag?.ToString(), out var p))
                period = p;

            var entity = _existing ?? new LessonLog
            {
                Year      = Settings.WorkYear.Value,
                Semester  = Math.Max(1, Settings.WorkSemester.Value),
                TeacherID = Settings.UserName.Value,
                CreatedAt = DateTime.Now,
            };
            entity.Date        = dto.Date;
            entity.Period      = period;
            entity.Subject     = subject;
            entity.Grade       = (int)(NumGrade.Value  ?? 0m);
            entity.Class       = (int)(NumClass.Value  ?? 0m);
            entity.Room        = (TxtRoom.Text        ?? "").Trim();
            entity.SectionName = (TxtSectionName.Text ?? "").Trim();
            entity.Topic       = topic;
            entity.Content     = (TxtContent.Text     ?? "").Trim();
            entity.Note        = (TxtNote.Text        ?? "").Trim();
            entity.UpdatedAt   = DateTime.Now;

            using var svc = new LessonLogService();
            if (_existing is null) entity.No = await svc.InsertAsync(entity);
            else                    await svc.UpdateAsync(entity);

            Result = entity;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"저장 실패: {ex.Message}";
            Debug.WriteLine($"[LessonLogEditDialog] {ex}");
        }
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (_existing is null) { Close(); return; }

        bool ok = await DialogService.ShowConfirmAsync(
            "차시 기록 삭제",
            $"'{_existing.Subject} - {_existing.Topic}' 기록을 삭제하시겠습니까?\n복구할 수 없습니다.");
        if (!ok) return;

        try
        {
            using var svc = new LessonLogService();
            await svc.DeleteAsync(_existing.No);
            Deleted = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"삭제 실패: {ex.Message}";
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
