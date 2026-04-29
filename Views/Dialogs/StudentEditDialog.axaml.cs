using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;
using SaemDesk.ViewModels;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 추가 / 수정 다이얼로그.
/// existing == null → 추가 모드, existing != null → 수정 모드.
/// </summary>
public partial class StudentEditDialog : Window
{
    public bool Saved { get; private set; }

    private readonly StudentListItemViewModel? _existing;
    private bool _isSaving;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public StudentEditDialog() : this(null) { }

    public StudentEditDialog(StudentListItemViewModel? existing)
    {
        InitializeComponent();
        _existing = existing;

        bool isNew = existing is null;
        Title          = isNew ? "학생 추가" : "학생 정보 수정";
        TitleText.Text = isNew ? "학생 추가" : "학생 정보 수정";
        SubTitleText.Text =
            $"{Settings.HomeGrade}학년 {Settings.HomeRoom}반 · {Settings.WorkYear}학년도";

        if (!isNew && existing is not null)
        {
            NumberBox.Value        = existing.Number;
            NameBox.Text           = existing.Name;
            SexCombo.SelectedIndex = existing.Sex == "여" ? 1 : 0;
            StatusCombo.SelectedIndex = existing.Status switch
            {
                "전학" => 1,
                "졸업" => 2,
                "휴학" => 3,
                _     => 0,
            };
        }
    }

    // ────────────────────────────────────────────────────
    //  이벤트 핸들러
    // ────────────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;

        // 유효성 검사
        string name = NameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            ShowError("이름을 입력해 주세요.");
            return;
        }

        _isSaving = true;
        SaveButton.IsEnabled = false;
        HideError();

        try
        {
            int    number = (int)(NumberBox.Value ?? 1);
            string sex    = SexCombo.SelectedIndex == 1 ? "여" : "남";
            string status = StatusCombo.SelectedIndex switch
            {
                1 => "전학",
                2 => "졸업",
                3 => "휴학",
                _ => "재학",
            };

            if (_existing is null)
                await CreateStudentAsync(number, name, sex, status);
            else
                await UpdateStudentAsync(number, name, sex, status);

            Saved = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"저장 실패: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[StudentEditDialog] 오류: {ex}");
            SaveButton.IsEnabled = true;
            _isSaving = false;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    // ────────────────────────────────────────────────────
    //  저장 로직
    // ────────────────────────────────────────────────────

    private async Task CreateStudentAsync(int number, string name, string sex, string status)
    {
        string schoolCode = Settings.SchoolCode.Value;
        int    year       = Settings.WorkYear.Value;

        // StudentID: schoolCode(7자) + year(4자) + ticks(4자)
        string codeSegment = (schoolCode + "0000000")[..7];
        string seqSegment  = ((DateTime.UtcNow.Ticks / 1000) % 9000 + 1000).ToString();
        string studentId   = $"{codeSegment}{year:0000}{seqSegment}";

        var student = new Student
        {
            StudentID = studentId,
            Name      = name,
            Sex       = sex,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };

        var enrollment = new Enrollment
        {
            StudentID  = studentId,
            Name       = name,
            Sex        = sex,
            SchoolCode = schoolCode,
            Year       = year,
            Semester   = Settings.WorkSemester.Value,
            Grade      = Settings.HomeGrade.Value,
            Class      = Settings.HomeRoom.Value,
            Number     = number,
            Status     = status,
            TeacherID  = Settings.UserName.Value,
            CreatedAt  = DateTime.Now,
            UpdatedAt  = DateTime.Now,
        };

        using var svc = new StudentService(SchoolDatabase.DbPath);
        await svc.RegisterNewStudentAsync(student, enrollment);
    }

    private async Task UpdateStudentAsync(int number, string name, string sex, string status)
    {
        if (_existing is null) return;

        // Enrollment 업데이트
        using var eRepo = new EnrollmentRepository(SchoolDatabase.DbPath);
        var enrollment = await eRepo.GetByIdAsync(_existing.EnrollmentNo);
        if (enrollment is null)
            throw new InvalidOperationException("학적 정보를 찾을 수 없습니다.");

        enrollment.Number    = number;
        enrollment.Name      = name;
        enrollment.Sex       = sex;
        enrollment.Status    = status;
        enrollment.UpdatedAt = DateTime.Now;
        await eRepo.UpdateAsync(enrollment);

        // Student 테이블 이름·성별 동기화
        using var sRepo = new StudentRepository(SchoolDatabase.DbPath);
        var student = await sRepo.GetByIdAsync(_existing.StudentID);
        if (student is not null)
        {
            student.Name      = name;
            student.Sex       = sex;
            student.UpdatedAt = DateTime.Now;
            await sRepo.UpdateAsync(student);
        }
    }

    // ────────────────────────────────────────────────────
    //  UI 헬퍼
    // ────────────────────────────────────────────────────

    private void ShowError(string msg)
    {
        ErrorText.Text      = msg;
        ErrorText.IsVisible = true;
    }

    private void HideError() => ErrorText.IsVisible = false;
}
