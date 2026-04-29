using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>학급일지 페이지.</summary>
public partial class DiaryPageVM : ViewModelBase
{
    public ObservableCollection<ClassDiary> Diaries { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiaries))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ClassDiary? _selectedDiary;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;

    public bool HasDiaries  => Diaries.Count > 0;
    public bool IsEmpty     => !IsLoading && !HasDiaries && string.IsNullOrEmpty(ErrorText);
    public bool HasSelection => SelectedDiary is not null;

    public string DiaryCountText =>
        $"{Settings.WorkYear}학년도 {Settings.WorkSemester}학기 · {Settings.HomeGrade}학년 {Settings.HomeRoom}반";

    // ── 오늘 일지가 이미 있는지 ──────────────────────────
    public bool TodayDiaryExists =>
        Diaries.Count > 0 && Diaries[0].Date == DateTime.Today;

    public string WriteTodayButtonText =>
        TodayDiaryExists ? "오늘 일지 수정" : "오늘 일지 작성";

    public DiaryPageVM()
    {
        _ = LoadDiariesAsync();
    }

    // ────────────────────────────────────────────────────
    //  목록 로드
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadDiariesAsync()
    {
        IsLoading  = true;
        ErrorText  = string.Empty;
        StatusText = "불러오는 중…";

        try
        {
            using var repo = new ClassDiaryRepository(SchoolDatabase.DbPath);
            var list = await repo.GetByClassAsync(
                Settings.SchoolCode.Value,
                Settings.WorkYear.Value,
                Settings.HomeGrade.Value,
                Settings.HomeRoom.Value);

            list.Sort((a, b) => b.Date.CompareTo(a.Date));   // 최신 먼저

            Diaries.Clear();
            foreach (var d in list)
                Diaries.Add(d);

            StatusText = $"총 {Diaries.Count}건";
        }
        catch (Exception ex)
        {
            StatusText = string.Empty;
            ErrorText  = "학급일지를 불러오지 못했습니다.";
            System.Diagnostics.Debug.WriteLine($"[DiaryPageVM] {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasDiaries));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(TodayDiaryExists));
            OnPropertyChanged(nameof(WriteTodayButtonText));
        }
    }

    // ────────────────────────────────────────────────────
    //  오늘 일지 작성 / 수정
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task WriteTodayDiaryAsync()
    {
        ClassDiary? existing = TodayDiaryExists ? Diaries[0] : null;
        var result = await DialogService.ShowDiaryEditAsync(existing);
        if (result is null) return;
        await LoadDiariesAsync();
    }

    // ────────────────────────────────────────────────────
    //  선택된 일지 수정
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task EditSelectedDiaryAsync()
    {
        if (SelectedDiary is null) return;
        var result = await DialogService.ShowDiaryEditAsync(SelectedDiary);
        if (result is null) return;
        await LoadDiariesAsync();
    }

    // ────────────────────────────────────────────────────
    //  선택된 일지 삭제
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteSelectedDiaryAsync()
    {
        if (SelectedDiary is null) return;

        bool ok = await DialogService.ShowConfirmAsync(
            "일지 삭제",
            $"{SelectedDiary.Date:M월 d일} 학급일지를 삭제하시겠습니까?");
        if (!ok) return;

        try
        {
            using var svc = new ClassDiaryService(SchoolDatabase.DbPath);
            await svc.DeleteDiaryAsync(SelectedDiary.No);
            SelectedDiary = null;
            await LoadDiariesAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[DiaryPageVM] 삭제 오류: {ex.Message}");
        }
    }
}
