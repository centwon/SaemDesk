using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 동아리 활동 기록 — 동아리 선택 + 부원 + 활동 기록(StudentLog 카테고리=동아리활동).
/// </summary>
public partial class ClubActivityPageVM : ViewModelBase
{
    public ObservableCollection<Club> Clubs { get; } = new();
    public ObservableCollection<ClubEnrollment> Members { get; } = new();
    public ObservableCollection<StudentLog> Logs { get; } = new();

    [ObservableProperty] private Club? _selectedClub;
    [ObservableProperty] private int _year = Settings.WorkYear;
    [ObservableProperty] private int _semester = Settings.WorkSemester;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;

    public int MemberCount => Members.Count;
    public int LogCount => Logs.Count;

    public ClubActivityPageVM() { _ = LoadClubsAsync(); }

    [RelayCommand]
    private async Task LoadClubsAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ErrorText = string.Empty;
        try
        {
            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            var teacherId = Settings.UserName.Value;
            var schoolCode = Settings.SchoolCode.Value;
            List<Club> rows;
            if (!string.IsNullOrWhiteSpace(teacherId))
                rows = (await repo.GetByTeacherAsync(teacherId, Year)).ToList();
            else if (!string.IsNullOrWhiteSpace(schoolCode))
                rows = await repo.GetBySchoolAsync(schoolCode, Year);
            else
                rows = (await repo.GetAllAsync()).Where(c => c.Year == Year).ToList();
            Clubs.Clear();
            foreach (var c in rows) Clubs.Add(c);
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnSelectedClubChanged(Club? value) { _ = ReloadMembersAndLogsAsync(); }

    [RelayCommand]
    private async Task ReloadMembersAndLogsAsync()
    {
        if (SelectedClub is null) { Members.Clear(); Logs.Clear(); return; }
        try
        {
            using var enrollRepo = new ClubEnrollmentRepository(SchoolDatabase.DbPath);
            using var logRepo = new StudentLogRepository(SchoolDatabase.DbPath);

            var members = await enrollRepo.GetByClubAsync(SelectedClub.No);
            Members.Clear();
            foreach (var m in members) Members.Add(m);

            var teacherId = Settings.UserName.Value;
            var teacherLogs = string.IsNullOrEmpty(teacherId)
                ? new List<StudentLog>()
                : await logRepo.GetByTeacherAsync(teacherId, Year, Semester);
            var clubLogs = teacherLogs
                .Where(l => l.Category == LogCategory.동아리활동 && l.ClubNo == SelectedClub.No)
                .OrderByDescending(l => l.Date)
                .ToList();
            Logs.Clear();
            foreach (var l in clubLogs) Logs.Add(l);

            StatusText = $"부원 {Members.Count}명 · 기록 {Logs.Count}건";
            OnPropertyChanged(nameof(MemberCount));
            OnPropertyChanged(nameof(LogCount));
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task AddLogAsync(string? content)
    {
        if (SelectedClub is null) { ErrorText = "동아리를 선택하세요."; return; }
        if (string.IsNullOrWhiteSpace(content)) { ErrorText = "내용이 비어 있습니다."; return; }
        try
        {
            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);
            var log = new StudentLog
            {
                StudentID = string.Empty, // 단체 기록(부원 전체)
                TeacherID = Settings.UserName.Value,
                Year = Year,
                Semester = Semester,
                Date = DateTime.Today,
                Category = LogCategory.동아리활동,
                ClubNo = SelectedClub.No,
                ClubName = SelectedClub.ClubName,
                Log = content,
            };
            log.No = await repo.CreateAsync(log);
            Logs.Insert(0, log);
            StatusText = "활동 기록 추가됨";
            OnPropertyChanged(nameof(LogCount));
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }
}
