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
/// 수업 누가 기록 페이지 — NewSchool LessonActivityPage MVP 포팅.
/// 내 차시 기록(LessonLog) 을 학년/학기/과목/학급 필터로 조회.
/// </summary>
public partial class LessonActivityPageVM : ViewModelBase
{
    private readonly List<LessonLog> _all = new();

    public ObservableCollection<LessonLog> Logs       { get; } = new();
    public ObservableCollection<string>    Subjects   { get; } = new();
    public ObservableCollection<string>    ClassLabels { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasLogs))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private LessonLog? _selectedLog;

    [ObservableProperty] private int    _semester = 0; // 0=전체, 1, 2
    [ObservableProperty] private string _subject  = "전체";
    [ObservableProperty] private string _classLabel = "전체";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;

    public bool HasLogs      => Logs.Count > 0;
    public bool IsEmpty      => !IsLoading && !HasLogs && string.IsNullOrEmpty(ErrorText);
    public bool HasSelection => SelectedLog is not null;

    public LessonActivityPageVM()
    {
        Subjects.Add("전체");
        ClassLabels.Add("전체");
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
            using var svc = new LessonLogService();
            int? semFilter = Semester > 0 ? Semester : null;
            var list = await svc.GetMyLessonsAsync(semFilter);

            _all.Clear();
            _all.AddRange(list);
            RebuildLookups();
            ApplyFilter();
            StatusText = $"총 {_all.Count}건 (내 차시 기록)";
        }
        catch (Exception ex)
        {
            ErrorText = "차시 기록을 불러오지 못했습니다.";
            Debug.WriteLine($"[LessonActivityVM] {ex}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasLogs));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private void RebuildLookups()
    {
        var subj = _all.Select(x => x.Subject).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x);
        Subjects.Clear();
        Subjects.Add("전체");
        foreach (var s in subj) Subjects.Add(s);
        if (!Subjects.Contains(Subject)) Subject = "전체";

        var cls = _all.Where(x => x.Grade > 0 && x.Class > 0)
                      .Select(x => $"{x.Grade}-{x.Class}")
                      .Distinct()
                      .OrderBy(x => x);
        ClassLabels.Clear();
        ClassLabels.Add("전체");
        foreach (var c in cls) ClassLabels.Add(c);
        if (!ClassLabels.Contains(ClassLabel)) ClassLabel = "전체";
    }

    partial void OnSubjectChanged(string value)    => ApplyFilter();
    partial void OnClassLabelChanged(string value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Logs.Clear();
        IEnumerable<LessonLog> filtered = _all;

        if (Subject != "전체")
            filtered = filtered.Where(l => l.Subject == Subject);

        if (ClassLabel != "전체")
        {
            var parts = ClassLabel.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int g) &&
                int.TryParse(parts[1], out int c))
            {
                filtered = filtered.Where(l => l.Grade == g && l.Class == c);
            }
        }

        string keyword = SearchText ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(l =>
                (l.Subject?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (l.Topic?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)   == true) ||
                (l.Content?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (l.Note?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)    == true));
        }

        foreach (var l in filtered.OrderByDescending(x => x.Date).ThenBy(x => x.Period))
            Logs.Add(l);

        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task AddLogAsync()
    {
        var (saved, _) = await SaemDesk.Services.DialogService.ShowLessonLogEditAsync();
        if (saved is not null) await QueryAsync();
    }

    [RelayCommand]
    private async Task EditSelectedAsync()
    {
        if (SelectedLog is null) return;
        var (saved, deleted) = await SaemDesk.Services.DialogService.ShowLessonLogEditAsync(SelectedLog);
        if (saved is not null || deleted) await QueryAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedLog is null) return;
        bool ok = await SaemDesk.Services.DialogService.ShowConfirmAsync(
            "차시 기록 삭제",
            $"'{SelectedLog.Subject} - {SelectedLog.Topic}' 차시 기록을 삭제하시겠습니까?\n복구할 수 없습니다.");
        if (!ok) return;

        try
        {
            using var svc = new LessonLogService();
            await svc.DeleteAsync(SelectedLog.No);
            SelectedLog = null;
            await QueryAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
        }
    }
}
