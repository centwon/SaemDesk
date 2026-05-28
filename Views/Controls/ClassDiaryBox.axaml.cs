using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SaemDesk.Services;
using SaemDesk.ViewModels;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학급일지 입력 컨트롤 — Avalonia 12 이식.
/// 출결(결석/지각/조퇴) + 메모 + 알림장(JoditEditor) + 시간표.
/// </summary>
public partial class ClassDiaryBox : UserControl
{
    public ClassDiaryViewModel ViewModel { get; }

    private bool _isChanged;

    public ClassDiaryBox()
    {
        InitializeComponent();
        ViewModel = new ClassDiaryViewModel();
        DataContext = ViewModel;

        NoticeBox.TextChanged += NoticeBox_TextChanged;
        Unloaded += OnUnloaded;
    }

    /// <summary>특정 날짜의 학급일지 로드.</summary>
    public async Task LoadDiaryAsync(int grade, int classNumber, DateTime date)
    {
        if (_isChanged) await SaveDiaryAsync();

        await ViewModel.LoadDiaryAsync(grade, classNumber, date);

        NoticeBox.Text = ViewModel.Notice ?? string.Empty;
        UpdateNoticePreview();

        await LoadTimetableAsync(grade, classNumber, Settings.WorkYear);
        _isChanged = false;
        ResetTextBoxStyles();
    }

    private async Task LoadTimetableAsync(int grade, int classNumber, int year)
    {
        if (grade == 0 || classNumber == 0 || year == 0) return;

        using var service = new TimetableService(SchoolDatabase.DbPath);
        var timeset = await service.GetClassTimetableAsync(
            Settings.SchoolCode, year, Settings.WorkSemester, grade, classNumber);

        ClassTimeTable.DataContext = timeset;
    }

    public async Task SaveDiaryAsync()
    {
        if (!_isChanged) return;

        // Grade/Class가 유효하지 않으면 (초기 빈 상태) 저장 안 함
        if (ViewModel.Grade <= 0 || ViewModel.Class <= 0) return;

        ViewModel.Notice = NoticeBox.Text;
        await ViewModel.SaveDiaryAsync();

        _isChanged = false;
        ResetTextBoxStyles();
    }

    private void ResetTextBoxStyles()
    {
        TBoxAbsent.FontStyle = FontStyle.Normal;
        TBoxLate.FontStyle = FontStyle.Normal;
        TBoxLeaveEarly.FontStyle = FontStyle.Normal;
        TBoxMemo.FontStyle = FontStyle.Normal;
    }

    private void MarkTextBoxAsChanged(TextBox tb)
    {
        _isChanged = true;
        tb.FontStyle = FontStyle.Italic;
    }

    private void OnAttendanceTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb) MarkTextBoxAsChanged(tb);
    }

    private void OnMemoTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb) MarkTextBoxAsChanged(tb);
    }

    private void NoticeBox_TextChanged(object? sender, string e)
    {
        _isChanged = true;
        UpdateNoticePreview();
    }

    private void UpdateNoticePreview()
    {
        string content = NoticeBox.Text ?? string.Empty;
        string plainText = StripHtmlTags(content);

        TxtNoticePreview.Text = string.IsNullOrWhiteSpace(plainText)
            ? "(내용 없음)"
            : (plainText.Length > 50 ? plainText.Substring(0, 50) + "..." : plainText);
    }

    private static string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        string text = Regex.Replace(html, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    /// <summary>알림장 전체 편집 버튼 — JoditEditorWin 으로 헤더 포함 전체화면 편집 (NewSchool 원본 동등).</summary>
    private async void BtnNoticeEdit_Click(object? sender, RoutedEventArgs e)
    {
        var editorWin = new JoditEditorWin(
            "알림장 편집",
            BuildNoticeHeaderHtml() + "<br>" + NoticeBox.Text,
            JoditEditor.EditorMode.Full);

        editorWin.SetSize(1000, 800);

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        bool result = await editorWin.ShowDialogAsync(owner);

        if (result)
        {
            string content = RemoveNoticeHeaderHtml(editorWin.Text);
            NoticeBox.Text = content;
            _isChanged = true;
            UpdateNoticePreview();
        }
    }

    /// <summary>알림장 헤더 HTML 생성 (학년/반/날짜 정보).</summary>
    private string BuildNoticeHeaderHtml()
    {
        string dateStr = ViewModel.Date.ToString("yyyy년 M월 d일(ddd)");
        return $@"<table style='border-collapse:collapse;width:100%;border:0;' data-notice-header='true'>
                <tbody>
                    <tr>
                        <td style='width:100%;text-align:center;border:none;' colspan='2'>
                            <span style='font-size:18px;'>알 림 장</span>
                        </td>
                    </tr>
                    <tr>
                        <td style='width:50%;border:none;'>
                            <span style='font-size:16px;'>{ViewModel.Grade}학년 {ViewModel.Class}반</span>
                        </td>
                        <td style='width:50%;text-align:right;border:none;'>
                            <span style='font-size:16px;'>{dateStr}</span>
                        </td>
                    </tr>
                </tbody>
            </table>";
    }

    /// <summary>알림장 헤더 HTML 제거 (정규식, Native AOT 호환).</summary>
    private static string RemoveNoticeHeaderHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        html = Regex.Replace(html,
            @"<table[^>]*data-notice-header[^>]*>.*?</table>",
            string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // fallback: "알 림 장" 텍스트가 있는 테이블 제거
        html = Regex.Replace(html,
            @"<table[^>]*>\s*<tbody>\s*<tr>\s*<td[^>]*>\s*<span[^>]*>알[\s\u00A0]*림[\s\u00A0]*장</span>.*?</table>",
            string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        html = Regex.Replace(html, @"^(\s*<br\s*/?>\s*)+", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"(\s*<br\s*/?>\s*)+$", string.Empty, RegexOptions.IgnoreCase);

        return html.Trim();
    }

    private async void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_isChanged) await SaveDiaryAsync();
    }
}
