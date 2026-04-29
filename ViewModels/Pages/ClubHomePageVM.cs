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
/// 동아리 홈 — 담임 동아리 카드 목록 + 부원 수 + 활동실.
/// </summary>
public partial class ClubHomePageVM : ViewModelBase
{
    public ObservableCollection<ClubCardVM> Clubs { get; } = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _errorText = string.Empty;

    [ObservableProperty]
    private int _year = Settings.WorkYear;

    public bool IsEmpty => Clubs.Count == 0 && !IsBusy;

    public ClubHomePageVM() { _ = ReloadAsync(); }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            using var clubRepo = new ClubRepository(SchoolDatabase.DbPath);
            using var enrollRepo = new ClubEnrollmentRepository(SchoolDatabase.DbPath);
            var teacherId = Settings.UserName.Value;
            var schoolCode = Settings.SchoolCode.Value;
            List<Club> rows;
            if (!string.IsNullOrWhiteSpace(teacherId))
                rows = (await clubRepo.GetByTeacherAsync(teacherId, Year)).ToList();
            else if (!string.IsNullOrWhiteSpace(schoolCode))
                rows = await clubRepo.GetBySchoolAsync(schoolCode, Year);
            else
                rows = (await clubRepo.GetAllAsync()).Where(c => c.Year == Year).ToList();

            Clubs.Clear();
            foreach (var c in rows)
            {
                var members = await enrollRepo.GetByClubAsync(c.No);
                Clubs.Add(new ClubCardVM(c, members.Count));
            }
            StatusText = $"동아리 {Clubs.Count}개";
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; OnPropertyChanged(nameof(IsEmpty)); }
    }
}

public sealed class ClubCardVM
{
    public Club Source { get; }
    public int MemberCount { get; }
    public ClubCardVM(Club c, int n) { Source = c; MemberCount = n; }
    public string Name => Source.ClubName;
    public string Room => string.IsNullOrEmpty(Source.ActivityRoom) ? "활동실 미지정" : Source.ActivityRoom;
    public string YearText => $"{Source.Year}학년도";
    public string MemberText => $"부원 {MemberCount}명";
    public string Remark => Source.Remark ?? string.Empty;
}
