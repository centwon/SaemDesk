using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학사일정 페이지 ViewModel.
/// DB 캐시 우선 로드 → NEIS 다운로드 버튼으로 갱신.
/// </summary>
public partial class CalendarPageVM : ViewModelBase
{
    // ── 헤더 정보 ─────────────────────────────────────────
    public string SchoolYearText { get; } =
        $"{Settings.WorkYear}학년도";

    public string SchoolName { get; } =
        string.IsNullOrWhiteSpace(Settings.SchoolName.Value)
            ? "학교 미설정"
            : Settings.SchoolName.Value;

    public bool HasSchool => !string.IsNullOrWhiteSpace(Settings.SchoolCode.Value);
    public bool HasApiKey => !string.IsNullOrWhiteSpace(Settings.NeisApiKey.Value);

    // ── 이벤트 목록 ───────────────────────────────────────
    public ObservableCollection<SchoolScheduleGroup> Events { get; } = [];

    // ── 상태 ─────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _errorText = string.Empty;

    [ObservableProperty] private string _statusText = string.Empty;

    public bool HasEvents => Events.Count > 0;
    public bool IsEmpty   => !IsLoading && !HasEvents && string.IsNullOrEmpty(ErrorText);

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public CalendarPageVM()
    {
        _ = LoadCalendarAsync();
    }

    // ────────────────────────────────────────────────────
    //  Commands
    // ────────────────────────────────────────────────────

    /// <summary>로컬 DB에서 학사일정 로드</summary>
    [RelayCommand]
    private async Task LoadCalendarAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;
        try
        {
            if (!HasSchool)
            {
                StatusText = string.Empty;
                return;
            }

            using var svc = new SchoolScheduleService(SchoolDatabase.DbPath);
            var list   = await svc.GetSchedulesByYearAsync(
                Settings.SchoolCode.Value, Settings.WorkYear.Value);
            var groups = SchoolScheduleGroupHelper.GroupSchedules(list);

            Events.Clear();
            foreach (var g in groups)
                Events.Add(g);

            StatusText = Events.Count > 0 ? $"{Events.Count}개 일정" : string.Empty;
            OnPropertyChanged(nameof(HasEvents));
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            ErrorText = $"로드 오류: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[CalendarPageVM] 로드 오류: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>NEIS API에서 학사일정 다운로드 후 DB에 저장, 화면 갱신</summary>
    [RelayCommand]
    private async Task DownloadFromNeisAsync()
    {
        if (!HasSchool)
        {
            ErrorText = "설정에서 학교를 먼저 등록해 주세요.";
            return;
        }
        if (!HasApiKey)
        {
            ErrorText = "설정에서 NEIS API 키를 먼저 입력해 주세요.";
            return;
        }

        IsLoading  = true;
        ErrorText  = string.Empty;
        StatusText = "NEIS에서 다운로드 중…";
        try
        {
            using var svc = new SchoolScheduleService(SchoolDatabase.DbPath);
            var result = await svc.DownloadSchedulesAsync(
                Settings.SchoolCode.Value,
                Settings.ProvinceCode.Value,
                Settings.WorkYear.Value);

            if (result.Success)
            {
                StatusText = result.Message;
                await LoadCalendarAsync();       // 화면 갱신 (IsLoading 재관리됨)
            }
            else
            {
                ErrorText  = result.Message;
                StatusText = string.Empty;
                IsLoading  = false;
            }
        }
        catch (Exception ex)
        {
            ErrorText  = $"다운로드 오류: {ex.Message}";
            IsLoading  = false;
            System.Diagnostics.Debug.WriteLine($"[CalendarPageVM] 다운로드 오류: {ex}");
        }
    }
}
