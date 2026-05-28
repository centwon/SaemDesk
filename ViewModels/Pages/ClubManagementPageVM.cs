using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Collections;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 동아리 관리 — CourseManagementPageVM 패턴. 학년도 필터, 카드별 추가/수정/삭제/부원 관리.
/// </summary>
public partial class ClubManagementPageVM : ViewModelBase
{
    // 현재 필터 값 — YearSemesterPicker 이벤트로 주입
    public int FilterYear { get; private set; } =
        Settings.WorkYear.Value > 0 ? Settings.WorkYear.Value : DateTime.Today.Year;

    /// <summary>VM 생성 시점의 로그인 교사 이름 (설정에서 로드)</summary>
    public string TeacherName { get; } =
        string.IsNullOrWhiteSpace(Settings.UserName.Value)
            ? Settings.User.Value
            : Settings.UserName.Value;

    public OptimizedObservableCollection<Club> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasClubs))]
    private bool _isLoading;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;

    public bool HasClubs => Items.Count > 0;
    public bool IsEmpty  => !IsLoading && !HasClubs && string.IsNullOrEmpty(ErrorText);

    public ClubManagementPageVM() { }

    /// <summary>YearSemesterPicker 이벤트로 호웉 — 학년도 갱신 후 재조회.</summary>
    public void SetFilter(int year)
    {
        FilterYear = year;
        _ = QueryAsync();
    }

    [RelayCommand]
    private async Task QueryAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;
        StatusText = "조회 중…";
        try
        {
            var teacherId = Settings.User.Value;
            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            var list = await repo.GetByTeacherAsync(teacherId, FilterYear);

            Items.ReplaceAll(list.OrderBy(x => x.ClubName));

            StatusText = $"총 {list.Count}개 동아리";
        }
        catch (Exception ex)
        {
            ErrorText = "동아리를 불러오지 못했습니다.";
            Debug.WriteLine($"[ClubMgmtVM] {ex}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasClubs));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>부원 관리 버튼 전용 — 특정 Club 동아리 부원 다이얼로그 오픈.</summary>
    [RelayCommand]
    private async Task EnrollMembersAsync(Club? club)
    {
        if (club is null) return;
        await DialogService.ShowClubEnrollmentAsync(club);
    }

    /// <summary>카드 버튼 전용 — 특정 Club 직접 수정.</summary>
    [RelayCommand]
    private async Task EditItemAsync(Club? club)
    {
        if (club is null) return;
        var saved = await DialogService.ShowClubEditAsync(
            club.SchoolCode, club.TeacherID, club.Year, club);
        if (saved) await QueryAsync();
    }

    /// <summary>카드 버튼 전용 — 특정 Club 직접 삭제.</summary>
    [RelayCommand]
    private async Task DeleteItemAsync(Club? club)
    {
        if (club is null) return;
        bool ok = await DialogService.ShowConfirmAsync(
            "동아리 삭제",
            $"'{club.ClubName}' 동아리를 삭제하시겠습니까?\n등록된 부원 정보도 함께 삭제됩니다.");
        if (!ok) return;
        try
        {
            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(club.No);
            await QueryAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddClubAsync()
    {
        var saved = await DialogService.ShowClubEditAsync(
            Settings.SchoolCode.Value, Settings.User.Value, FilterYear);
        if (saved) await QueryAsync();
    }
}
