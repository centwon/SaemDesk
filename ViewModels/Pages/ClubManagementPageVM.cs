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
/// 동아리 관리 — 학년도 필터, 추가/수정/삭제, 검색.
/// </summary>
public partial class ClubManagementPageVM : ViewModelBase
{
    public ObservableCollection<Club> Items { get; } = new();

    [ObservableProperty] private int _year = Settings.WorkYear;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private Club? _selected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;

    // 편집 폼
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formRoom = string.Empty;
    [ObservableProperty] private string _formRemark = string.Empty;

    public bool HasSelection => Selected is not null;

    public ClubManagementPageVM() { _ = ReloadAsync(); }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            var schoolCode = Settings.SchoolCode.Value;
            var rows = string.IsNullOrEmpty(schoolCode)
                ? (await repo.GetAllAsync()).Where(c => c.Year == Year).ToList()
                : await repo.GetBySchoolAsync(schoolCode, Year);

            if (!string.IsNullOrWhiteSpace(SearchText))
                rows = rows.Where(r => r.ClubName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

            Items.Clear();
            foreach (var c in rows.OrderBy(r => r.ClubName)) Items.Add(c);
            StatusText = $"총 {Items.Count}개";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnSelectedChanged(Club? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        if (value is null)
        {
            FormName = ""; FormRoom = ""; FormRemark = "";
        }
        else
        {
            FormName = value.ClubName;
            FormRoom = value.ActivityRoom;
            FormRemark = value.Remark;
        }
    }

    [RelayCommand]
    private void NewClub()
    {
        Selected = null;
        FormName = "(새 동아리)";
        FormRoom = "";
        FormRemark = "";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName)) { ErrorText = "동아리명이 비어 있습니다."; return; }
        try
        {
            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            if (Selected is null)
            {
                var c = new Club
                {
                    SchoolCode = Settings.SchoolCode.Value,
                    TeacherID = Settings.UserName.Value,
                    Year = Year,
                    ClubName = FormName,
                    ActivityRoom = FormRoom,
                    Remark = FormRemark,
                };
                c.No = await repo.CreateAsync(c);
                StatusText = "추가됨";
            }
            else
            {
                Selected.ClubName = FormName;
                Selected.ActivityRoom = FormRoom;
                Selected.Remark = FormRemark;
                await repo.UpdateAsync(Selected);
                StatusText = "수정됨";
            }
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        try
        {
            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(Selected.No);
            Items.Remove(Selected);
            Selected = null;
            StatusText = "삭제됨";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }
}
