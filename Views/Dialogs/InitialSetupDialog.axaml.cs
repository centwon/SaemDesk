using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 프로그램 초기 설정 창.
/// 원본: NewSchool.Dialogs.InitialSetupDialog (WinUI3 ContentDialog).
/// 학교 검색 → 사용자 정보 → 학년도/학기 3단계 순서.
/// </summary>
public partial class InitialSetupDialog : Window
{
    private School? _selectedSchool;
    private bool _isSchoolSelected;
    private bool _isUserNameEntered;
    private bool _isYearSemesterSet;

    public bool IsSuccess { get; private set; }

    public InitialSetupDialog()
    {
        InitializeComponent();

        // 기본값 — 현재 연도, 현재 월 기준 학기
        WorkYearBox.Value  = DateTime.Now.Year;
        int month          = DateTime.Now.Month;
        WorkSemesterCombo.SelectedIndex = (month >= 3 && month <= 8) ? 0 : 1;

        UpdateStatus();
    }

    // ────────────────────────────────────────────────────
    //  1단계: 학교 검색
    // ────────────────────────────────────────────────────

    private async void OnSearchSchoolClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SchoolSearchDialog();
            await dlg.ShowDialog(this);

            if (dlg.Result != null)
            {
                _selectedSchool = dlg.Result;
                SchoolNameBox.Text    = _selectedSchool.SchoolName;
                SchoolCodeBox.Text    = _selectedSchool.SchoolCode;
                SchoolAddressBox.Text = _selectedSchool.Address ?? "";

                SchoolInfoBar.IsVisible = true;
                _isSchoolSelected = true;
                UpdateStatus();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InitialSetupDialog] 학교 검색: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  2단계: 사용자 정보
    // ────────────────────────────────────────────────────

    private void OnUserNameChanged(object? sender, TextChangedEventArgs e)
    {
        _isUserNameEntered = !string.IsNullOrWhiteSpace(UserNameBox.Text);
        UpdateStatus();
    }

    // ────────────────────────────────────────────────────
    //  3단계: 학년도/학기
    // ────────────────────────────────────────────────────

    private void OnWorkYearChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        CheckYearSemesterSet();
    }

    private void OnWorkSemesterChanged(object? sender, SelectionChangedEventArgs e)
    {
        CheckYearSemesterSet();
    }

    private void CheckYearSemesterSet()
    {
        _isYearSemesterSet = (WorkYearBox.Value ?? 0) > 0
                             && WorkSemesterCombo.SelectedIndex >= 0;
        UpdateStatus();
    }

    // ────────────────────────────────────────────────────
    //  완료 / 취소
    // ────────────────────────────────────────────────────

    private async void OnComplete(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;

        if (!Validate()) return;

        CompleteButton.IsEnabled = false;
        try
        {
            await SaveSettingsAsync();
            IsSuccess = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text      = $"저장 오류: {ex.Message}";
            ErrorText.IsVisible = true;
            Debug.WriteLine($"[InitialSetupDialog] 저장 실패: {ex.Message}");
            CompleteButton.IsEnabled = true;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        IsSuccess = false;
        Close();
    }

    // ────────────────────────────────────────────────────
    //  유효성 검사
    // ────────────────────────────────────────────────────

    private bool Validate()
    {
        if (_selectedSchool == null)
        {
            ErrorText.Text      = "학교를 선택해 주세요.";
            ErrorText.IsVisible = true;
            return false;
        }
        if (string.IsNullOrWhiteSpace(UserNameBox.Text))
        {
            ErrorText.Text      = "이름을 입력해 주세요.";
            ErrorText.IsVisible = true;
            return false;
        }
        if ((WorkYearBox.Value ?? 0) <= 0)
        {
            ErrorText.Text      = "학년도를 입력해 주세요.";
            ErrorText.IsVisible = true;
            return false;
        }
        if (WorkSemesterCombo.SelectedIndex < 0)
        {
            ErrorText.Text      = "학기를 선택해 주세요.";
            ErrorText.IsVisible = true;
            return false;
        }
        return true;
    }

    // ────────────────────────────────────────────────────
    //  저장
    // ────────────────────────────────────────────────────

    private async Task SaveSettingsAsync()
    {
        if (_selectedSchool is null) return;

        // 1. 학교 정보 DB 저장
        using var schoolSvc = new SchoolService(SchoolDatabase.DbPath);
        await schoolSvc.SaveSchoolAsync(_selectedSchool);

        // 2. 교사 정보 생성 및 저장
        var now       = DateTime.Now;
        var random    = new Random().Next(1000, 9999);
        string teacherId = $"T{now:yyyyMMddHHmmss}{random}";

        string subjectStr = (UserSubjectCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        var teacher = new Teacher
        {
            TeacherID  = teacherId,
            LoginID    = teacherId,
            Name       = UserNameBox.Text?.Trim() ?? "",
            Status     = "재직",
            Phone      = UserPhoneBox.Text?.Trim() ?? "",
            Email      = UserEmailBox.Text?.Trim() ?? "",
            Subject    = subjectStr,
            HireDate   = now.ToString("yyyy-MM-dd"),
            CreatedAt  = now,
            UpdatedAt  = now,
        };

        using var teacherSvc = new TeacherService(SchoolDatabase.DbPath);
        var (ok, msg) = await teacherSvc.CreateAsync(teacher);
        if (!ok) throw new Exception($"교사 정보 저장 실패: {msg}");

        // 3. 근무 이력
        var history = new TeacherSchoolHistory
        {
            TeacherID  = teacherId,
            SchoolCode = _selectedSchool.SchoolCode,
            StartDate  = now.ToString("yyyy-MM-dd"),
            IsCurrent  = true,
            Position   = subjectStr,
            CreatedAt  = now,
            UpdatedAt  = now,
        };
        var (ok2, msg2) = await teacherSvc.AddHistoryAsync(history);
        if (!ok2) throw new Exception($"근무 이력 저장 실패: {msg2}");

        // 4. Settings 저장
        Settings.SchoolCode.Set(_selectedSchool.SchoolCode);
        Settings.SchoolName.Set(_selectedSchool.SchoolName);
        Settings.ProvinceCode.Set(_selectedSchool.ATPT_OFCDC_SC_CODE ?? "");
        Settings.ProvinceName.Set(_selectedSchool.ATPT_OFCDC_SC_NAME ?? "");
        Settings.SchoolAddress.Set(_selectedSchool.Address ?? "");

        Settings.User.Set(teacherId);
        Settings.UserName.Set(UserNameBox.Text?.Trim() ?? "");

        Settings.WorkYear.Set((int)(WorkYearBox.Value ?? DateTime.Now.Year));
        Settings.WorkSemester.Set(WorkSemesterCombo.SelectedIndex + 1);

        if ((HomeGradeBox.Value ?? 0) > 0)
            Settings.HomeGrade.Set((int)HomeGradeBox.Value!.Value);

        if ((HomeRoomBox.Value ?? 0) > 0)
            Settings.HomeRoom.Set((int)HomeRoomBox.Value!.Value);

        Debug.WriteLine($"[InitialSetupDialog] 초기 설정 완료 — teacherId={teacherId}");
    }

    // ────────────────────────────────────────────────────
    //  UI 상태
    // ────────────────────────────────────────────────────

    private void UpdateStatus()
    {
        Step1Status.Text = _isSchoolSelected   ? "☑ 학교 선택"  : "□ 학교 선택";
        Step2Status.Text = _isUserNameEntered  ? "☑ 사용자 정보" : "□ 사용자 정보";
        Step3Status.Text = _isYearSemesterSet  ? "☑ 학년도/학기" : "□ 학년도/학기";

        CompleteButton.IsEnabled = _isSchoolSelected && _isUserNameEntered && _isYearSemesterSet;
    }
}
