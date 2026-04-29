using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SaemDesk.Models;
using SaemDesk.Views.Dialogs;
using System.Threading.Tasks;
using System;

namespace SaemDesk.Services;

/// <summary>
/// ContentDialog 대체. 모달 다이얼로그를 메인 윈도우 위에 띄운다.
/// </summary>
public static class DialogService
{
    public static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
        ?.MainWindow;

    public static async Task<TResult?> ShowAsync<TDialog, TResult>(TDialog dialog)
        where TDialog : Window
    {
        var owner = MainWindow;
        if (owner is null) return default;

        await dialog.ShowDialog(owner);

        return dialog is IDialogResult<TResult> result ? result.Result : default;
    }

    public static Task ShowAsync<TDialog>(TDialog dialog)
        where TDialog : Window
    {
        var owner = MainWindow;
        if (owner is null) return Task.CompletedTask;
        return dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message);
        var owner = MainWindow;
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    /// <summary>NEIS 학교 검색 다이얼로그를 열고 선택된 학교를 반환한다. 취소 시 null.</summary>
    public static async Task<School?> ShowSchoolSearchAsync()
    {
        var dialog = new SchoolSearchDialog();
        var owner  = MainWindow;
        if (owner is null) return null;
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    /// <summary>학급일지 작성/수정 다이얼로그를 열고 저장된 일지를 반환한다. 취소 시 null.</summary>
    public static async Task<ClassDiary?> ShowDiaryEditAsync(ClassDiary? existing = null)
    {
        var dialog = new DiaryEditDialog(existing);
        var owner  = MainWindow;
        if (owner is null) return null;
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    /// <summary>학생 추가/수정 다이얼로그를 열고 저장 여부를 반환한다.</summary>
    public static async Task<bool> ShowStudentEditAsync(
        SaemDesk.ViewModels.StudentListItemViewModel? existing = null)
    {
        var dialog = new StudentEditDialog(existing);
        var owner  = MainWindow;
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return dialog.Saved;
    }

    /// <summary>학생 상세 다이얼로그(상세 정보 + 활동 기록). 닫기만 있는 viewer.</summary>
    public static async Task ShowStudentDetailAsync(string studentId, string studentName)
    {
        var dialog = new StudentDetailDialog(studentId, studentName);
        var owner  = MainWindow;
        if (owner is null) return;
        await dialog.ShowDialog(owner);
    }

    /// <summary>게시글 작성/수정 다이얼로그. 저장 여부를 반환한다.</summary>
    public static async Task<bool> ShowPostEditAsync(SaemDesk.Models.Post? existing = null)
    {
        var dialog = new PostEditDialog(existing);
        var owner  = MainWindow;
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return dialog.Saved;
    }

    /// <summary>게시글 상세 보기 다이얼로그(닫기 전용).</summary>
    public static async Task ShowPostDetailAsync(int postNo)
    {
        var dialog = new PostDetailDialog(postNo);
        var owner  = MainWindow;
        if (owner is null) return;
        await dialog.ShowDialog(owner);
    }

    /// <summary>수업(Course) 추가/편집 다이얼로그. 저장된 Course 반환(취소 시 null).</summary>
    public static async Task<SaemDesk.Models.Course?> ShowCourseEditAsync(
        string schoolCode, string teacherId, int year, int semester, SaemDesk.Models.Course? existing = null)
    {
        var dialog = existing is null
            ? new CourseEditDialog(schoolCode, teacherId, year, semester)
            : new CourseEditDialog(existing);
        var owner = MainWindow;
        if (owner is null) return null;
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    /// <summary>수업 차시 기록(LessonLog) 추가/편집 다이얼로그. (저장된 항목, 삭제 여부) 반환.</summary>
    public static async Task<(SaemDesk.Models.LessonLog? Saved, bool Deleted)> ShowLessonLogEditAsync(
        SaemDesk.Models.LessonLog? existing = null)
    {
        var dialog = new LessonLogEditDialog(existing);
        var owner  = MainWindow;
        if (owner is null) return (null, false);
        await dialog.ShowDialog(owner);
        return (dialog.Result, dialog.Deleted);
    }

    /// <summary>학생 누가기록 작성/수정 다이얼로그. 저장 여부 반환.</summary>
    public static async Task<bool> ShowStudentLogEditAsync(
        string studentId, string studentName, SaemDesk.Models.StudentLog? existing = null)
    {
        var dialog = new StudentLogEditDialog(
            studentId ?? string.Empty,
            string.IsNullOrEmpty(studentName) ? "이름 미상" : studentName,
            existing);
        var owner  = MainWindow;
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return dialog.Result is not null;
    }

    /// <summary>학급 단위 통합 내보내기 다이얼로그(닫기 전용).</summary>
    public static async Task ShowExportAsync()
    {
        var dialog = new ExportDialog();
        var owner  = MainWindow;
        if (owner is null) return;
        await dialog.ShowDialog(owner);
    }

    /// <summary>일정 설정(Google Calendar 연동 포함) 다이얼로그(닫기 전용).</summary>
    public static async Task ShowCalendarSettingsAsync()
    {
        var dialog = new CalendarSettingsDialog();
        var owner  = MainWindow;
        if (owner is null) return;
        await dialog.ShowDialog(owner);
    }

    /// <summary>
    /// 통합 일정/할일 편집 다이얼로그. 새 항목(date), 또는 기존 KEvent 수정.
    /// 반환: (저장된 KEvent or null, 삭제 여부).
    /// </summary>
    public static async Task<(SaemDesk.Scheduler.KEvent? Saved, bool Deleted)> ShowUnifiedItemEditAsync(DateTime date)
    {
        var dialog = new UnifiedItemDialog(date);
        var owner  = MainWindow;
        if (owner is null) return (null, false);
        await dialog.ShowDialog(owner);
        return (dialog.ResultEvent, dialog.Deleted);
    }

    public static async Task<(SaemDesk.Scheduler.KEvent? Saved, bool Deleted)> ShowUnifiedItemEditAsync(SaemDesk.Scheduler.KEvent existing)
    {
        var dialog = new UnifiedItemDialog(existing);
        var owner  = MainWindow;
        if (owner is null) return (null, false);
        await dialog.ShowDialog(owner);
        return (dialog.ResultEvent, dialog.Deleted);
    }

    /// <summary>시간표(한 칸) 추가/수정 다이얼로그. 저장된 슬롯을 반환한다. 취소 시 null.</summary>
    public static async Task<ClassTimetable?> ShowTimetableEditAsync(
        ClassTimetable? existing = null,
        int defaultDay = 1,
        int defaultPeriod = 1)
    {
        var dialog = new TimetableEditDialog(existing, defaultDay, defaultPeriod);
        var owner  = MainWindow;
        if (owner is null) return null;
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }
}

/// <summary>
/// 결과값을 반환하는 다이얼로그가 구현하는 인터페이스.
/// </summary>
public interface IDialogResult<out T>
{
    T Result { get; }
}
