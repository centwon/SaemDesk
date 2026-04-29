using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Helpers;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 학생 정보 출력 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.StudentInfoExportPage (WinUI3).
/// 학급 필터 + 출력항목 선택 + 미리보기(JoditEditor ReadOnly) + CSV/Excel/프린터 출력.
/// </summary>
public partial class StudentInfoExportPage : UserControl, IDisposable
{
    private bool _disposed;
    private DataTable? _data;
    private IReadOnlyList<Enrollment> _currentStudents = Array.Empty<Enrollment>();

    public StudentInfoExportPage()
    {
        InitializeComponent();
        FilterBar.SelectionChanged += OnFilterSelectionChanged;
        Unloaded += (_, _) => Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _data?.Dispose();
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────
    //  필터 변경
    // ────────────────────────────────────────────────────

    private void OnFilterSelectionChanged(object? sender, FilterChangedEventArgs e)
    {
        _currentStudents = e.Students;
        // 전체 반 선택 시 '학급' 컬럼 자동 체크
        if (e.IsAllClass)
            ChkShowClass.IsChecked = true;
    }

    // ────────────────────────────────────────────────────
    //  버튼 핸들러
    // ────────────────────────────────────────────────────

    private async void BtnPreview_Click(object? sender, RoutedEventArgs e)
    {
        if (!Validate()) return;
        try
        {
            await MakeDataAsync();
            MakePreview();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentInfoExportPage] Preview: {ex.Message}");
        }
    }

    private async void BtnExport_Click(object? sender, RoutedEventArgs e)
    {
        if (_data is null)
        {
            await ShowInfoAsync("먼저 미리보기를 생성해주세요.");
            return;
        }

        if (RbToExcel.IsChecked == true)
            await ExportToExcelAsync();
        else if (RbToPrinter.IsChecked == true)
            await PrintAsync();
        else
            await ShowInfoAsync("출력 대상을 선택해주세요.");
    }

    // ────────────────────────────────────────────────────
    //  유효성 검사
    // ────────────────────────────────────────────────────

    private bool Validate()
    {
        int year  = FilterBar.Year;
        int grade = FilterBar.Grade;
        if (year == 0 || grade == 0)
        {
            _ = ShowInfoAsync("학년도와 학년을 선택해주세요.");
            return false;
        }
        return true;
    }

    // ────────────────────────────────────────────────────
    //  DataTable 생성 (NewSchool MakeDataAsync 동등)
    // ────────────────────────────────────────────────────

    private async Task MakeDataAsync()
    {
        _data?.Dispose();
        _data = new DataTable();

        if (ChkShowNo.IsChecked    == true) _data.Columns.Add("연번",  typeof(int));
        if (ChkShowClass.IsChecked == true) _data.Columns.Add("학급",  typeof(int));

        _data.Columns.Add("번호", typeof(int));
        _data.Columns.Add("이름", typeof(string));

        // 선택된 항목 수집 (CheckBox.Tag 기반 — AOT 호환)
        var selectedItems = GetAllCheckBoxes(PanItem)
            .Where(c => c.IsChecked == true)
            .Select(c => (Tag: c.Tag?.ToString() ?? "", Label: c.Content?.ToString() ?? ""))
            .ToList();

        foreach (var (tag, label) in selectedItems)
            _data.Columns.Add(label, tag == "Birth" ? typeof(DateTime) : typeof(string));

        // 사용자 정의 항목
        if (ExpanderUserItem.IsExpanded)
        {
            foreach (var tb in new[] { TBoxUser1, TBoxUser2, TBoxUser3, TBoxUser4, TBoxUser5 })
                if (!string.IsNullOrWhiteSpace(tb.Text))
                    _data.Columns.Add(tb.Text!.Trim(), typeof(string));
        }

        if (ChkShowEtc.IsChecked == true) _data.Columns.Add("비고", typeof(string));

        await LoadStudentDataAsync(selectedItems.Select(i => i.Tag).ToList());
    }

    private async Task LoadStudentDataAsync(List<string> selectedTags)
    {
        if (_data is null) return;

        // FilterBar 이벤트에서 캐시된 학생 목록 사용 — DB 재조회 없음
        var enrollments = _currentStudents
            .OrderBy(e => e.Class).ThenBy(e => e.Number)
            .ToList();

        bool needDetail = selectedTags.Any(IsDetailProperty);
        var studentIds  = enrollments.Select(e => e.StudentID).ToList();

        using var studentSvc = new StudentService(SchoolDatabase.DbPath);
        var studentDict = (await studentSvc.GetStudentsByIdsAsync(studentIds))
            .ToDictionary(s => s.StudentID, s => s);

        Dictionary<string, StudentDetail> detailDict = [];
        if (needDetail)
        {
            using var detailSvc = new StudentDetailService(SchoolDatabase.DbPath);
            detailDict = (await detailSvc.GetByStudentIdsAsync(studentIds))
                .ToDictionary(d => d.StudentID, d => d);
        }

        for (int i = 0; i < enrollments.Count; i++)
        {
            var e = enrollments[i];
            if (!studentDict.TryGetValue(e.StudentID, out var student)) continue;
            detailDict.TryGetValue(e.StudentID, out var detail);

            var row  = _data.NewRow();
            int col  = 0;

            if (ChkShowNo.IsChecked    == true) row[col++] = i + 1;
            if (ChkShowClass.IsChecked == true) row[col++] = e.Class;

            row[col++] = e.Number;
            row[col++] = student.Name;

            foreach (var tag in selectedTags)
            {
                var val = GetPropertyValue(tag, student, detail);
                if (val is not null) row[col] = val;
                col++;
            }

            // 사용자 정의 항목은 빈 문자열 (수동 입력용)
            if (ExpanderUserItem.IsExpanded)
                foreach (var tb in new[] { TBoxUser1, TBoxUser2, TBoxUser3, TBoxUser4, TBoxUser5 })
                    if (!string.IsNullOrWhiteSpace(tb.Text)) col++;

            _data.Rows.Add(row);
        }
    }

    // ────────────────────────────────────────────────────
    //  미리보기 (NewSchool MakePreview / GenerateHtml 동등)
    // ────────────────────────────────────────────────────

    private void MakePreview()
    {
        if (_data is null) return;
        string html = GenerateHtml();
        PreviewWebView.NavigateToString(html);
    }

    private string GenerateHtml()
    {
        if (_data is null) return string.Empty;

        bool isLandscape = RbLandscape.IsChecked == true;
        string pageSize    = isLandscape ? "A4 landscape" : "A4";
        string fontSize    = isLandscape ? "9pt" : "10pt";
        string cellPadding = isLandscape ? "4px 3px" : "6px 4px";

        var sb = new StringBuilder();

        string title = string.IsNullOrWhiteSpace(TboxTitle.Text) ? "학생 정보" : TboxTitle.Text!.Trim();
        int year    = FilterBar.Year;
        int grade   = FilterBar.Grade;
        int classNo = FilterBar.ClassNum;
        string classInfo = classNo == 0
            ? $"{year}학년도 {grade}학년"
            : $"{year}학년도 {grade}학년 {classNo}반";

        sb.AppendLine($"<h1 style='text-align:center;font-size:16pt;margin-bottom:10px;'>{Enc(title)}</h1>");
        sb.AppendLine($"<p style='text-align:right;font-size:12pt;margin-bottom:20px;'>{Enc(classInfo)}</p>");
        sb.AppendLine($@"<style>
body{{font-family:'Malgun Gothic','Noto Sans KR',sans-serif;margin:0;padding:20px;}}
table{{width:100%;border-collapse:collapse;font-size:{fontSize};margin-top:10px;}}
th,td{{border:1px solid #000;padding:{cellPadding};text-align:center;}}
th{{background:#f0f0f0;font-weight:bold;}}
@media print{{@page{{size:{pageSize};margin:15mm;}}body{{margin:0;padding:0;}}}}
</style>");

        sb.Append("<table><thead><tr>");
        foreach (DataColumn col in _data.Columns)
            sb.Append($"<th>{Enc(col.ColumnName)}</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (DataRow row in _data.Rows)
        {
            sb.Append("<tr>");
            foreach (var cell in row.ItemArray)
            {
                string val = cell is DateTime dt && dt != default
                    ? dt.ToString("yyyy.M.d.")
                    : cell?.ToString() ?? string.Empty;
                sb.Append($"<td>{Enc(val)}</td>");
            }
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    private static string Enc(string s) => System.Net.WebUtility.HtmlEncode(s);

    // ────────────────────────────────────────────────────
    //  내보내기
    // ────────────────────────────────────────────────────

    private async Task ExportToExcelAsync()
    {
        if (_data is null) return;
        try
        {
            string title    = string.IsNullOrWhiteSpace(TboxTitle.Text) ? "학생정보" : TboxTitle.Text!.Trim();
            int year        = FilterBar.Year;
            int grade       = FilterBar.Grade;
            int classNo     = FilterBar.ClassNum;
            string subtitle = classNo == 0
                ? $"{year}학년도 {grade}학년 전체"
                : $"{year}학년도 {grade}학년 {classNo}반";

            bool ok = await ExcelHelpers.SaveDataTableToExcelAsync(
                App.FilePicker, _data, title, subtitle, openAfterSave: true);

            if (!ok) await ShowInfoAsync("엑셀 저장이 취소됐거나 실패했습니다.");
        }
        catch (Exception ex)
        {
            await ShowInfoAsync($"엑셀 내보내기 실패: {ex.Message}");
        }
    }

    private async Task PrintAsync()
    {
        try { await PreviewWebView.InvokeScript("window.print()"); }
        catch (Exception ex) { await ShowInfoAsync($"인쇄 실패: {ex.Message}"); }
    }

    // ────────────────────────────────────────────────────
    //  속성 매핑 (AOT 호환 — Reflection 없음)
    // ────────────────────────────────────────────────────

    private static object? GetPropertyValue(string tag, Student s, StudentDetail? d) => tag switch
    {
        "Sex"              => s.Sex,
        "Birth"            => s.BirthDate,
        "Phone"            => s.Phone,
        "Email"            => s.Email,
        "Address"          => s.Address,
        "Memo"             => s.Memo,
        "GuardianName"     => d?.GuardianName,
        "GuardianRelation" => d?.GuardianRelation,
        "GuardianPhone"    => d?.GuardianPhone,
        "FatherName"       => d?.FatherName,
        "FatherPhone"      => d?.FatherPhone,
        "FatherJob"        => d?.FatherJob,
        "MotherName"       => d?.MotherName,
        "MotherPhone"      => d?.MotherPhone,
        "MotherJob"        => d?.MotherJob,
        "Interest"         => d?.Interests,
        "Talents"          => d?.Talents,
        "CareerHope"       => d?.CareerGoal,
        "Family"           => d?.FamilyInfo,
        "Friends"          => d?.Friends,
        "HealthInfo"       => d?.HealthInfo,
        "Allergies"        => d?.Allergies,
        "SpecialNeeds"     => d?.SpecialNeeds,
        _                  => null
    };

    private static bool IsDetailProperty(string tag) => tag switch
    {
        "GuardianName" or "GuardianRelation" or "GuardianPhone"
        or "FatherName" or "FatherPhone" or "FatherJob"
        or "MotherName" or "MotherPhone" or "MotherJob"
        or "Family" or "Friends" or "Interest" or "Talents"
        or "CareerHope" or "HealthInfo" or "Allergies"
        or "SpecialNeeds" => true,
        _ => false
    };

    // ────────────────────────────────────────────────────
    //  헬퍼
    // ────────────────────────────────────────────────────

    private static List<CheckBox> GetAllCheckBoxes(Control parent)
    {
        var result = new List<CheckBox>();
        if (parent is CheckBox cb) { result.Add(cb); return result; }
        if (parent is Panel p)
            foreach (var child in p.Children)
                result.AddRange(GetAllCheckBoxes(child));
        return result;
    }

    private async Task ShowInfoAsync(string msg)
    {
        var dlg = new Dialogs.ConfirmDialog("알림", msg);
        var w   = TopLevel.GetTopLevel(this) as Window;
        if (w is not null) await dlg.ShowDialog(w);
    }
}
