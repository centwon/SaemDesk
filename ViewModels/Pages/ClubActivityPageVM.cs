using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 동아리 활동 기록 페이지 ViewModel.
/// 원본 NewSchool ClubActivityPage — 동아리 선택 후
/// ListStudent(부원) + LogListViewer(활동 기록) 표시.
/// </summary>
public partial class ClubActivityPageVM : ViewModelBase
{
    public ObservableCollection<Club>       Clubs      { get; } = new();
    public ObservableCollection<LogCategory> Categories { get; } = new(new[]
    {
        LogCategory.동아리활동,
        LogCategory.전체,
    });

    [ObservableProperty] private Club?        _selectedClub;
    [ObservableProperty] private LogCategory  _selectedCategory = LogCategory.동아리활동;
    [ObservableProperty] private int          _year       = Settings.WorkYear.Value;
    [ObservableProperty] private bool         _isBusy;
    [ObservableProperty] private string       _statusText = string.Empty;

    public ClubActivityPageVM() => _ = LoadClubsAsync();

    [RelayCommand]
    private async Task LoadClubsAsync()
    {
        try
        {
            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            var list = await repo.GetBySchoolAsync(Settings.SchoolCode.Value, Year);
            Clubs.Clear();
            foreach (var c in list) Clubs.Add(c);
            if (Clubs.Count > 0) SelectedClub = Clubs[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClubActivityPageVM] LoadClubs: {ex.Message}");
        }
    }
}
