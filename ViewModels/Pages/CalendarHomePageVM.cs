using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SaemDesk.Models;
using SaemDesk.Scheduler;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 통합 달력(月) ViewModel — NewSchool Kcalendar 동등.
/// View 가 BaseDate 변경 시 LoadAsync() 를 호출, DayInfo[42] 를 반환받아 셀에 매핑한다.
/// </summary>
public partial class CalendarHomePageVM : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderText))]
    private DateTime _baseDate = DateTime.Today;

    [ObservableProperty] private bool _isLoading;

    public string HeaderText => $"{BaseDate:yyyy년 M월}";
    public string AppVersion => AppInfo.Version;

    private List<SchoolSchedule> _schedules = new();
    private List<KEvent>         _events    = new();

    /// <summary>현재 BaseDate 기준 6×7=42칸의 DayInfo 배열을 만들어 반환.</summary>
    public async Task<DayInfo[]> LoadAsync()
    {
        IsLoading = true;
        try
        {
            var firstOfMonth = new DateTime(BaseDate.Year, BaseDate.Month, 1);
            int dow          = (int)firstOfMonth.DayOfWeek;
            var calStart     = firstOfMonth.AddDays(-dow);
            var calEnd       = calStart.AddDays(42);

            // 학사일정
            _schedules = new();
            if (Settings.ShowEvents.Value && !string.IsNullOrEmpty(Settings.SchoolCode.Value))
            {
                try
                {
                    using var schedSvc = new SchoolScheduleService(SchoolDatabase.DbPath);
                    var (ok, _, list) = await schedSvc.GetSchedulesByDataRangeAsync(
                        Settings.SchoolCode.Value, calStart, calEnd);
                    if (ok) _schedules = list;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CalendarHomeVM] 학사일정 로드 실패: {ex.Message}");
                }
            }

            // KEvent (event + task)
            _events = new();
            try
            {
                using var svc = Scheduler.Scheduler.CreateService();
                var all = await svc.GetEventsByDateAsync(calStart, 42);

                if (Settings.ShowTasks.Value)
                {
                    var tasks = await svc.GetTasksByDateAsync(calStart, 42, true);
                    var eventOnly = all.Where(e => e.ItemType != "task").ToList();
                    _events = new List<KEvent>(eventOnly.Count + tasks.Count);
                    _events.AddRange(tasks);
                    _events.AddRange(eventOnly);
                }
                else
                {
                    _events = all.Where(e => e.ItemType != "task").ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalendarHomeVM] KEvent 로드 실패: {ex.Message}");
            }

            // DayInfo 빌드
            var infos = new DayInfo[42];
            int currentMonth = BaseDate.Month;
            for (int i = 0; i < 42; i++)
            {
                var d = calStart.AddDays(i);
                var schedules = _schedules.Where(x => x.AA_YMD.Date == d.Date).ToList();
                var dayItems  = _events.Where(x => d.Date >= x.Start.Date && d.Date <= x.End.Date).ToList();
                var tasks     = dayItems.Where(e => e.ItemType == "task").ToList();
                var events    = dayItems.Where(e => e.ItemType != "task").ToList();
                infos[i] = new DayInfo(d, schedules, tasks, events,
                                       isInCurrentMonth: d.Month == currentMonth);
            }
            return infos;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void GotoPreviousMonth() => BaseDate = BaseDate.AddMonths(-1);
    public void GotoNextMonth()     => BaseDate = BaseDate.AddMonths( 1);
    public void GotoToday()         => BaseDate = DateTime.Today;
}
