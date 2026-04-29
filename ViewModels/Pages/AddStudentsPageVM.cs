using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniExcelLibs;
using SaemDesk.Helpers;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학급 명렬 일괄 입력 페이지 VM — NewSchool AddStudentsPage MVP 포팅.
///
/// 핵심 흐름:
/// - 학년도/학년/반/번호/이름 입력 → 추가 → 임시 목록 누적
/// - 저장: 각 항목을 Student.CreateAsync + EnrollmentService.AssignToClassAsync
/// - 학생 ID 는 Student.GenerateStudentID(SchoolCode, Year, sequence) 로 자동 생성
/// </summary>
public partial class AddStudentsPageVM : ViewModelBase
{
    public ObservableCollection<PendingStudent> Pending { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private int _pendingCount;

    [ObservableProperty] private decimal _year   = DateTime.Today.Year;
    [ObservableProperty] private decimal _grade  = 1;
    [ObservableProperty] private decimal _classNo = 1;
    [ObservableProperty] private decimal _number = 1;
    [ObservableProperty] private string  _name   = string.Empty;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;
    [ObservableProperty] private bool   _isSaving;

    public bool CanSave => Pending.Count > 0 && !IsSaving;
    public bool IsEmpty => Pending.Count == 0;

    public AddStudentsPageVM()
    {
        // 설정값으로 초기화
        Year   = Settings.WorkYear.Value > 0 ? Settings.WorkYear.Value : DateTime.Today.Year;
        Grade  = Settings.HomeGrade.Value > 0 ? Settings.HomeGrade.Value : 1;
        ClassNo = Settings.HomeRoom.Value > 0 ? Settings.HomeRoom.Value : 1;

        Pending.CollectionChanged += (_, _) =>
        {
            PendingCount = Pending.Count;
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsEmpty));
        };
    }

    [RelayCommand]
    private void AddOne()
    {
        ErrorText = string.Empty;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorText = "이름을 입력해 주세요.";
            return;
        }
        int year  = (int)Year;
        int grade = (int)Grade;
        int cls   = (int)ClassNo;
        int num   = (int)Number;
        if (year < 2000 || grade < 1 || cls < 1 || num < 1)
        {
            ErrorText = "학년도/학년/반/번호 값을 확인해 주세요.";
            return;
        }

        // 임시 학생 ID(저장 시 SchoolCode 와 함께 정식 ID 부여)
        string tempId = $"{year}-{grade}-{cls}-{num}";

        // 같은 (학년/반/번호) 중복 방지
        if (Pending.Any(p => p.Grade == grade && p.Class == cls && p.Number == num))
        {
            ErrorText = $"{grade}학년 {cls}반 {num}번이 이미 목록에 있습니다.";
            return;
        }

        Pending.Add(new PendingStudent
        {
            TempId  = tempId,
            Year    = year,
            Grade   = grade,
            Class   = cls,
            Number  = num,
            Name    = Name.Trim(),
        });

        // 다음 입력 편의: 번호 자동 증가, 이름 비우기
        Number = num + 1;
        Name   = string.Empty;
        StatusText = $"추가됨: {grade}-{cls}-{num} {Pending[^1].Name}";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        var toRemove = Pending.Where(p => p.IsSelected).ToList();
        foreach (var p in toRemove) Pending.Remove(p);
        StatusText = toRemove.Count > 0 ? $"{toRemove.Count}건 삭제" : "선택된 항목이 없습니다.";
    }

    [RelayCommand]
    private void RemoveOne(PendingStudent? item)
    {
        if (item is null) return;
        Pending.Remove(item);
    }

    [RelayCommand]
    private void Clear()
    {
        Pending.Clear();
        StatusText = "목록을 비웠습니다.";
    }

    /// <summary>학생 명렬 입력용 Excel 템플릿 다운로드.</summary>
    [RelayCommand]
    private async Task DownloadTemplateAsync()
    {
        ErrorText = string.Empty;
        try
        {
            string defaultName = "학생명렬_템플릿.xlsx";
            string? path = await SaemDesk.App.FilePicker.SaveFileAsync(defaultName, "xlsx");
            if (string.IsNullOrEmpty(path)) return;

            // 첫 행은 헤더, 두 번째 행부터 예시 1줄 (사용자가 지우거나 덮어쓰기)
            var rows = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["학년도"] = (int)Year,
                    ["학년"]   = (int)Grade,
                    ["반"]     = (int)ClassNo,
                    ["번호"]   = 1,
                    ["이름"]   = "홍길동",
                },
            };
            await MiniExcel.SaveAsAsync(path, rows, overwriteFile: true);
            StatusText = $"템플릿 저장: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            ErrorText = $"템플릿 저장 실패: {ex.Message}";
            Debug.WriteLine($"[AddStudentsVM.Template] {ex}");
        }
    }

    /// <summary>Excel(.xlsx) 에서 학생 명렬을 일괄 가져오기 — NewSchool 견고 파서 이식.</summary>
    [RelayCommand]
    private async Task ImportFromExcelAsync()
    {
        ErrorText = string.Empty;
        StatusText = string.Empty;
        try
        {
            string? path = await SaemDesk.App.FilePicker.OpenFileAsync(".xlsx");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            // 구 형식(.xls BIFF) 차단 — MiniExcel 은 OpenXML(.xlsx) 만 처리
            if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorText = "구 형식(.xls)은 지원되지 않습니다. Excel 에서 .xlsx 로 다시 저장해 주세요.";
                return;
            }

            int yearForImport = (int)Year;
            int totalImported = 0, totalSkipped = 0;

            // ExcelHelper.DataToText: List<string[,]> — 시트별 1-based 인덱스 배열
            var sheets = ExcelHelper.DataToText(path);
            foreach (var sheetData in sheets)
            {
                var (imp, skip) = ProcessWorksheetData(sheetData, yearForImport);
                totalImported += imp;
                totalSkipped  += skip;
            }

            if (totalImported == 0 && totalSkipped == 0)
            {
                ErrorText = "필수 열('번호', '이름' 또는 '성명')을 찾을 수 없습니다.";
                return;
            }

            StatusText = totalSkipped == 0
                ? $"Excel 가져오기 완료: {totalImported}명"
                : $"Excel 가져오기 완료: {totalImported}명 / 건너뜀 {totalSkipped}건";
        }
        catch (Exception ex)
        {
            ErrorText = $"가져오기 실패: {ex.Message}";
            Debug.WriteLine($"[AddStudentsVM.Import] {ex}");
        }
    }

    /// <summary>워크시트 1장 처리 — 헤더 자동 탐색 + "1학년/1반/1번" 접미사 파싱.</summary>
    private (int imported, int skipped) ProcessWorksheetData(string[,] sheetData, int year)
    {
        int rowCount = sheetData.GetLength(0);
        int colCount = sheetData.GetLength(1);
        if (rowCount < 2 || colCount < 2) return (0, 0);

        // 1-based 인덱스 (NewSchool과 동일)
        int gradeCol = -1, classCol = -1, numberCol = -1, nameCol = -1, titleRow = -1;

        // 처음 10행 이내에서 헤더 자동 탐색
        for (int row = 1; row <= Math.Min(10, rowCount - 1); row++)
        {
            for (int col = 1; col <= colCount - 1; col++)
            {
                var cell = (sheetData[row, col] ?? string.Empty).Replace(" ", string.Empty);

                if (cell.Equals("학년", StringComparison.OrdinalIgnoreCase))
                    gradeCol = col;
                else if (cell.Equals("반", StringComparison.OrdinalIgnoreCase) ||
                         cell.Equals("학급", StringComparison.OrdinalIgnoreCase))
                    classCol = col;
                else if (cell.Equals("번호", StringComparison.OrdinalIgnoreCase))
                    numberCol = col;
                else if (cell.Equals("이름", StringComparison.OrdinalIgnoreCase) ||
                         cell.Equals("성명", StringComparison.OrdinalIgnoreCase))
                {
                    nameCol = col;
                    titleRow = row;
                }
            }
            if (titleRow > 0) break;
        }

        if (titleRow == -1 || numberCol == -1 || nameCol == -1)
            return (0, 0);

        // 학년/반 컬럼이 누락된 경우 — 화면의 현재 학년/반 입력값을 기본값으로 사용
        int defaultGrade = gradeCol == -1 ? (int)Grade   : 0;
        int defaultClass = classCol == -1 ? (int)ClassNo : 0;

        int imported = 0, skipped = 0;

        for (int row = titleRow + 1; row < rowCount; row++)
        {
            if (!TryParseNumberFromText(sheetData[row, numberCol], out int number) || number < 1)
            { skipped++; continue; }

            string name = (sheetData[row, nameCol] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

            int grade = defaultGrade;
            if (gradeCol != -1 &&
                TryParseNumberFromText(sheetData[row, gradeCol], out int g) && g >= 1 && g <= 6)
                grade = g;
            if (grade <= 0) { skipped++; continue; }

            int cls = defaultClass;
            if (classCol != -1 &&
                TryParseNumberFromText(sheetData[row, classCol], out int c) && c >= 1)
                cls = c;
            if (cls <= 0) { skipped++; continue; }

            if (Pending.Any(p => p.Grade == grade && p.Class == cls && p.Number == number))
            { skipped++; continue; }

            Pending.Add(new PendingStudent
            {
                TempId = $"{year}-{grade}-{cls}-{number}",
                Year   = year,
                Grade  = grade,
                Class  = cls,
                Number = number,
                Name   = name,
            });
            imported++;
        }

        return (imported, skipped);
    }

    /// <summary>"1학년" → 1, "3반" → 3, "1" → 1 — NewSchool 파서 동등.</summary>
    private static bool TryParseNumberFromText(string? text, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        if (int.TryParse(text, out result)) return true;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return !string.IsNullOrEmpty(digits) && int.TryParse(digits, out result);
    }

    private static int? TryGetInt(IDictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v is null) return null;
        if (v is int i) return i;
        if (v is long l) return (int)l;
        if (v is double d) return (int)d;
        if (v is decimal m) return (int)m;
        if (int.TryParse(v.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static string? TryGetString(IDictionary<string, object> row, string key)
        => row.TryGetValue(key, out var v) && v is not null ? v.ToString() : null;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Pending.Count == 0) return;
        IsSaving = true;
        ErrorText = string.Empty;
        try
        {
            string sc = Settings.SchoolCode.Value;
            if (string.IsNullOrEmpty(sc))
            {
                ErrorText = "학교 코드가 설정되어 있지 않습니다. 설정에서 학교를 먼저 등록하세요.";
                return;
            }

            // School 테이블에 해당 SchoolCode 가 없으면 Enrollment FK가 깨짐 → 자동 등록
            using (var schoolRepo = new SaemDesk.Repositories.SchoolRepository(SchoolDatabase.DbPath))
            {
                var existing = await schoolRepo.GetBySchoolCodeAsync(sc);
                if (existing == null)
                {
                    string sname = Settings.SchoolName.Value;
                    if (string.IsNullOrWhiteSpace(sname))
                    {
                        ErrorText = $"학교({sc}) 정보가 등록되어 있지 않습니다. 설정에서 학교를 먼저 등록해 주세요.";
                        return;
                    }

                    await schoolRepo.CreateAsync(new SaemDesk.Models.School
                    {
                        SchoolCode = sc,
                        SchoolName = sname,
                        Address    = Settings.SchoolAddress.Value ?? string.Empty,
                        IsActive   = true,
                    });
                }
            }

            int success = 0;
            int failure = 0;
            var failures = new System.Collections.Generic.List<string>();
            using var studentRepo = new StudentRepository(SchoolDatabase.DbPath);
            using var enrollSvc   = new EnrollmentService();

            // 입학연도 = Year - (Grade - 1) (대략적; 사용자가 직접 입력하지 않음)
            int sequenceCounter = await studentRepo.GetCountAsync() + 1;

            foreach (var p in Pending.ToList())
            {
                try
                {
                    int admissionYear = p.Year - (p.Grade - 1);
                    string sid = Student.GenerateStudentID(sc, admissionYear, sequenceCounter++);

                    var stu = new Student
                    {
                        StudentID = sid,
                        Name      = p.Name,
                    };
                    await studentRepo.CreateAsync(stu);

                    var enroll = new Enrollment
                    {
                        StudentID  = sid,
                        Name       = p.Name,
                        SchoolCode = sc,
                        Year       = p.Year,
                        Semester   = 1,
                        Grade      = p.Grade,
                        Class      = p.Class,
                        Number     = p.Number,
                        Status     = "재학",
                    };
                    await enrollSvc.AssignToClassAsync(enroll);

                    Pending.Remove(p);
                    success++;
                }
                catch (Exception ex)
                {
                    failure++;
                    failures.Add($"{p.Grade}-{p.Class}-{p.Number} {p.Name}: {ex.Message}");
                    Debug.WriteLine($"[AddStudentsVM] 저장 실패: {ex}");
                }
            }

            StatusText = failure == 0
                ? $"저장 완료: {success}명"
                : $"저장 완료: {success}명 / 실패: {failure}명";
            if (failure > 0 && failures.Count > 0)
                ErrorText = string.Join("\n", failures.Take(3));
        }
        catch (Exception ex)
        {
            ErrorText = $"저장 중 오류: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
}

/// <summary>임시 목록 행.</summary>
public partial class PendingStudent : ObservableObject
{
    public string TempId { get; init; } = string.Empty;
    public int Year   { get; init; }
    public int Grade  { get; init; }
    public int Class  { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = string.Empty;

    public string ClassInfo => $"{Year}학년도 {Grade}학년 {Class}반 {Number}번";

    [ObservableProperty] private bool _isSelected;
}
