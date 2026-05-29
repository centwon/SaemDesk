using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

public partial class ClassTimetablePageVM : ViewModelBase
{
    private readonly Dictionary<(int day, int period), ClassTimetable> _slots = new();

    // ── 현재 필터 (기본값: 담임 학급) ──────────────────────
    private int _year     = Settings.WorkYear;
    private int _semester = Math.Max(1, Settings.WorkSemester);
    private int _grade    = Settings.HomeGrade;
    private int _classNum = Settings.HomeRoom;

    [ObservableProperty] private bool _isLoading;

    /// <summary>보기/편집 모드. 편집 모드에서만 셀 클릭·추가가 가능.</summary>
    [ObservableProperty] private bool _isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditCellInfo))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    private bool _isEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditCellInfo))]
    private int _editDay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditCellInfo))]
    private int _editPeriod;

    [ObservableProperty] private string _editSubject = "";
    [ObservableProperty] private string _editTeacher = "";

    public string EditCellInfo => EditDay > 0
        ? $"{DayName(EditDay)} {EditPeriod}교시"
        : "";

    public bool CanDelete => IsEditing && GetSlot(EditDay, EditPeriod) != null;

    public event Action? DataChanged;

    public ClassTimetable? GetSlot(int day, int period)
        => _slots.TryGetValue((day, period), out var item) ? item : null;

    // 편집 모드를 끄면 열려있던 편집 패널을 닫고 셀을 다시 그린다.
    partial void OnIsEditModeChanged(bool value)
    {
        if (!value) IsEditing = false;
        DataChanged?.Invoke();
    }

    /// <summary>필터(연도·학기·학년·반)를 지정하고 시간표를 로드.</summary>
    public async Task LoadAsync(int year, int semester, int grade, int classNum)
    {
        _year = year;
        _semester = semester;
        _grade = grade;
        _classNum = classNum;
        IsEditing = false;

        IsLoading = true;
        try
        {
            _slots.Clear();
            using var repo = new ClassTimetableRepository(SchoolDatabase.DbPath);
            var list = await repo.GetByClassAsync(
                Settings.SchoolCode.Value, year, semester, grade, classNum);

            foreach (var item in list)
                _slots[(item.DayOfWeek, item.Period)] = item;

            DataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClassTimetablePage] 로드 실패: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void BeginEdit(int day, int period)
    {
        if (!IsEditMode) return;

        EditDay = day;
        EditPeriod = period;
        var slot = GetSlot(day, period);
        EditSubject = slot?.SubjectName ?? "";
        EditTeacher = slot?.TeacherName ?? "";
        IsEditing = true;
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditSubject)) return;

        try
        {
            using var repo = new ClassTimetableRepository(SchoolDatabase.DbPath);
            var existing = GetSlot(EditDay, EditPeriod);

            if (existing != null)
            {
                existing.SubjectName = EditSubject.Trim();
                existing.TeacherName = EditTeacher.Trim();
                await repo.UpdateAsync(existing);
            }
            else
            {
                var item = new ClassTimetable
                {
                    SchoolCode = Settings.SchoolCode.Value,
                    Year = _year,
                    Semester = _semester,
                    Grade = _grade,
                    Class = _classNum,
                    DayOfWeek = EditDay,
                    Period = EditPeriod,
                    SubjectName = EditSubject.Trim(),
                    TeacherName = EditTeacher.Trim(),
                };
                await repo.CreateAsync(item);
            }

            IsEditing = false;
            await LoadAsync(_year, _semester, _grade, _classNum);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClassTimetablePage] 저장 실패: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteEditAsync()
    {
        var existing = GetSlot(EditDay, EditPeriod);
        if (existing == null) return;

        try
        {
            using var repo = new ClassTimetableRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(existing.No);
            _slots.Remove((EditDay, EditPeriod));
            IsEditing = false;
            DataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClassTimetablePage] 삭제 실패: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    private static string DayName(int day) => day switch
    {
        1 => "월요일", 2 => "화요일", 3 => "수요일",
        4 => "목요일", 5 => "금요일", _ => ""
    };
}
