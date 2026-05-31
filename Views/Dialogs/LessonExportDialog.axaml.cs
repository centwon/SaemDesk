using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 수업기록 내보내기 다이얼로그.
/// 묶음 단위(진도표/기간일지) × 형식(Excel/PDF/HTML) → 형식별 서비스로 위임.
/// 진도표는 과목·강의실 필터(강의실 "전체" 포함)로 선택. 미리보기는 HTML을 브라우저로 연다.
/// </summary>
public partial class LessonExportDialog : Window
{
    private const string AllLabel = "전체";

    private string? _lastPath;
    private bool _isBusy;
    private bool _updatingFilters;

    public LessonExportDialog()
    {
        InitializeComponent();

        SubTitleText.Text = $"{Settings.WorkYear.Value}학년도 · {Settings.WorkSemester.Value}학기";

        var today = DateTime.Today;
        DpFrom.SelectedDate = ToOffset(new DateTime(today.Year, today.Month, 1));
        DpTo.SelectedDate   = ToOffset(today);

        Opened += OnOpenedLoadFilters;
    }

    // Local DateTime + Zero offset 예외 방지 (Kind=Unspecified)
    private static DateTimeOffset ToOffset(DateTime d)
        => new(DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified), TimeSpan.Zero);

    // ── 과목/강의실 필터 로드 ──
    private async void OnOpenedLoadFilters(object? sender, EventArgs e)
    {
        _updatingFilters = true;
        try
        {
            using var svc = new CourseService();
            var courses = await svc.GetMyCoursesAsync();
            var subjects = courses
                .Select(c => c.Subject)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            CboSubject.ItemsSource = subjects;
            if (subjects.Count > 0) CboSubject.SelectedIndex = 0;

            CboRoom.ItemsSource = new List<string> { AllLabel };
            CboRoom.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LessonExportDialog] 과목 로드 실패: {ex.Message}");
        }
        finally { _updatingFilters = false; }

        await RefreshRoomsAsync();
    }

    private async void OnSubjectChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingFilters) return;
        await RefreshRoomsAsync();
    }

    /// <summary>선택 과목의 강의실 목록(+전체)으로 강의실 콤보 갱신.</summary>
    private async Task RefreshRoomsAsync()
    {
        string subject = CboSubject.SelectedItem as string ?? string.Empty;
        var rooms = new List<string> { AllLabel };
        if (!string.IsNullOrWhiteSpace(subject))
        {
            try
            {
                using var svc = new LessonLogService();
                rooms.AddRange(await svc.GetRoomsAsync(subject));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LessonExportDialog] 강의실 로드 실패: {ex.Message}");
            }
        }

        _updatingFilters = true;
        CboRoom.ItemsSource = rooms;
        CboRoom.SelectedIndex = 0;
        _updatingFilters = false;
    }

    private void OnModeChanged(object? sender, RoutedEventArgs e)
    {
        if (ProgressPanel is null || JournalPanel is null) return;
        bool progress = RbProgress.IsChecked == true;
        ProgressPanel.IsVisible = progress;
        JournalPanel.IsVisible  = !progress;
    }

    // ── 현재 선택값 ──
    private bool     IsProgress => RbProgress.IsChecked == true;
    private string   Subject    => CboSubject.SelectedItem as string ?? string.Empty;
    private string?  Room       => CboRoom.SelectedItem as string is string r && r != AllLabel ? r : null;
    private DateTime From       => DpFrom.SelectedDate is DateTimeOffset d ? d.Date : DateTime.Today;
    private DateTime To         => DpTo.SelectedDate   is DateTimeOffset d ? d.Date : DateTime.Today;
    private int      Year       => Settings.WorkYear.Value;
    private int      Semester   => Settings.WorkSemester.Value;
    private string   Format     => (FormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Excel";

    private bool Validate()
    {
        if (IsProgress && string.IsNullOrWhiteSpace(Subject))
        {
            StatusText.Text = "과목을 선택해주세요.";
            return false;
        }
        return true;
    }

    // ── 미리보기: HTML → 임시파일 → 브라우저 ──
    private async void OnPreview(object? sender, RoutedEventArgs e)
    {
        if (_isBusy || !Validate()) return;
        await RunAsync(async () =>
        {
            var html = new HtmlExportService();
            string content;
            if (IsProgress)
            {
                var logs = await LessonLogExportService.LoadProgressAsync(Semester, Subject, Room);
                if (logs.Count == 0) { StatusText.Text = "해당 조건의 기록이 없습니다."; return; }
                content = html.BuildLessonProgressHtml(Year, Subject, Room, logs);
            }
            else
            {
                var logs = await LessonLogExportService.LoadJournalAsync(Semester, From, To);
                if (logs.Count == 0) { StatusText.Text = "해당 기간의 기록이 없습니다."; return; }
                content = html.BuildLessonJournalHtml(Year, From, To, logs);
            }

            var tmp = Path.Combine(Path.GetTempPath(), $"수업기록미리보기_{DateTime.Now:yyyyMMddHHmmss}.html");
            File.WriteAllText(tmp, content, Encoding.UTF8);
            Process.Start(new ProcessStartInfo { FileName = tmp, UseShellExecute = true });
            StatusText.Text = "브라우저에서 미리보기를 열었습니다.";
        });
    }

    // ── 내보내기 ──
    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (_isBusy || !Validate()) return;
        await RunAsync(async () =>
        {
            string? path;
            if (IsProgress)
            {
                var logs = await LessonLogExportService.LoadProgressAsync(Semester, Subject, Room);
                path = Format switch
                {
                    "Pdf"  => new LessonLogPrintService().GenerateProgressPdf(Year, Subject, Room, logs),
                    "Html" => logs.Count == 0 ? null : new HtmlExportService().ExportLessonProgressToHtml(Year, Subject, Room, logs),
                    _      => new LessonLogExportService().ExportProgressToExcel(Subject, Room, logs),
                };
            }
            else
            {
                var logs = await LessonLogExportService.LoadJournalAsync(Semester, From, To);
                path = Format switch
                {
                    "Pdf"  => new LessonLogPrintService().GenerateJournalPdf(Year, From, To, logs),
                    "Html" => logs.Count == 0 ? null : new HtmlExportService().ExportLessonJournalToHtml(Year, From, To, logs),
                    _      => new LessonLogExportService().ExportJournalToExcel(From, To, logs),
                };
            }

            if (string.IsNullOrEmpty(path))
            {
                StatusText.Text = "내보낼 데이터가 없습니다.";
                _lastPath = null;
                ResultPathText.IsVisible = false;
                OpenFolderButton.IsEnabled = false;
                return;
            }

            _lastPath = path;
            StatusText.Text = "내보내기가 완료되었습니다.";
            ResultPathText.Text = path;
            ResultPathText.IsVisible = true;
            OpenFolderButton.IsEnabled = true;
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        _isBusy = true;
        BusyBar.IsVisible = true;
        ExportButton.IsEnabled = false;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"실패: {ex.Message}";
            Debug.WriteLine($"[LessonExportDialog] {ex}");
        }
        finally
        {
            BusyBar.IsVisible = false;
            ExportButton.IsEnabled = true;
            _isBusy = false;
        }
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = !string.IsNullOrEmpty(_lastPath)
                ? Path.GetDirectoryName(_lastPath)
                : Path.Combine(Settings.UserDataPath, "Exports");
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"폴더를 열 수 없습니다: {ex.Message}";
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
