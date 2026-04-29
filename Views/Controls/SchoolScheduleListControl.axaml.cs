using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.Views.Controls;

/// <summary>학사일정 목록 — Avalonia 12 이식. NEIS 다운로드 + 기간 조회 지원.</summary>
public partial class SchoolScheduleListControl : UserControl
{
    private ObservableCollection<SchoolScheduleGroup> _scheduleGroups = new();

    public ObservableCollection<SchoolScheduleGroup> ScheduleGroups
    {
        get => _scheduleGroups;
        set
        {
            _scheduleGroups = value ?? new ObservableCollection<SchoolScheduleGroup>();
            ScheduleItemsRepeater.ItemsSource = _scheduleGroups;
        }
    }

    public SchoolScheduleListControl()
    {
        InitializeComponent();
        ScheduleItemsRepeater.ItemsSource = _scheduleGroups;
    }

    public async Task LoadSchedulesAsync(DateTime startDate, int days = 30, bool includeDownload = false)
    {
        try
        {
            using var service = new SchoolScheduleService(SchoolDatabase.DbPath);
            List<SchoolSchedule>? schedules = null;

            if (Settings.IsNeisEventDownloaded.Value || !includeDownload)
            {
                var (ok, msg, list) = await service.GetSchedulesByDataRangeAsync(
                    Settings.SchoolCode, startDate, startDate.AddDays(days + 1));

                if (ok && list != null && list.Any()) schedules = list;
                else Debug.WriteLine($"[SchoolScheduleListControl] 조회 실패: {msg}");
            }
            else
            {
                var (ok, msg, list) = await service.DownloadFromNeisAsync(
                    Settings.SchoolCode, Settings.ProvinceCode, startDate.Year,
                    startDate, startDate.AddDays(days + 1));

                if (ok && list != null)
                {
                    schedules = list;
                    Settings.IsNeisEventDownloaded.Set(true);
                }
                else Debug.WriteLine($"[SchoolScheduleListControl] 다운로드 실패: {msg}");
            }

            _scheduleGroups.Clear();
            if (schedules != null)
            {
                foreach (var g in SchoolScheduleGroupHelper.GroupSchedules(schedules))
                    _scheduleGroups.Add(g);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SchoolScheduleListControl] 로드 오류: {ex.Message}");
        }
    }

    public void SetSchedules(List<SchoolSchedule> schedules)
    {
        try
        {
            _scheduleGroups.Clear();
            if (schedules == null || schedules.Count == 0) return;

            foreach (var g in SchoolScheduleGroupHelper.GroupSchedules(schedules))
                _scheduleGroups.Add(g);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SchoolScheduleListControl] 설정 오류: {ex.Message}");
        }
    }
}
