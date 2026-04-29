using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniExcelLibs;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 학사일정 관리(편집) 페이지 VM —
/// 연도/기간 필터, 행사명·내용·수업공제일·대상학년 인라인 편집,
/// NEIS 동기화·수동 추가·일괄 저장·삭제.
/// </summary>
public partial class SchoolScheduleManagementPageVM : ViewModelBase
{
    public ObservableCollection<int> Years { get; } = new();
    public ObservableCollection<SchoolScheduleItemVM> Items { get; } = new();

    [ObservableProperty]
    private int _selectedYear = Settings.WorkYear;

    [ObservableProperty]
    private DateTime _startDate = new DateTime(Settings.WorkYear, 3, 1);

    [ObservableProperty]
    private DateTime _endDate = new DateTime(Settings.WorkYear + 1, 2, 28);

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _errorText = string.Empty;

    [ObservableProperty]
    private bool _isSelectAll;

    public int ItemCount => Items.Count;
    public int SelectedCount => Items.Count(i => i.IsSelected);
    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    public SchoolScheduleManagementPageVM()
    {
        int now = DateTime.Today.Year;
        for (int y = now - 5; y <= now + 1; y++) Years.Add(y);
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(IsEmpty));
        };
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy) return;
        ErrorText = string.Empty;
        IsBusy = true;
        StatusText = "조회 중...";
        try
        {
            var schoolCode = Settings.SchoolCode.Value;
            if (string.IsNullOrWhiteSpace(schoolCode))
            {
                ErrorText = "학교가 설정되지 않았습니다. 설정에서 학교를 먼저 선택하세요.";
                return;
            }
            using var repo = new SaemDesk.Repositories.SchoolScheduleRepository(SchoolDatabase.DbPath);
            var rows = await repo.GetByDateRangeAsync(schoolCode, StartDate, EndDate);
            Items.Clear();
            foreach (var r in rows.Where(r => r.AY == SelectedYear || SelectedYear == 0))
                Items.Add(new SchoolScheduleItemVM(r));
            StatusText = $"총 {Items.Count}개";
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SyncFromNeisAsync()
    {
        if (IsBusy) return;
        ErrorText = string.Empty;
        IsBusy = true;
        StatusText = "NEIS 동기화 중...";
        try
        {
            using var svc = new SchoolScheduleService(SchoolDatabase.DbPath);
            var schoolCode = Settings.SchoolCode.Value;
            if (string.IsNullOrWhiteSpace(schoolCode))
            {
                ErrorText = "학교가 설정되지 않았습니다.";
                return;
            }
            var rows = await svc.GetSchedulesByYearAsync(schoolCode, SelectedYear);
            StatusText = $"NEIS 다운로드 {rows.Count}건 — 다시 조회하세요.";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddManual()
    {
        var s = new SchoolSchedule
        {
            AY = SelectedYear,
            AA_YMD = DateTime.Today,
            EVENT_NM = "(새 일정)",
            SBTR_DD_SC_NM = "해당없음",
            IsManual = true,
            SD_SCHUL_CODE = Settings.SchoolCode.Value,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };
        Items.Insert(0, new SchoolScheduleItemVM(s) { IsSelected = true, IsModified = true });
    }

    [RelayCommand]
    private async Task SaveSelectedAsync()
    {
        if (IsBusy) return;
        var targets = Items.Where(i => i.IsSelected && i.IsModified).ToList();
        if (targets.Count == 0)
        {
            StatusText = "선택된 변경 항목이 없습니다.";
            return;
        }
        IsBusy = true;
        try
        {
            using var repo = new SaemDesk.Repositories.SchoolScheduleRepository(SchoolDatabase.DbPath);
            int saved = 0;
            foreach (var v in targets)
            {
                var m = v.ToModel();
                if (m.No <= 0)
                    m.No = await repo.CreateAsync(m);
                else
                    await repo.UpdateAsync(m);
                v.IsModified = false;
                saved++;
            }
            StatusText = $"{saved}건 저장됨";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (IsBusy) return;
        var targets = Items.Where(i => i.IsSelected).ToList();
        if (targets.Count == 0) return;
        IsBusy = true;
        try
        {
            using var repo = new SaemDesk.Repositories.SchoolScheduleRepository(SchoolDatabase.DbPath);
            var ids = targets.Where(t => t.No > 0).Select(t => t.No).ToList();
            if (ids.Count > 0) await repo.DeleteBulkAsync(ids);
            foreach (var v in targets) Items.Remove(v);
            StatusText = $"{targets.Count}건 삭제";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (Items.Count == 0) { StatusText = "내보낼 항목이 없습니다."; return; }
        try
        {
            var path = await App.FilePicker.SaveFileAsync(
                $"학사일정_{SelectedYear}.xlsx", "xlsx");
            if (string.IsNullOrEmpty(path)) return;
            var rows = Items.Select(i => new Dictionary<string, object>
            {
                ["날짜"] = i.DisplayDate,
                ["요일"] = i.DisplayDayOfWeek,
                ["행사명"] = i.EventName,
                ["내용"] = i.EventContent,
                ["수업공제일"] = i.SbtrType,
                ["대상학년"] = string.Join(",", new[]
                {
                    i.G1?"1":null, i.G2?"2":null, i.G3?"3":null,
                    i.G4?"4":null, i.G5?"5":null, i.G6?"6":null,
                }.Where(s => s != null)!),
                ["구분"] = i.IsManual ? "수동" : "NEIS",
            }).Cast<object>().ToList();
            await MiniExcel.SaveAsAsync(path, rows, overwriteFile: true);
            StatusText = $"Excel 저장: {path}";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task UploadToGoogleAsync()
    {
        if (App.GoogleSync is null)
        {
            ErrorText = "Google Calendar 가 연결되지 않았습니다. (설정 → 일정 설정 → Google 연동)";
            return;
        }
        var targets = Items.Where(i => i.IsSelected).ToList();
        if (targets.Count == 0) targets = Items.ToList();
        if (targets.Count == 0) { StatusText = "업로드할 항목이 없습니다."; return; }

        IsBusy = true;
        StatusText = $"Google Calendar 업로드 중... ({targets.Count}건)";
        try
        {
            var schedules = targets.Select(t => t.ToModel()).ToList();
            var result = await App.GoogleSync.UploadSchoolSchedulesAsync(schedules);
            StatusText = $"Google 업로드 — 생성 {result.Created}, 오류 {result.Errors}";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    // 일괄 학년 적용용 임시 체크박스 상태
    [ObservableProperty] private bool _bulkG1;
    [ObservableProperty] private bool _bulkG2;
    [ObservableProperty] private bool _bulkG3;
    [ObservableProperty] private bool _bulkG4;
    [ObservableProperty] private bool _bulkG5;
    [ObservableProperty] private bool _bulkG6;

    [RelayCommand]
    private void ApplyGradesToSelected()
    {
        var targets = Items.Where(i => i.IsSelected).ToList();
        if (targets.Count == 0) { StatusText = "선택된 항목이 없습니다."; return; }
        foreach (var t in targets)
        {
            t.G1 = BulkG1; t.G2 = BulkG2; t.G3 = BulkG3;
            t.G4 = BulkG4; t.G5 = BulkG5; t.G6 = BulkG6;
            // OnG*Changed 가 IsModified=true 자동 셋
        }
        StatusText = $"{targets.Count}건 학년 일괄 적용 — 저장 버튼으로 DB 반영";
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        IsSelectAll = !IsSelectAll;
        foreach (var i in Items) i.IsSelected = IsSelectAll;
        OnPropertyChanged(nameof(SelectedCount));
    }
}

public partial class SchoolScheduleItemVM : ObservableObject
{
    private readonly SchoolSchedule _src;
    public int No => _src.No;

    public SchoolScheduleItemVM(SchoolSchedule s)
    {
        _src = s;
        _date = s.AA_YMD;
        _eventName = s.EVENT_NM;
        _eventContent = s.EVENT_CNTNT;
        _sbtrType = string.IsNullOrEmpty(s.SBTR_DD_SC_NM) ? "해당없음" : s.SBTR_DD_SC_NM;
        _g1 = s.ONE_GRADE_EVENT_YN;
        _g2 = s.TW_GRADE_EVENT_YN;
        _g3 = s.THREE_GRADE_EVENT_YN;
        _g4 = s.FR_GRADE_EVENT_YN;
        _g5 = s.FIV_GRADE_EVENT_YN;
        _g6 = s.SIX_GRADE_EVENT_YN;
        IsManual = s.IsManual;
    }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isModified;
    public bool IsManual { get; }
    public string ManualIcon => IsManual ? "✋" : "🛰";
    public string ModifiedIcon => IsModified ? "●" : string.Empty;

    [ObservableProperty] private DateTime _date;
    [ObservableProperty] private string _eventName = string.Empty;
    [ObservableProperty] private string _eventContent = string.Empty;
    [ObservableProperty] private string _sbtrType = "해당없음";
    [ObservableProperty] private bool _g1, _g2, _g3, _g4, _g5, _g6;

    public string DisplayDate => Date.ToString("yyyy-MM-dd");
    public string DisplayDayOfWeek => Date.ToString("ddd",
        System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));

    partial void OnEventNameChanged(string value) { IsModified = true; }
    partial void OnEventContentChanged(string value) { IsModified = true; }
    partial void OnSbtrTypeChanged(string value) { IsModified = true; }
    partial void OnDateChanged(DateTime value) { IsModified = true; }
    partial void OnG1Changed(bool value) { IsModified = true; }
    partial void OnG2Changed(bool value) { IsModified = true; }
    partial void OnG3Changed(bool value) { IsModified = true; }
    partial void OnG4Changed(bool value) { IsModified = true; }
    partial void OnG5Changed(bool value) { IsModified = true; }
    partial void OnG6Changed(bool value) { IsModified = true; }

    public SchoolSchedule ToModel()
    {
        _src.AA_YMD = Date;
        _src.EVENT_NM = EventName;
        _src.EVENT_CNTNT = EventContent;
        _src.SBTR_DD_SC_NM = SbtrType;
        _src.ONE_GRADE_EVENT_YN = G1;
        _src.TW_GRADE_EVENT_YN = G2;
        _src.THREE_GRADE_EVENT_YN = G3;
        _src.FR_GRADE_EVENT_YN = G4;
        _src.FIV_GRADE_EVENT_YN = G5;
        _src.SIX_GRADE_EVENT_YN = G6;
        _src.UpdatedAt = DateTime.Now;
        return _src;
    }
}
