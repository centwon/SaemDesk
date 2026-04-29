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
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학생 누가기록(StudentLog) 페이지 ViewModel — NewSchool PageStudentLog MVP 포팅.
/// 기간 + 카테고리 필터로 학급 전체의 누가기록을 조회/추가/편집/삭제.
/// </summary>
public partial class StudentLogPageVM : ViewModelBase
{
    private readonly List<StudentLog> _all = new();

    public ObservableCollection<StudentLog>  Logs       { get; } = new();
    public ObservableCollection<LogCategory> Categories { get; } = new(new[]
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
    [NotifyPropertyChangedFor(nameof(HasLogs))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private StudentLog? _selectedLog;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private DateTime _startDate = DateTime.Today.AddMonths(-3);
    [ObservableProperty] private DateTime _endDate   = DateTime.Today;
    [ObservableProperty] private LogCategory _selectedCategory = LogCategory.전체;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;

    public bool HasLogs      => Logs.Count > 0;
    public bool IsEmpty      => !IsLoading && !HasLogs && string.IsNullOrEmpty(ErrorText);
    public bool HasSelection => SelectedLog is not null;

    public string ClassHeaderText
    {
        get
        {
            int g = Settings.HomeGrade.Value;
            int r = Settings.HomeRoom.Value;
            return (g <= 0 || r <= 0) ? "담임 학급 미설정" : $"{g}학년 {r}반";
        }
    }

    public StudentLogPageVM()
    {
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
            string sc = Settings.SchoolCode.Value;
            int year  = Settings.WorkYear.Value;
            int grade = Settings.HomeGrade.Value;
            int room  = Settings.HomeRoom.Value;

            if (string.IsNullOrEmpty(sc) || year <= 0 || grade <= 0 || room <= 0)
            {
                ErrorText = "학교/학년도/담임 학급 설정을 먼저 완료해 주세요.";
                _all.Clear();
                ApplyFilter();
                return;
            }

            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);
            var list = await repo.GetByClassAndDateRangeAsync(sc, year, grade, room, StartDate.Date, EndDate.Date);

            _all.Clear();
            _all.AddRange(list);
            ApplyFilter();
            StatusText = $"총 {_all.Count}건 — {ClassHeaderText}";
        }
        catch (Exception ex)
        {
            ErrorText = "누가기록을 불러오지 못했습니다.";
            Debug.WriteLine($"[StudentLogPageVM] {ex}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasLogs));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(LogCategory value) => ApplyFilter();

    private void ApplyFilter()
    {
        Logs.Clear();
        IEnumerable<StudentLog> filtered = _all;

        if (SelectedCategory != LogCategory.전체)
            filtered = filtered.Where(l => l.Category == SelectedCategory);

        string keyword = SearchText ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(l =>
                (l.ActivityName?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (l.Log?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)          == true) ||
                (l.Topic?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)        == true) ||
                (l.StudentID?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)    == true));
        }

        foreach (var l in filtered.OrderByDescending(x => x.Date).ThenBy(x => x.StudentID))
            Logs.Add(l);

        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task AddLogAsync()
    {
        // 빈 학생ID 로 추가 다이얼로그 열기 (다이얼로그 안에서 학생 선택 가능)
        bool saved = await DialogService.ShowStudentLogEditAsync(string.Empty, string.Empty);
        if (saved) await QueryAsync();
    }

    [RelayCommand]
    private async Task EditSelectedAsync()
    {
        if (SelectedLog is null) return;
        bool saved = await DialogService.ShowStudentLogEditAsync(SelectedLog.StudentID, string.Empty, SelectedLog);
        if (saved) await QueryAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedLog is null) return;
        bool ok = await DialogService.ShowConfirmAsync(
            "기록 삭제",
            $"'{SelectedLog.ActivityName}' 기록을 삭제하시겠습니까?\n복구할 수 없습니다.");
        if (!ok) return;

        try
        {
            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(SelectedLog.No);
            SelectedLog = null;
            await QueryAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
        }
    }
}
