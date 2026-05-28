using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Collections;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>수업 관리 페이지.</summary>
public partial class LessonsPageVM : ViewModelBase
{
    // 전체 시간표 (5일치 캐시)
    private List<ClassTimetable> _allSlots = [];

    public OptimizedObservableCollection<ClassTimetable> DaySlots { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSlots))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ClassTimetable? _selectedSlot;

    // 1=월 ... 5=금
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonSelected))]
    [NotifyPropertyChangedFor(nameof(IsTueSelected))]
    [NotifyPropertyChangedFor(nameof(IsWedSelected))]
    [NotifyPropertyChangedFor(nameof(IsThuSelected))]
    [NotifyPropertyChangedFor(nameof(IsFriSelected))]
    [NotifyPropertyChangedFor(nameof(SelectedDayName))]
    private int _selectedDay;

    [ObservableProperty] private string _statusText  = string.Empty;
    [ObservableProperty] private string _errorText   = string.Empty;

    public bool HasSlots     => DaySlots.Count > 0;
    public bool IsEmpty      => !IsLoading && !HasSlots;
    public bool HasSelection => SelectedSlot is not null;

    public bool IsMonSelected => SelectedDay == 1;
    public bool IsTueSelected => SelectedDay == 2;
    public bool IsWedSelected => SelectedDay == 3;
    public bool IsThuSelected => SelectedDay == 4;
    public bool IsFriSelected => SelectedDay == 5;

    public string SelectedDayName => SelectedDay switch
    {
        1 => "월요일", 2 => "화요일", 3 => "수요일", 4 => "목요일", 5 => "금요일",
        _ => string.Empty
    };

    public string ClassHeaderText =>
        $"{Settings.HomeGrade}학년 {Settings.HomeRoom}반 시간표";

    public string SchoolYearText =>
        $"{Settings.WorkYear}학년도 {Settings.WorkSemester}학기";

    public LessonsPageVM()
    {
        // 오늘 요일 (1=월...5=금, 주말이면 월요일)
        int dow = (int)DateTime.Today.DayOfWeek;  // 0=일, 1=월...
        _selectedDay = dow >= 1 && dow <= 5 ? dow : 1;

        _ = LoadTimetableAsync();
    }

    [RelayCommand]
    private async Task LoadTimetableAsync()
    {
        IsLoading  = true;
        ErrorText  = string.Empty;
        StatusText = "불러오는 중…";

        try
        {
            using var repo = new ClassTimetableRepository(SchoolDatabase.DbPath);
            _allSlots = await repo.GetByClassAsync(
                Settings.SchoolCode.Value,
                Settings.WorkYear.Value,
                Settings.WorkSemester.Value,
                Settings.HomeGrade.Value,
                Settings.HomeRoom.Value);

            FilterByDay(SelectedDay);
            StatusText = $"총 {_allSlots.Count}칸";
        }
        catch (Exception ex)
        {
            StatusText = string.Empty;
            ErrorText  = "시간표를 불러오지 못했습니다.";
            System.Diagnostics.Debug.WriteLine($"[LessonsPageVM] {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>선택 요일 변경 시 필터 재적용.</summary>
    partial void OnSelectedDayChanged(int value) => FilterByDay(value);

    private void FilterByDay(int day)
    {
        SelectedSlot = null;
        DaySlots.ReplaceAll(_allSlots.Where(x => x.DayOfWeek == day).OrderBy(x => x.Period));

        OnPropertyChanged(nameof(HasSlots));
        OnPropertyChanged(nameof(IsEmpty));
    }

    // ────────────────────────────────────────────────────
    //  CRUD
    // ────────────────────────────────────────────────────

    /// <summary>현재 선택된 요일에 새 슬롯 추가 (다음 빈 교시로).</summary>
    [RelayCommand]
    private async Task AddSlotAsync()
    {
        // 현재 요일에서 가장 큰 교시 + 1 (없으면 1)
        int nextPeriod = DaySlots.Count > 0
            ? DaySlots.Max(s => s.Period) + 1
            : 1;
        if (nextPeriod > 10) nextPeriod = 1;

        int day = SelectedDay >= 1 && SelectedDay <= 5 ? SelectedDay : 1;

        var saved = await DialogService.ShowTimetableEditAsync(
            existing: null,
            defaultDay: day,
            defaultPeriod: nextPeriod);

        if (saved is null) return;
        await LoadTimetableAsync();
    }

    [RelayCommand]
    private async Task EditSelectedSlotAsync()
    {
        if (SelectedSlot is null) return;
        var saved = await DialogService.ShowTimetableEditAsync(SelectedSlot);
        if (saved is null) return;
        await LoadTimetableAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedSlotAsync()
    {
        if (SelectedSlot is null) return;

        bool ok = await DialogService.ShowConfirmAsync(
            "시간표 삭제",
            $"{SelectedSlot.DayName}요일 {SelectedSlot.Period}교시 " +
            $"({SelectedSlot.SubjectName}) 항목을 삭제하시겠습니까?");
        if (!ok) return;

        try
        {
            using var repo = new ClassTimetableRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(SelectedSlot.No);
            SelectedSlot = null;
            await LoadTimetableAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[LessonsPageVM] 삭제 오류: {ex.Message}");
        }
    }

    // 요일 버튼 커맨드
    [RelayCommand] private void SelectMon() => SelectedDay = 1;
    [RelayCommand] private void SelectTue() => SelectedDay = 2;
    [RelayCommand] private void SelectWed() => SelectedDay = 3;
    [RelayCommand] private void SelectThu() => SelectedDay = 4;
    [RelayCommand] private void SelectFri() => SelectedDay = 5;
}
