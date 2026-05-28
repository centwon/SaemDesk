using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Collections;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 동아리 홈 ViewModel.
/// 원본 NewSchool ClubHomePage 와 동등 —
/// 동아리 선택 + 부원 명단 표시. 자료실은 추후 Board 임베드.
/// </summary>
public partial class ClubHomePageVM : ViewModelBase
{
    public OptimizedObservableCollection<Club>           Clubs   { get; } = new();
    public OptimizedObservableCollection<ClubEnrollment> Members { get; } = new();

    [ObservableProperty] private Club?  _selectedClub;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private int    _year = Settings.WorkYear.Value;

    public ClubHomePageVM() => _ = LoadClubsAsync();

    [RelayCommand]
    private async Task LoadClubsAsync()
    {
        IsBusy = true;
        try
        {
            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            var list = await repo.GetBySchoolAsync(Settings.SchoolCode.Value, Year);
            Clubs.ReplaceAll(list);
            if (Clubs.Count > 0) SelectedClub = Clubs[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClubHomePageVM] {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    public async Task LoadMembersAsync()
    {
        if (SelectedClub is null) return;
        try
        {
            using var repo = new ClubEnrollmentRepository(SchoolDatabase.DbPath);
            var list = await repo.GetByClubAsync(SelectedClub.No);
            Members.ReplaceAll(list);
            StatusText = $"부원 {Members.Count}명";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClubHomePageVM] Members: {ex.Message}");
        }
    }
}
