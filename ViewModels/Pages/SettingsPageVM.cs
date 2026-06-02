using System;
using System.Diagnostics;
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
    [ObservableProperty] private bool _topMost;

    /// <summary>테마 ("System" / "Light" / "Dark"). ComboBox 는 ThemeIndex 로 바인딩.</summary>
    [ObservableProperty] private string _theme = "Light";

    /// <summary>ComboBox SelectedIndex (0=시스템, 1=라이트, 2=다크). -1(미선택)은 무시.</summary>
    public int ThemeIndex
    {
        get => Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        set
        {
            if (value < 0) return;            // ComboBox 초기화 시 -1 무시
            Theme = value switch { 1 => "Light", 2 => "Dark", _ => "System" };
        }
    }

    // ── 데이터 관리 (백업) ────────────────────────────────
    [ObservableProperty] private bool    _autoBackup;
    [ObservableProperty] private decimal _autoBackupIntervalDays;   // NumericUpDown → decimal
    [ObservableProperty] private decimal _backupRetentionCount;     // NumericUpDown → decimal
    [ObservableProperty] private string  _lastBackupDisplay = "없음";

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

        StartWithWindows = Settings.StartWithWindows;
        TopMost          = Settings.TopMost;
        Theme            = string.IsNullOrEmpty(Settings.Theme.Value) ? "System" : Settings.Theme.Value;

        AutoBackup             = Settings.AutoBackup;
        AutoBackupIntervalDays = Settings.AutoBackupIntervalDays.Value;
        BackupRetentionCount   = Settings.BackupRetentionCount.Value;
        RefreshLastBackup();
    }

    /// <summary>마지막 백업 시각을 표시용 문자열로 갱신.</summary>
    private void RefreshLastBackup()
    {
        var raw = Settings.LastBackupTime.Value;
        LastBackupDisplay = DateTime.TryParse(raw, out var dt)
            ? dt.ToString("yyyy-MM-dd HH:mm")
            : "없음";
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

    partial void OnAutoBackupChanged(bool value)             { if (!_isLoading) Settings.AutoBackup.Set(value); }
    partial void OnAutoBackupIntervalDaysChanged(decimal value) { if (!_isLoading) Settings.AutoBackupIntervalDays.Set((int)value); }
    partial void OnBackupRetentionCountChanged(decimal value)   { if (!_isLoading) Settings.BackupRetentionCount.Set((int)value); }

    partial void OnThemeChanged(string value)
    {
        if (_isLoading) return;
        Settings.Theme.Set(value);
        App.ApplyTheme(value);
        OnPropertyChanged(nameof(ThemeIndex));
    }

    partial void OnTopMostChanged(bool value)
    {
        if (_isLoading) return;
        Settings.TopMost.Set(value);
        if (DialogService.MainWindow is { } w) w.Topmost = value;
    }

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

    // ────────────────────────────────────────────────────
    //  데이터 관리 (백업 / 복원 / 초기화 / 폴더 열기)
    // ────────────────────────────────────────────────────

    /// <summary>지금 전체 데이터 백업 (Settings.db + 모든 DB)</summary>
    [RelayCommand]
    private void BackupNow()
    {
        var dir = Settings.Backup();
        if (dir is null) { ShowStatus("백업에 실패했습니다."); return; }
        RefreshLastBackup();
        ShowStatus($"백업 완료: {dir}");
    }

    /// <summary>백업 폴더를 선택해 전체 데이터 복원 (덮어쓰기 → 재시작 필요)</summary>
    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var folder = await App.FilePicker.OpenFolderAsync();
        if (string.IsNullOrEmpty(folder)) return;

        var ok = await DialogService.ShowConfirmAsync(
            "복원 확인",
            "선택한 백업으로 모든 데이터를 덮어씁니다. 계속할까요?\n복원 후 앱을 다시 시작해야 변경 사항이 모두 적용됩니다.");
        if (!ok) return;

        if (Settings.Restore(folder))
        {
            _isLoading = true;
            LoadSettings();
            _isLoading = false;
            await DialogService.ShowInfoAsync("복원이 완료되었습니다. 앱을 다시 시작하세요.");
        }
        else
        {
            ShowStatus("복원에 실패했습니다. 올바른 백업 폴더인지 확인하세요.");
        }
    }

    /// <summary>모든 설정을 기본값으로 초기화 (데이터는 보존)</summary>
    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        var ok = await DialogService.ShowConfirmAsync(
            "초기화 확인",
            "모든 설정을 기본값으로 되돌립니다. 학생·일정 등 데이터는 삭제되지 않습니다. 계속할까요?");
        if (!ok) return;

        Settings.ResetToDefaults();
        _isLoading = true;
        LoadSettings();
        _isLoading = false;
        App.ApplyTheme(Theme);
        ShowStatus("설정을 기본값으로 초기화했습니다.");
    }

    /// <summary>데이터 폴더를 탐색기로 열기</summary>
    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = Settings.UserDataPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowStatus($"폴더 열기 실패: {ex.Message}");
        }
    }
}
