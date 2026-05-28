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

namespace SaemDesk.ViewModels.Pages;

// ── 주간 시간표 요일별 항목 ────────────────────────────────────
/// <summary>요일별 수업 묶음 (SchedulerPage 바인딩용).</summary>
public sealed class WeekDayEntry
{
    public string DayName   { get; set; } = string.Empty;
    public string DateLabel { get; set; } = string.Empty;
    public bool   IsToday   { get; set; }
    public List<ClassTimetable> Slots { get; } = [];
    public bool   HasSlots      => Slots.Count > 0;
    public string SlotCountText => HasSlots ? $"{Slots.Count}교시" : "수업 없음";
}

// ────────────────────────────────────────────────────────────

/// <summary>
/// 스케줄러(주간 시간표) 페이지 ViewModel.
/// 이번 주 월~금 시간표를 한눈에 보여준다.
/// </summary>
public partial class SchedulerPageVM : ViewModelBase
{
    // ── 헤더 ─────────────────────────────────────────────
    [ObservableProperty] private string _weekRangeText = string.Empty;

    public string SchoolYearText =>
        $"{Settings.WorkYear}학년도 {Settings.WorkSemester}학기";

    public string ClassHeaderText =>
        $"{Settings.HomeGrade}학년 {Settings.HomeRoom}반";

    // ── 데이터 ───────────────────────────────────────────
    public OptimizedObservableCollection<WeekDayEntry> WeekDays { get; } = new();

    // ── 상태 ─────────────────────────────────────────────
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _errorText = string.Empty;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public SchedulerPageVM()
    {
        _ = LoadWeeklyAsync();
    }

    // ────────────────────────────────────────────────────
    //  이번 주 시간표 로드
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadWeeklyAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;
        try
        {
            // 이번 주 월요일 계산 (.NET DayOfWeek: 0=Sun, 1=Mon)
            var today  = DateTime.Today;
            int offset = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
            if (offset < 0) offset += 7;           // 일요일이면 6일 뒤의 월요일이 아니라 이번 주 월요일
            var monday = today.AddDays(-offset);

            WeekRangeText = $"{monday:M월 d일}(월) – {monday.AddDays(4):M월 d일}(금)";

            // DB에서 전체 시간표 로드
            using var repo = new ClassTimetableRepository(SchoolDatabase.DbPath);
            var all = await repo.GetByClassAsync(
                Settings.SchoolCode.Value,
                Settings.WorkYear.Value,
                Settings.WorkSemester.Value,
                Settings.HomeGrade.Value,
                Settings.HomeRoom.Value);

            // 요일별로 분류 (ClassTimetable.DayOfWeek: 1=월 ~ 5=금)
            string[] dayNames = ["월", "화", "수", "목", "금"];
            var days = new List<WeekDayEntry>(5);
            for (int i = 0; i < 5; i++)
            {
                var date  = monday.AddDays(i);
                var entry = new WeekDayEntry
                {
                    DayName   = dayNames[i],
                    DateLabel = date.ToString("M/d"),
                    IsToday   = date == today,
                };
                entry.Slots.AddRange(
                    all.Where(x => x.DayOfWeek == i + 1)
                       .OrderBy(x => x.Period));
                days.Add(entry);
            }

            WeekDays.ReplaceAll(days);
        }
        catch (Exception ex)
        {
            ErrorText = $"로드 오류: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[SchedulerPageVM] 로드 오류: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
