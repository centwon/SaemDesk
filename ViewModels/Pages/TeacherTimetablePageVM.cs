using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Services;
using SaemDesk.ViewModels;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 교사 시간표 — 학년도/학기 → 5일×7교시 그리드.
/// </summary>
public partial class TeacherTimetablePageVM : ViewModelBase
{
    public ObservableCollection<int> Years { get; } = new();
    public TimetableViewModel Timetable { get; } = new();
    public ObservableCollection<PeriodRowVM> PeriodRows { get; } = new();

    [ObservableProperty] private int _year = Settings.WorkYear;
    [ObservableProperty] private int _semester = Math.Max(1, Settings.WorkSemester);
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;

    public TeacherTimetablePageVM()
    {
        var now = DateTime.Today.Year;
        for (int y = now - 5; y <= now + 1; y++) Years.Add(y);
        Timetable.InitializeEmptyTimetable();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = "조회 중...";
        try
        {
            using var svc = new TimetableService(SchoolDatabase.DbPath);
            var teacherId = Settings.UserName.Value;
            if (string.IsNullOrWhiteSpace(teacherId))
            {
                ErrorText = "교사 ID(설정 → 사용자명)가 비어 있습니다.";
                return;
            }
            var vm = await svc.GetTeacherTimetableAsync(teacherId, Year, Semester);
            Timetable.Title = vm.Title;
            Timetable.Year = vm.Year;
            Timetable.Semester = vm.Semester;
            Timetable.Items.Clear();
            foreach (var i in vm.Items) Timetable.Items.Add(i);
            int filled = Timetable.Items.Count(i => !i.IsEmpty);
            HasResult = filled > 0;
            StatusText = HasResult ? $"{filled}교시 배정됨" : "배정된 수업이 없습니다.";

            PeriodRows.Clear();
            for (int p = 1; p <= 7; p++)
            {
                PeriodRows.Add(new PeriodRowVM
                {
                    Period = p,
                    Mon = Timetable.GetItem(1, p) ?? new TimetableItemViewModel { DayOfWeek = 1, Period = p, IsEmpty = true },
                    Tue = Timetable.GetItem(2, p) ?? new TimetableItemViewModel { DayOfWeek = 2, Period = p, IsEmpty = true },
                    Wed = Timetable.GetItem(3, p) ?? new TimetableItemViewModel { DayOfWeek = 3, Period = p, IsEmpty = true },
                    Thu = Timetable.GetItem(4, p) ?? new TimetableItemViewModel { DayOfWeek = 4, Period = p, IsEmpty = true },
                    Fri = Timetable.GetItem(5, p) ?? new TimetableItemViewModel { DayOfWeek = 5, Period = p, IsEmpty = true },
                });
            }
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }
}

public sealed class PeriodRowVM
{
    public int Period { get; set; }
    public TimetableItemViewModel Mon { get; set; } = new();
    public TimetableItemViewModel Tue { get; set; } = new();
    public TimetableItemViewModel Wed { get; set; } = new();
    public TimetableItemViewModel Thu { get; set; } = new();
    public TimetableItemViewModel Fri { get; set; } = new();
}
