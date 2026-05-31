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

    /// <summary>단순 알림 다이얼로그 (확인 버튼만). UserControl owner는 무시됨.</summary>
    public static async Task ShowInfoAsync(string message, UserControl? _ = null)
    {
        var dialog = new ConfirmDialog("알림", message);
        var owner = MainWindow;
        if (owner is null) return;
        await dialog.ShowDialog(owner);
    }

    /// <summary>커스텀 컨텐츠 다이얼로그. UserControl owner는 무시됨.</summary>
    public static async Task<bool> ShowCustomAsync(string title, Control content, UserControl? _ = null)
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        panel.Children.Add(content);

        var okBtn = new Button { Content = "확인", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        okBtn.Classes.Add("accent");
        var cancelBtn = new Button { Content = "취소" };
        var btnRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(okBtn);
        panel.Children.Add(btnRow);

        bool result = false;
        var win = new Window
        {
            Title = title,
            Content = panel,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        okBtn.Click += (_, _) => { result = true; win.Close(); };
        cancelBtn.Click += (_, _) => { result = false; win.Close(); };

        var owner = MainWindow;
        if (owner is null) return false;
        await win.ShowDialog(owner);
        return result;
    }

    /// <summary>한 줄 텍스트 입력 다이얼로그. 확인 시 입력값(트림), 취소·빈 값 시 null.</summary>
    public static async Task<string?> ShowInputAsync(string title, string prompt, string defaultText = "")
    {
        var owner = MainWindow;
        if (owner is null) return null;

        var input = new TextBox { Text = defaultText, MinWidth = 320 };

        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(input);

        var okBtn = new Button { Content = "확인" };
        okBtn.Classes.Add("accent");
        var cancelBtn = new Button { Content = "취소" };
        var btnRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(okBtn);
        panel.Children.Add(btnRow);

        string? result = null;
        var win = new Window
        {
            Title = title,
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        void Confirm() { result = input.Text?.Trim() ?? string.Empty; win.Close(); }
        okBtn.Click     += (_, _) => Confirm();
        cancelBtn.Click += (_, _) => win.Close();
        input.KeyDown   += (_, ev) =>
        {
            if (ev.Key == Avalonia.Input.Key.Enter)       Confirm();
            else if (ev.Key == Avalonia.Input.Key.Escape) win.Close();
        };
        win.Opened += (_, _) => { input.Focus(); input.SelectAll(); };

        await win.ShowDialog(owner);
        return string.IsNullOrEmpty(result) ? null : result;
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
    public static async Task ShowStudentDetailAsync(
        string studentId, string classInfo, string studentName, int year = 0)
    {
        var dialog = new StudentDetailDialog(studentId, classInfo, studentName, year);
        var owner  = MainWindow;
        if (owner is null) return;
        await dialog.ShowDialog(owner);
    }

    /// <summary>게시글 상세 보기 다이얼로그(닫기 전용).</summary>
    public static async Task ShowPostDetailAsync(int postNo)
    {
        var dialog = new PostDetailDialog(postNo);
        var owner  = MainWindow;
        if (owner is null) return;
        await dialog.ShowDialog(owner);
    }

    /// <summary>수강생 관리 다이얼로그. 저장 여부를 반환한다.</summary>
    public static async Task<bool> ShowCourseEnrollmentAsync(SaemDesk.Models.Course course)
    {
        var dialog = new CourseEnrollmentDialog(course);
        var owner  = MainWindow;
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return dialog.IsSuccess;
    }

    /// <summary>시간표 배치 다이얼로그. 저장 여부를 반환한다.</summary>
    public static async Task<bool> ShowCourseScheduleAsync(SaemDesk.Models.Course course)
    {
        var dialog = new CourseScheduleDialog(course);
        var owner  = MainWindow;
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return dialog.IsSuccess;
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

    /// <summary>통합 일정/할일 편집 다이얼로그.</summary>
    public static async Task<(SaemDesk.Scheduler.KEvent? Saved, bool Deleted)> ShowUnifiedItemEditAsync(DateTime date)
    {
        var dialog = new UnifiedItemDialog(date);
        var owner  = MainWindow;
        if (owner is null) return (null, false);
        await dialog.ShowDialog(owner);
        return (dialog.ResultEvent, dialog.Deleted);
    }

    /// <summary>통합 일정/할일 편집 다이얼로그 — 캘린더 미리 지정 (KAgendaControl FixedCalendarName 용).</summary>
    public static async Task<(SaemDesk.Scheduler.KEvent? Saved, bool Deleted)> ShowUnifiedItemEditAsync(
        DateTime date, int defaultCalendarId)
    {
        var dialog = new UnifiedItemDialog(date, defaultCalendarId);
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

    /// <summary>동아리 추가/편집 다이얼로그. 저장 여부를 반환한다.</summary>
    public static async Task<bool> ShowClubEditAsync(
        string schoolCode, string teacherId, int year, SaemDesk.Models.Club? existing = null)
    {
        var dialog = existing is null
            ? new ClubEditDialog(schoolCode, teacherId, year)
            : new ClubEditDialog(existing);
        var owner = MainWindow;
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return dialog.IsSuccess;
    }

    /// <summary>동아리 부원 관리 다이얼로그.</summary>
    public static async Task ShowClubEnrollmentAsync(SaemDesk.Models.Club club)
    {
        var dialog = new ClubEnrollmentDialog(club);
        var owner  = MainWindow;
        if (owner is null) return;
        await dialog.ShowDialog(owner);
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
