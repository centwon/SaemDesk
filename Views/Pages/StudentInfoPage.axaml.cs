using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Helpers;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;
using SaemDesk.Views.Dialogs;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 학생 정보 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.PageStudentInfo (WinUI3).
/// 구성: 좌측 ListStudent + 우측 상단 StudentCard + 우측 하단 LogListViewer.
/// </summary>
public partial class StudentInfoPage : UserControl, IDisposable
{
    private bool _disposed;
    private StudentInfoPageVM VM => (StudentInfoPageVM)DataContext!;

    private string? _currentStudentId;
    private int _currentYear;
    private int _currentGrade;
    private int _currentClass;

    // StudentCard의 ViewModel에 직접 접근
    private StudentCardViewModel? CardVM => SCard.ViewModel;

    private bool _isSaving;

    public StudentInfoPage()
    {
        InitializeComponent();
        DataContext = new StudentInfoPageVM();

        StudentList.StudentSelected += OnStudentSelected;

        // 반 선택 시 자동 로드
        FilterBar.SelectionChanged += OnFilterBarChanged;

        LogList.StudentInfoMode = StudentInfoMode.HideAll;
        LogList.Category        = LogCategory.전체;

        // StudentCard는 생성자에서 DataContext(StudentCardViewModel)를 고정 설정하므로
        // Loaded 이후 바로 구독 가능
        Loaded += (_, _) =>
        {
            if (CardVM is { } vm)
                vm.PropertyChanged += OnCardVMPropertyChanged;
        };

        Unloaded += async (_, _) =>
        {
            await SaveChangedAsync();
            if (CardVM is { } vm)
                vm.PropertyChanged -= OnCardVMPropertyChanged;
            Dispose();
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StudentList.StudentSelected -= OnStudentSelected;
        CardVM?.Dispose();
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────
    //  필터 — 반 선택 시 자동 로드 (조회 버튼 없음)
    // ────────────────────────────────────────────────────

    private async void OnFilterBarChanged(object? sender, FilterChangedEventArgs e)
    {
        _currentYear  = e.Year;
        _currentGrade = e.Grade;
        _currentClass = e.Class;
        if (_currentYear > 0 && _currentGrade > 0 && _currentClass > 0)
            await LoadStudentListAsync();
    }

    // ────────────────────────────────────────────────────
    //  학생 목록 로드
    // ────────────────────────────────────────────────────

    private async Task LoadStudentListAsync()
    {
        try
        {
            using var svc = new EnrollmentService();
            var roster = await svc.GetClassRosterAsync(
                Settings.SchoolCode.Value, _currentYear, _currentGrade, _currentClass);
            StudentList.LoadStudents(roster);

            CardVM?.Clear();
            LogList.Clear();
            _currentStudentId = null;
            EnableButtons(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentInfoPage] LoadStudents: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  학생 선택
    // ────────────────────────────────────────────────────

    private async void OnStudentSelected(object? sender, Enrollment enrollment)
    {
        if (_isSaving) return; // 저장 중 재선택 무시
        await SaveChangedAsync();

        _currentStudentId = enrollment.StudentID;

        try
        {
            if (CardVM is not null)
            {
                await CardVM.LoadStudentAsync(enrollment.StudentID);
            }
            await LoadStudentLogsAsync(enrollment.StudentID);
            EnableButtons(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentInfoPage] OnStudentSelected: {ex.Message}");
        }
    }

    // StudentCardViewModel의 IsChanged 변경 감지
    private void OnCardVMPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StudentCardViewModel.IsChanged))
        {
            bool hasStudent = !string.IsNullOrEmpty(_currentStudentId);
            BtnSave.IsEnabled = hasStudent && (CardVM?.IsChanged == true);
        }
    }

    // ────────────────────────────────────────────────────
    //  누가기록 로드
    // ────────────────────────────────────────────────────

    private async Task LoadStudentLogsAsync(string studentId)
    {
        try
        {
            using var svc = new StudentLogService();
            var logs = await svc.GetStudentLogsAsync(studentId, _currentYear);

            var vms = new List<StudentLogViewModel>();
            foreach (var l in logs)
                vms.Add(await StudentLogViewModel.CreateAsync(l));

            LogList.LoadLogs(vms);
            LogList.StudentInfoMode = StudentInfoMode.HideAll;
            LogList.Category        = LogCategory.전체;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentInfoPage] LoadLogs: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  버튼 핸들러
    // ────────────────────────────────────────────────────

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
        => await SaveStudentInfoAsync();

    private async void BtnReset_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentStudentId) || CardVM is null) return;

        var dlg = new ConfirmDialog("학생 정보 초기화",
            $"{CardVM.Name} 학생의 정보를 모두 초기화합니다.\n되돌릴 수 없습니다.");
        bool ok = await dlg.ShowDialogAsync(TopLevel.GetTopLevel(this) as Window ?? new Window());
        if (!ok) return;

        bool success = await CardVM.ResetAllInfoAsync();
        if (success)
        {
            await CardVM.SaveAsync();
            await LoadStudentListAsync();
        }
    }

    private async void BtnPrint_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentStudentId) || CardVM is null) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        // 1. 인쇄 옵션 다이얼로그
        var optsDlg = new StudentPrintOptionsDialog();
        await optsDlg.ShowDialog(owner);
        if (!optsDlg.IsSuccess) return;

        // 2. 로그 목록 수집
        List<StudentLogViewModel>? logsToInclude = null;
        if (optsDlg.IncludeStudentLogs)
        {
            logsToInclude = optsDlg.AllLogs
                ? LogList.Logs.Take(optsDlg.MaxLogCountValue).ToList()
                : LogList.SelectedLogs.ToList();

            if (!optsDlg.AllLogs && logsToInclude.Count == 0)
            {
                var warn = new ConfirmDialog("선택 필요", "선택된 누가기록이 없습니다.");
                await warn.ShowDialogAsync(owner);
                return;
            }
        }

        // 3. PDF 생성
        try
        {
            var service = new StudentCardPrintService();
            var pdfPath = await service.GenerateStudentCardPdfAsync(
                CardVM,
                App.FilePicker,
                optsDlg.IncludeDetailInfo,
                logsToInclude);

            if (pdfPath is null) return; // 취소

            Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StudentInfoPage] BtnPrint: {ex.Message}");
        }
    }

    private async void BtnNewLog_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentStudentId)) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new StudentLogEditDialog(
            _currentStudentId, CardVM?.Name ?? "", null);
        await dlg.ShowDialog(owner);
        if (dlg.Result != null)
            await LoadStudentLogsAsync(_currentStudentId);
    }

    private async void BtnSaveLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.SaveChangedLogsAsync(); }
        catch (Exception ex)
        { System.Diagnostics.Debug.WriteLine($"[StudentInfoPage] SaveLog: {ex.Message}"); }
    }

    private async void BtnDeleteLog_Click(object? sender, RoutedEventArgs e)
    {
        try { await LogList.DeleteSelectedLogsAsync(); }
        catch (Exception ex)
        { System.Diagnostics.Debug.WriteLine($"[StudentInfoPage] DeleteLog: {ex.Message}"); }
    }

    // ────────────────────────────────────────────────────
    //  저장 헬퍼
    // ────────────────────────────────────────────────────

    private async Task SaveStudentInfoAsync()
    {
        if (string.IsNullOrEmpty(_currentStudentId) || CardVM is null) return;
        string savedId = _currentStudentId;
        _isSaving = true;
        try
        {
            bool ok = await CardVM.SaveAsync();
            if (ok)
            {
                await RefreshStudentListAsync();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                    () => StudentList.SelectStudent(savedId),
                    Avalonia.Threading.DispatcherPriority.Background);
                // 저장 성공 후 저장 버튼 비활성화
                BtnSave.IsEnabled = false;
            }
        }
        catch (Exception ex)
        { System.Diagnostics.Debug.WriteLine($"[StudentInfoPage] Save: {ex.Message}"); }
        finally
        { _isSaving = false; }
    }

    /// <summary>학생 목록만 새로고침 (선택/카드 유지)</summary>
    private async Task RefreshStudentListAsync()
    {
        try
        {
            using var svc = new EnrollmentService();
            var roster = await svc.GetClassRosterAsync(
                Settings.SchoolCode.Value, _currentYear, _currentGrade, _currentClass);
            StudentList.LoadStudents(roster);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StudentInfoPage] RefreshList: {ex.Message}");
        }
    }

    private async Task SaveChangedAsync()
    {
        if (string.IsNullOrEmpty(_currentStudentId) || CardVM is null) return;
        if (!CardVM.IsChanged) return;
        try { await CardVM.SaveAsync(); }
        catch (Exception ex)
        { System.Diagnostics.Debug.WriteLine($"[StudentInfoPage] AutoSave: {ex.Message}"); }
    }

    private void EnableButtons(bool enabled)
    {
        BtnSave.IsEnabled      = enabled && (CardVM?.IsChanged == true);
        BtnReset.IsEnabled     = enabled;
        BtnPrint.IsEnabled     = enabled;
        BtnNewLog.IsEnabled    = enabled;
        BtnSaveLog.IsEnabled   = enabled;
        BtnDeleteLog.IsEnabled = enabled;
    }

    // ──────────────────────────────────────────────────
    //  엑셀 일괄 입력 (양식 다운로드 / 일괄입력)
    // ──────────────────────────────────────────────────

    private async void BtnDownloadTemplate_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentYear <= 0 || _currentGrade <= 0 || _currentClass <= 0)
        {
            var owner = TopLevel.GetTopLevel(this) as Window ?? new Window();
            await new ConfirmDialog("알림", "학년/반을 먼저 선택하세요.").ShowDialogAsync(owner);
            return;
        }
        try { await GenerateStudentInfoTemplateAsync(); }
        catch (Exception ex) { Debug.WriteLine($"[StudentInfoPage] DownloadTemplate: {ex.Message}"); }
    }

    private async void BtnBulkImport_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentYear <= 0 || _currentGrade <= 0 || _currentClass <= 0)
        {
            var owner = TopLevel.GetTopLevel(this) as Window ?? new Window();
            await new ConfirmDialog("알림", "학년/반을 먼저 선택하세요.").ShowDialogAsync(owner);
            return;
        }

        var win = TopLevel.GetTopLevel(this) as Window;
        if (win is null) return;

        try
        {
            var picker = App.FilePicker;
            var filePath = await ExcelHelpers.PickExcelFileAsync(picker);
            if (filePath is null) return;

            var importData = await ParseStudentInfoExcelAsync(filePath);
            if (importData is null || importData.Count == 0)
            {
                await new ConfirmDialog("알림", "엑셀 파일에서 학생 데이터를 찾을 수 없습니다.").ShowDialogAsync(win);
                return;
            }

            var previewDlg = new BulkStudentInfoPreviewDialog();
            previewDlg.SetPreviewData(importData);
            await previewDlg.ShowDialog(win);
            if (!previewDlg.IsSuccess) return;

            await SaveBulkStudentInfoAsync(importData);
            await LoadStudentListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StudentInfoPage] BulkImport: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────
    //  엑셀 템플릿 생성
    // ──────────────────────────────────────────────────

    private async Task GenerateStudentInfoTemplateAsync()
    {
        using var enrollmentService = new EnrollmentService();
        var roster = await enrollmentService.GetClassRosterAsync(
            Settings.SchoolCode.Value, _currentYear, _currentGrade, _currentClass);

        if (roster.Count == 0)
        {
            var owner = TopLevel.GetTopLevel(this) as Window ?? new Window();
            await new ConfirmDialog("알림", "학급에 등록된 학생이 없습니다.").ShowDialogAsync(owner);
            return;
        }

        var studentIds = roster.Select(r => r.StudentID).ToList();
        using var studentService = new StudentService(SchoolDatabase.DbPath);
        using var detailService  = new StudentDetailService(SchoolDatabase.DbPath);

        var students  = (await studentService.GetStudentsByIdsAsync(studentIds)).ToDictionary(s => s.StudentID);
        var details   = (await detailService.GetByStudentIdsAsync(studentIds)).ToDictionary(d => d.StudentID);

        var rows = new List<Dictionary<string, object>>();
        foreach (var e in roster.OrderBy(r => r.Number))
        {
            students.TryGetValue(e.StudentID, out var st);
            details.TryGetValue(e.StudentID, out var d);
            rows.Add(new Dictionary<string, object>
            {
                ["번호"]       = e.Number,
                ["이름"]       = e.Name ?? "",
                ["성별"]       = st?.Sex ?? "",
                ["생년월일"]    = st?.BirthDate?.ToString("yyyy-MM-dd") ?? "",
                ["전화번호"]    = st?.Phone ?? "",
                ["이메일"]      = st?.Email ?? "",
                ["주소"]       = st?.Address ?? "",
                ["메모"]       = st?.Memo ?? "",
                ["보호자이름"]  = d?.GuardianName ?? "",
                ["보호자관계"]  = d?.GuardianRelation ?? "",
                ["보호자전화"]  = d?.GuardianPhone ?? "",
                ["아버지이름"]  = d?.FatherName ?? "",
                ["아버지전화"]  = d?.FatherPhone ?? "",
                ["아버지직업"]  = d?.FatherJob ?? "",
                ["어머니이름"]  = d?.MotherName ?? "",
                ["어머니전화"]  = d?.MotherPhone ?? "",
                ["어머니직업"]  = d?.MotherJob ?? "",
                ["진로희망"]    = d?.CareerGoal ?? "",
                ["특기재능"]    = d?.Talents ?? "",
                ["흥미관심"]    = d?.Interests ?? "",
                ["건강상태"]    = d?.HealthInfo ?? "",
                ["알레르기"]    = d?.Allergies ?? "",
                ["특이사항"]    = d?.SpecialNeeds ?? "",
            });
        }

        var picker = App.FilePicker;
        string defaultFileName = $"학생정보_{_currentGrade}학년{_currentClass}반_{DateTime.Now:yyyyMMdd}.xlsx";
        var savePath = await ExcelHelpers.SaveExcelFileAsync(picker, defaultFileName);
        if (savePath is null) return;

        string tempPath = Path.Combine(Path.GetTempPath(), $"si_template_{Guid.NewGuid():N}.xlsx");
        try
        {
            await Task.Run(() => MiniExcelLibs.MiniExcel.SaveAs(tempPath, rows));
            File.Copy(tempPath, savePath, overwrite: true);
            Process.Start(new ProcessStartInfo(savePath) { UseShellExecute = true });
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ──────────────────────────────────────────────────
    //  엑셀 파싱 + 학생 매칭
    // ──────────────────────────────────────────────────

    private static readonly Dictionary<string, string> s_studentFieldMap = new()
    {
        ["성별"] = "Sex",
        ["생년월일"] = "BirthDate", ["생일"] = "BirthDate",
        ["전화번호"] = "Phone",    ["전화"] = "Phone",    ["연락처"] = "Phone",
        ["이메일"]  = "Email",    ["메일"] = "Email",
        ["주소"]   = "Address",
        ["메모"]   = "Memo",
    };

    private static readonly Dictionary<string, string> s_detailFieldMap = new()
    {
        ["보호자이름"] = "GuardianName",  ["보호자"] = "GuardianName",
        ["보호자관계"] = "GuardianRelation", ["관계"] = "GuardianRelation",
        ["보호자전화"] = "GuardianPhone", ["보호자연락처"] = "GuardianPhone",
        ["아버지이름"] = "FatherName",    ["아버지"] = "FatherName",    ["부"] = "FatherName",
        ["아버지전화"] = "FatherPhone",   ["부전화"] = "FatherPhone",
        ["아버지직업"] = "FatherJob",     ["부직업"] = "FatherJob",
        ["어머니이름"] = "MotherName",    ["어머니"] = "MotherName",    ["모"] = "MotherName",
        ["어머니전화"] = "MotherPhone",   ["모전화"] = "MotherPhone",
        ["어머니직업"] = "MotherJob",     ["모직업"] = "MotherJob",
        ["진로희망"] = "CareerGoal",  ["진로"] = "CareerGoal",
        ["특기재능"] = "Talents",      ["특기"] = "Talents", ["재능"] = "Talents",
        ["흥미관심"] = "Interests",    ["흥미"] = "Interests", ["관심"] = "Interests", ["관심분야"] = "Interests",
        ["건강상태"] = "HealthInfo",   ["건강"] = "HealthInfo",
        ["알레르기"] = "Allergies",
        ["특이사항"] = "SpecialNeeds",
    };

    private async Task<List<StudentImportPreviewItem>?> ParseStudentInfoExcelAsync(string filePath)
    {
        var sheets = await ExcelHelper.DataToTextAsync(filePath);
        if (sheets is null || sheets.Count == 0) return null;

        var data = sheets[0];
        int rows = data.GetLength(0);
        int cols = data.GetLength(1);
        if (rows < 2 || cols < 2) return null;

        // 헤더 행 탐색 (번호+이름 컬럼)
        int headerRow = -1, numberCol = -1, nameCol = -1;
        for (int r = 1; r < Math.Min(rows, 11); r++)
        {
            for (int c = 1; c < cols; c++)
            {
                string v = (data[r, c] ?? "").Trim();
                if (v == "번호") numberCol = c;
                if (v == "이름" || v == "성명") nameCol = c;
            }
            if (numberCol > 0 && nameCol > 0) { headerRow = r; break; }
            numberCol = -1; nameCol = -1;
        }

        if (headerRow < 0) return null;

        // 컬럼 매핑
        var studentColMap = new Dictionary<int, string>();
        var detailColMap  = new Dictionary<int, string>();
        var colNameMap    = new Dictionary<int, string>();

        for (int c = 1; c < cols; c++)
        {
            string h = (data[headerRow, c] ?? "").Trim();
            if (string.IsNullOrEmpty(h)) continue;
            colNameMap[c] = h;
            if (s_studentFieldMap.TryGetValue(h, out var sf)) studentColMap[c] = sf;
            else if (s_detailFieldMap.TryGetValue(h, out var df)) detailColMap[c] = df;
        }

        // 학급 명부 + 기존 데이터
        using var enrollmentService = new EnrollmentService();
        var roster = await enrollmentService.GetClassRosterAsync(
            Settings.SchoolCode.Value, _currentYear, _currentGrade, _currentClass);
        var numberToEnrollment = roster.ToDictionary(r => r.Number);

        var ids = roster.Select(r => r.StudentID).ToList();
        using var studentService = new StudentService(SchoolDatabase.DbPath);
        using var detailService  = new StudentDetailService(SchoolDatabase.DbPath);
        var students = (await studentService.GetStudentsByIdsAsync(ids)).ToDictionary(s => s.StudentID);
        var details  = (await detailService.GetByStudentIdsAsync(ids)).ToDictionary(d => d.StudentID);

        var result = new List<StudentImportPreviewItem>();
        for (int r = headerRow + 1; r < rows; r++)
        {
            string numStr = (data[r, numberCol] ?? "").Trim();
            string name   = (data[r, nameCol]   ?? "").Trim();
            if (string.IsNullOrEmpty(numStr) && string.IsNullOrEmpty(name)) continue;
            if (!int.TryParse(numStr, out int number)) continue;

            var item = new StudentImportPreviewItem { Number = number, Name = name };

            if (numberToEnrollment.TryGetValue(number, out var enrollment))
            {
                item.MatchedStudentID = enrollment.StudentID;
                students.TryGetValue(enrollment.StudentID, out var st);
                details.TryGetValue(enrollment.StudentID, out var det);

                foreach (var (col, field) in studentColMap)
                {
                    string newVal = (data[r, col] ?? "").Trim();
                    if (string.IsNullOrEmpty(newVal)) continue;
                    string? oldVal = GetStudentField(st, field);
                    if (newVal != (oldVal ?? "")) { item.StudentFields[field] = newVal; item.Changes.Add(colNameMap[col]); }
                }
                foreach (var (col, field) in detailColMap)
                {
                    string newVal = (data[r, col] ?? "").Trim();
                    if (string.IsNullOrEmpty(newVal)) continue;
                    string? oldVal = GetDetailField(det, field);
                    if (newVal != (oldVal ?? "")) { item.DetailFields[field] = newVal; item.Changes.Add(colNameMap[col]); }
                }
            }
            result.Add(item);
        }
        return result;
    }

    // ──────────────────────────────────────────────────
    //  일괄 저장
    // ──────────────────────────────────────────────────

    private async Task SaveBulkStudentInfoAsync(List<StudentImportPreviewItem> importData)
    {
        var toSave = importData.Where(i => i.IsMatched && i.Changes.Count > 0).ToList();
        if (toSave.Count == 0) return;

        int success = 0, fail = 0;
        foreach (var item in toSave)
        {
            try
            {
                using var studentService = new StudentService(SchoolDatabase.DbPath);
                using var detailService  = new StudentDetailService(SchoolDatabase.DbPath);

                if (item.StudentFields.Count > 0)
                {
                    var st = await studentService.GetBasicInfoAsync(item.MatchedStudentID!);
                    if (st is not null)
                    {
                        foreach (var (field, val) in item.StudentFields) SetStudentField(st, field, val!);
                        await studentService.UpdateBasicInfoAsync(st);
                    }
                }
                if (item.DetailFields.Count > 0)
                {
                    var det = await detailService.GetByStudentIdAsync(item.MatchedStudentID!)
                              ?? new StudentDetail { StudentID = item.MatchedStudentID! };
                    foreach (var (field, val) in item.DetailFields) SetDetailField(det, field, val!);
                    await detailService.CreateOrUpdateAsync(det);
                }
                success++;
            }
            catch (Exception ex)
            {
                fail++;
                Debug.WriteLine($"[StudentInfoPage] BulkSave {item.Number}번: {ex.Message}");
            }
        }
        Debug.WriteLine($"[StudentInfoPage] BulkSave 완료: {success}성공 {fail}실패");
    }

    // ──────────────────────────────────────────────────
    //  필드 헬퍼
    // ──────────────────────────────────────────────────

    private static string? GetStudentField(Student? s, string f) => s is null ? null : f switch
    {
        "Sex"       => s.Sex,
        "BirthDate" => s.BirthDate?.ToString("yyyy-MM-dd"),
        "Phone"     => s.Phone,
        "Email"     => s.Email,
        "Address"   => s.Address,
        "Memo"      => s.Memo,
        _           => null,
    };

    private static string? GetDetailField(StudentDetail? d, string f) => d is null ? null : f switch
    {
        "GuardianName"     => d.GuardianName,
        "GuardianRelation" => d.GuardianRelation,
        "GuardianPhone"    => d.GuardianPhone,
        "FatherName"       => d.FatherName,
        "FatherPhone"      => d.FatherPhone,
        "FatherJob"        => d.FatherJob,
        "MotherName"       => d.MotherName,
        "MotherPhone"      => d.MotherPhone,
        "MotherJob"        => d.MotherJob,
        "CareerGoal"       => d.CareerGoal,
        "Talents"          => d.Talents,
        "Interests"        => d.Interests,
        "HealthInfo"       => d.HealthInfo,
        "Allergies"        => d.Allergies,
        "SpecialNeeds"     => d.SpecialNeeds,
        _                  => null,
    };

    private static void SetStudentField(Student s, string f, string v)
    {
        switch (f)
        {
            case "Sex":       s.Sex = v; break;
            case "BirthDate":
                if (DateTime.TryParseExact(v,
                    new[] { "yyyy-MM-dd", "yyyy.MM.dd", "yyyy/MM/dd" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    s.BirthDate = dt;
                break;
            case "Phone":   s.Phone   = v; break;
            case "Email":   s.Email   = v; break;
            case "Address": s.Address = v; break;
            case "Memo":    s.Memo    = v; break;
        }
    }

    private static void SetDetailField(StudentDetail d, string f, string v)
    {
        switch (f)
        {
            case "GuardianName":     d.GuardianName     = v; break;
            case "GuardianRelation": d.GuardianRelation = v; break;
            case "GuardianPhone":    d.GuardianPhone    = v; break;
            case "FatherName":       d.FatherName       = v; break;
            case "FatherPhone":      d.FatherPhone      = v; break;
            case "FatherJob":        d.FatherJob        = v; break;
            case "MotherName":       d.MotherName       = v; break;
            case "MotherPhone":      d.MotherPhone      = v; break;
            case "MotherJob":        d.MotherJob        = v; break;
            case "CareerGoal":       d.CareerGoal       = v; break;
            case "Talents":          d.Talents          = v; break;
            case "Interests":        d.Interests        = v; break;
            case "HealthInfo":       d.HealthInfo       = v; break;
            case "Allergies":        d.Allergies        = v; break;
            case "SpecialNeeds":     d.SpecialNeeds     = v; break;
        }
    }
}
