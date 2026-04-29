using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 설정 페이지 ViewModel.
/// Settings.*  ←→  [ObservableProperty] 양방향 동기화.
/// _isLoading 플래그로 초기 로드 중 Set() 호출 방지.
///
/// NumericUpDown.Value 타입이 decimal? 이므로
/// 해당 항목은 decimal 타입으로 선언 (AOT 컴파일드 바인딩 호환).
/// </summary>
public partial class SettingsPageVM : ViewModelBase
{
    private bool _isLoading = true;

    // ── 앱 버전 ──────────────────────────────────────────
    public string AppVersion => AppInfo.Version;

    // ── 사용자 / 담임 정보 ────────────────────────────────
    [ObservableProperty] private string  _userName    = string.Empty;
    [ObservableProperty] private decimal _homeGrade;          // NumericUpDown → decimal
    [ObservableProperty] private decimal _homeRoom;           // NumericUpDown → decimal

    // ── 학교 정보 (학교 검색 후 채워짐, 직접 편집 불가) ──
    [ObservableProperty] private string _schoolName    = string.Empty;
    [ObservableProperty] private string _provinceName  = string.Empty;
    [ObservableProperty] private string _schoolAddress = string.Empty;
    [ObservableProperty] private string _provinceCode  = string.Empty;
    [ObservableProperty] private string _schoolCode    = string.Empty;

    // ── 학년도 / 학기 ─────────────────────────────────────
    [ObservableProperty] private decimal _workYear;           // NumericUpDown → decimal
    [ObservableProperty] private int     _workSemester = 1;  // ComboBox.SelectedIndex via WorkSemesterIndex

    /// <summary>ComboBox SelectedIndex (0=1학기, 1=2학기). -1(미선택)은 무시.</summary>
    public int WorkSemesterIndex
    {
        get => WorkSemester - 1;
        set
        {
            if (value < 0) return;            // ComboBox 초기화 시 -1 무시
            WorkSemester = value + 1;
        }
    }

    // ── 수업 시간 설정 ─────────────────────────────────────
    [ObservableProperty] private TimeSpan _dayStarting;       // CompactTimePicker.Time → TimeSpan
    [ObservableProperty] private TimeSpan _assemblyTime;      // CompactTimePicker.Time → TimeSpan
    [ObservableProperty] private decimal  _onePeriodMinutes;  // NumericUpDown → decimal
    [ObservableProperty] private decimal  _breakTimeMinutes;  // NumericUpDown → decimal
    [ObservableProperty] private decimal  _lunchTimeMinutes;  // NumericUpDown → decimal

    // ── 앱 옵션 ──────────────────────────────────────────
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _autoBackup;

    // ── 상태 메시지 ───────────────────────────────────────
    [ObservableProperty] private string _statusMessage  = string.Empty;
    [ObservableProperty] private bool   _isStatusVisible;
    private DispatcherTimer? _statusTimer;

    /// <summary>InfoBar 메시지 표시 + 일정 시간 후 자동 사라짐.</summary>
    private void ShowStatus(string message, int seconds = 3)
    {
        StatusMessage   = message;
        IsStatusVisible = true;

        _statusTimer?.Stop();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _statusTimer.Tick += (_, _) =>
        {
            IsStatusVisible = false;
            _statusTimer?.Stop();
        };
        _statusTimer.Start();
    }

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public SettingsPageVM()
    {
        LoadSettings();
        _isLoading = false;
    }

    // ────────────────────────────────────────────────────
    //  일정 / Google Calendar 설정 다이얼로그
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenCalendarSettingsAsync()
    {
        await DialogService.ShowCalendarSettingsAsync();
    }

    // ────────────────────────────────────────────────────
    //  설정 읽기
    // ────────────────────────────────────────────────────

    private void LoadSettings()
    {
        UserName      = Settings.UserName;
        HomeGrade     = (decimal)Settings.HomeGrade.Value;
        HomeRoom      = (decimal)Settings.HomeRoom.Value;

        SchoolName    = Settings.SchoolName;
        ProvinceName  = Settings.ProvinceName;
        SchoolAddress = Settings.SchoolAddress;
        ProvinceCode  = Settings.ProvinceCode;
        SchoolCode    = Settings.SchoolCode;

        WorkYear      = (decimal)Settings.WorkYear.Value;
        WorkSemester  = Settings.WorkSemester;

        DayStarting   = Settings.DayStarting;
        AssemblyTime  = Settings.AssemblyTime;

        OnePeriodMinutes = (decimal)Settings.OnePeriod.Value.TotalMinutes;
        BreakTimeMinutes = (decimal)Settings.BreakTime.Value.TotalMinutes;
        LunchTimeMinutes = (decimal)Settings.LunchTime.Value.TotalMinutes;

        AutoBackup       = Settings.AutoBackup;
        StartWithWindows = Settings.StartWithWindows;
    }

    // ────────────────────────────────────────────────────
    //  설정 저장 — 각 프로퍼티 변경 시 자동 호출
    // ────────────────────────────────────────────────────

    partial void OnUserNameChanged(string value)        { if (!_isLoading) Settings.UserName.Set(value); }
    partial void OnHomeGradeChanged(decimal value)      { if (!_isLoading) Settings.HomeGrade.Set((int)value); }
    partial void OnHomeRoomChanged(decimal value)       { if (!_isLoading) Settings.HomeRoom.Set((int)value); }

    partial void OnWorkYearChanged(decimal value)       { if (!_isLoading) Settings.WorkYear.Set((int)value); }
    partial void OnWorkSemesterChanged(int value)
    {
        if (!_isLoading) Settings.WorkSemester.Set(value);
        OnPropertyChanged(nameof(WorkSemesterIndex));
    }

    partial void OnDayStartingChanged(TimeSpan value)   { if (!_isLoading) Settings.DayStarting.Set(value); }
    partial void OnAssemblyTimeChanged(TimeSpan value)  { if (!_isLoading) Settings.AssemblyTime.Set(value); }
    partial void OnOnePeriodMinutesChanged(decimal value) { if (!_isLoading) Settings.OnePeriod.Set(TimeSpan.FromMinutes((double)value)); }
    partial void OnBreakTimeMinutesChanged(decimal value) { if (!_isLoading) Settings.BreakTime.Set(TimeSpan.FromMinutes((double)value)); }
    partial void OnLunchTimeMinutesChanged(decimal value) { if (!_isLoading) Settings.LunchTime.Set(TimeSpan.FromMinutes((double)value)); }

    partial void OnAutoBackupChanged(bool value)        { if (!_isLoading) Settings.AutoBackup.Set(value); }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isLoading) return;
        Settings.StartWithWindows.Set(value);
#pragma warning disable CA1416
        if (OperatingSystem.IsWindows()) Settings.SetStartWithWindows(value);
#pragma warning restore CA1416
    }

    // ────────────────────────────────────────────────────
    //  Commands
    // ────────────────────────────────────────────────────

    /// <summary>NEIS 학교 검색 다이얼로그 열기</summary>
    [RelayCommand]
    private async Task SearchSchoolAsync()
    {
        var school = await DialogService.ShowSchoolSearchAsync();
        if (school is null) return;

        _isLoading = true;
        SchoolName    = school.SchoolName;
        ProvinceName  = school.ATPT_OFCDC_SC_NAME;
        SchoolAddress = school.Address;
        ProvinceCode  = school.ATPT_OFCDC_SC_CODE;
        SchoolCode    = school.SchoolCode;
        _isLoading    = false;

        Settings.SchoolName.Set(school.SchoolName);
        Settings.ProvinceName.Set(school.ATPT_OFCDC_SC_NAME);
        Settings.SchoolAddress.Set(school.Address);
        Settings.ProvinceCode.Set(school.ATPT_OFCDC_SC_CODE);
        Settings.SchoolCode.Set(school.SchoolCode);

        // 로컬 DB에도 School 레코드 저장 (upsert)
        try
        {
            using var svc = new SchoolService(SchoolDatabase.DbPath);
            await svc.SaveSchoolAsync(school);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPageVM] 학교 DB 저장 오류: {ex.Message}");
        }

        ShowStatus($"학교가 설정되었습니다: {school.SchoolName}");
    }

    /// <summary>설정 값 다시 로드 (변경 취소)</summary>
    [RelayCommand]
    private void ReloadSettings()
    {
        _isLoading = true;
        LoadSettings();
        _isLoading = false;
        ShowStatus("설정을 다시 불러왔습니다.");
    }
}
