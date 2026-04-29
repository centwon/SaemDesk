using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학생부 특기사항 페이지 ViewModel — NewSchool StudentSpecPage MVP 포팅.
/// 학년도 + 카테고리 필터로 조회하고, 학생/카테고리별 그룹으로 표시.
/// </summary>
public partial class StudentSpecPageVM : ViewModelBase
{
    private readonly List<StudentSpecial> _all = new();

    public ObservableCollection<StudentSpecial> Specs    { get; } = new();
    public ObservableCollection<LogCategory>    Categories { get; } = new(new[]
    {
        LogCategory.전체,
        LogCategory.교과활동,
        LogCategory.개인별세특,
        LogCategory.자율활동,
        LogCategory.동아리활동,
        LogCategory.봉사활동,
        LogCategory.진로활동,
        LogCategory.종합의견,
    });

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasSpecs))]
    private bool _isLoading;

    [ObservableProperty] private StudentSpecial? _selectedSpec;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private decimal _workYear = DateTime.Today.Year;
    [ObservableProperty] private LogCategory _selectedCategory = LogCategory.전체;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;

    public bool HasSpecs => Specs.Count > 0;
    public bool IsEmpty  => !IsLoading && !HasSpecs && string.IsNullOrEmpty(ErrorText);
    public bool HasSelection => SelectedSpec is not null;

    public StudentSpecPageVM()
    {
        // 페이지 진입 시 현재 학년도로 1회 조회
        WorkYear = Settings.WorkYear.Value > 0 ? Settings.WorkYear.Value : DateTime.Today.Year;
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
            using var repo = new StudentSpecialRepository(SchoolDatabase.DbPath);
            int year = (int)WorkYear;
            List<StudentSpecial> list;
            if (SelectedCategory == LogCategory.전체)
                list = await repo.GetByTypeAsync("", year);
            else
                list = await repo.GetByTypeAsync(SelectedCategory.ToString(), year);

            // ↑ GetByTypeAsync 가 빈 type 을 모든 항목으로 처리하지 않을 가능성 — fallback
            if (SelectedCategory == LogCategory.전체)
                list = await GetAllForYearAsync(repo, year);

            _all.Clear();
            _all.AddRange(list);
            ApplyFilter();
            StatusText = $"총 {_all.Count}건";
        }
        catch (Exception ex)
        {
            ErrorText = "특기사항을 불러오지 못했습니다.";
            Debug.WriteLine($"[StudentSpecPageVM] {ex}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasSpecs));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private static async Task<List<StudentSpecial>> GetAllForYearAsync(StudentSpecialRepository repo, int year)
    {
        // 카테고리 별로 모은 다음 합산
        var types = new[] { "교과활동", "개인별세특", "자율활동", "동아리활동", "봉사활동", "진로활동", "종합의견" };
        var all = new List<StudentSpecial>();
        foreach (var t in types)
            all.AddRange(await repo.GetByTypeAsync(t, year));
        return all;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(LogCategory value) => ApplyFilter();

    private void ApplyFilter()
    {
        Specs.Clear();
        string keyword = SearchText ?? string.Empty;
        IEnumerable<StudentSpecial> filtered = _all;

        if (SelectedCategory != LogCategory.전체)
            filtered = filtered.Where(s => s.Type == SelectedCategory.ToString());

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(s =>
                (s.Title?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (s.Content?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (s.SubjectName?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) == true));
        }

        foreach (var s in filtered.OrderBy(x => x.StudentID).ThenBy(x => x.Type).ThenByDescending(x => x.Date))
            Specs.Add(s);

        OnPropertyChanged(nameof(HasSpecs));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedSpec is null) return;
        bool ok = await SaemDesk.Services.DialogService.ShowConfirmAsync(
            "특기사항 삭제",
            $"'{SelectedSpec.Title}' 항목을 삭제하시겠습니까?\n복구할 수 없습니다.");
        if (!ok) return;

        try
        {
            using var repo = new StudentSpecialRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(SelectedSpec.No);
            SelectedSpec = null;
            await QueryAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
        }
    }
}
