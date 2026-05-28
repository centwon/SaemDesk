using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniExcelLibs;
using SaemDesk.Collections;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 진도 관리(매트릭스) 페이지 VM —
/// 수업 선택 → 단원 × 강의실(분반) 매트릭스, 셀 클릭 시 토글(Normal ↔ Completed).
/// </summary>
public partial class ProgressMatrixPageVM : ViewModelBase
{
    public OptimizedObservableCollection<Course> Courses { get; } = new();
    public OptimizedObservableCollection<MatrixRow> Rows { get; } = new();
    public OptimizedObservableCollection<string> Rooms { get; } = new();

    [ObservableProperty]
    private Course? _selectedCourse;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _errorText = string.Empty;

    public int TotalSections => Rows.Count;
    public string LeadingRoom { get; private set; } = "-";
    public int MaxGap { get; private set; }

    public ProgressMatrixPageVM()
    {
        _ = LoadCoursesAsync();
    }

    [RelayCommand]
    private async Task LoadCoursesAsync()
    {
        if (IsBusy) return;
        ErrorText = string.Empty;
        IsBusy = true;
        try
        {
            using var svc = new SaemDesk.Services.CourseService();
            var list = await svc.GetMyCoursesAsync();
            Courses.ReplaceAll(list);
            StatusText = $"수업 {Courses.Count}개";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnSelectedCourseChanged(Course? value)
    {
        if (value is null) return;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedCourse is null || IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            using var sectionRepo = new CourseSectionRepository(SchoolDatabase.DbPath);
            using var progressRepo = new LessonProgressRepository(SchoolDatabase.DbPath);

            var sections = await sectionRepo.GetByCourseAsync(SelectedCourse.No);
            var rooms = SelectedCourse.RoomList;
            var progress = await progressRepo.GetByCourseAsync(SelectedCourse.No);

            Rooms.ReplaceAll(rooms);

            var newRows = new List<MatrixRow>();
            int idx = 0;
            foreach (var sec in sections.OrderBy(s => s.SortOrder).ThenBy(s => s.No))
            {
                var row = new MatrixRow
                {
                    Index = ++idx,
                    SectionId = sec.No,
                    SectionName = $"{sec.UnitNo}-{sec.ChapterNo}-{sec.SectionNo} {sec.SectionName}",
                };
                foreach (var room in rooms)
                {
                    var p = progress.FirstOrDefault(x => x.CourseSectionId == sec.No && x.Room == room);
                    row.Cells.Add(new MatrixCell
                    {
                        SectionId = sec.No,
                        Room = room,
                        State = p is null
                            ? CellState.Empty
                            : p.IsCompleted ? CellStateEx.FromType(p.ProgressType) : CellState.Empty,
                    });
                }
                newRows.Add(row);
            }
            Rows.ReplaceAll(newRows);

            ComputeStats();
            StatusText = $"단원 {Rows.Count} × 분반 {Rooms.Count}";
            OnPropertyChanged(nameof(TotalSections));
            OnPropertyChanged(nameof(LeadingRoom));
            OnPropertyChanged(nameof(MaxGap));
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ComputeStats()
    {
        if (Rooms.Count == 0) { LeadingRoom = "-"; MaxGap = 0; return; }
        var counts = Rooms.Select(r => (Room: r,
            Done: Rows.Sum(row => row.Cells.FirstOrDefault(c => c.Room == r)?.IsDone == true ? 1 : 0))).ToList();
        var max = counts.Max(c => c.Done);
        var min = counts.Min(c => c.Done);
        MaxGap = max - min;
        LeadingRoom = counts.OrderByDescending(c => c.Done).First().Room;
    }

    [RelayCommand]
    private async Task AnalyzeGapAsync()
    {
        if (SelectedCourse is null) return;
        try
        {
            using var repo = new LessonProgressRepository(SchoolDatabase.DbPath);
            var gaps = await repo.GetProgressGapsAsync(SelectedCourse.No, Rooms.ToList());
            if (gaps.Count == 0) { StatusText = "격차 데이터 없음"; return; }
            var max = gaps.OrderByDescending(g => g.GapFromMax).First();
            StatusText = $"격차 분석: {max.Room} {max.CompletedCount}/{max.TotalCount} (최대 격차 {max.GapFromMax}, 평균과 차이 {max.GapFromAverage:F1})";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task SyncFromSchedulesAsync()
    {
        if (SelectedCourse is null || IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            using var progressRepo = new LessonProgressRepository(SchoolDatabase.DbPath);
            using var sectionRepo = new CourseSectionRepository(SchoolDatabase.DbPath);
            using var scheduleRepo = new ScheduleRepository(SchoolDatabase.DbPath);
            using var mapRepo = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            var svc = new SaemDesk.Services.ProgressSyncService(
                progressRepo, sectionRepo, scheduleRepo, mapRepo);

            int total = 0;
            foreach (var room in Rooms)
            {
                var r = await svc.SyncProgressFromSchedulesAsync(SelectedCourse.No, room);
                total += r.AffectedCount;
            }
            StatusText = $"일정 동기화 완료 — {total}건 업데이트";
            await RefreshAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (Rows.Count == 0) { StatusText = "내보낼 매트릭스가 없습니다."; return; }
        try
        {
            var path = await App.FilePicker.SaveFileAsync(
                $"진도매트릭스_{SelectedCourse?.Subject}.xlsx", "xlsx");
            if (string.IsNullOrEmpty(path)) return;

            var rows = new List<Dictionary<string, object>>();
            foreach (var row in Rows)
            {
                var dict = new Dictionary<string, object>
                {
                    ["순번"] = row.Index,
                    ["단원"] = row.SectionName,
                };
                foreach (var c in row.Cells)
                    dict[c.Room] = c.Glyph;
                rows.Add(dict);
            }
            await MiniExcel.SaveAsAsync(path, rows, overwriteFile: true);
            StatusText = $"Excel 저장: {path}";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private Task ToggleCellAsync(MatrixCell? cell) => ApplyCellAsync(cell, "toggle");

    [RelayCommand]
    private Task MarkMakeupAsync(MatrixCell? cell) => ApplyCellAsync(cell, "makeup");

    [RelayCommand]
    private Task MarkSkippedAsync(MatrixCell? cell) => ApplyCellAsync(cell, "skipped");

    [RelayCommand]
    private Task MarkMergedAsync(MatrixCell? cell) => ApplyCellAsync(cell, "merged");

    [RelayCommand]
    private Task MarkFailedAsync(MatrixCell? cell) => ApplyCellAsync(cell, "failed");

    [RelayCommand]
    private Task ClearCellAsync(MatrixCell? cell) => ApplyCellAsync(cell, "clear");

    private async Task ApplyCellAsync(MatrixCell? cell, string action)
    {
        if (cell is null || SelectedCourse is null) return;
        try
        {
            using var repo = new LessonProgressRepository(SchoolDatabase.DbPath);
            switch (action)
            {
                case "toggle":
                    if (cell.IsDone)
                    {
                        await repo.MarkAsIncompleteAsync(cell.SectionId, cell.Room);
                        cell.State = CellState.Empty;
                    }
                    else
                    {
                        await repo.MarkAsCompletedAsync(cell.SectionId, cell.Room);
                        cell.State = CellState.Done;
                    }
                    break;
                case "makeup":
                    await repo.MarkAsMakeupAsync(cell.SectionId, cell.Room, DateTime.Today);
                    cell.State = CellState.Makeup;
                    break;
                case "skipped":
                    await repo.MarkAsSkippedAsync(cell.SectionId, cell.Room);
                    cell.State = CellState.Skipped;
                    break;
                case "merged":
                    // 병합: Completed + ProgressType = Merged
                    var p = await repo.GetOrCreateAsync(cell.SectionId, cell.Room);
                    p.IsCompleted = true;
                    p.ProgressType = ProgressType.Merged;
                    p.CompletedDate = DateTime.Today;
                    p.UpdatedAt = DateTime.Now;
                    await repo.UpdateAsync(p);
                    cell.State = CellState.Merged;
                    break;
                case "failed":
                    var f = await repo.GetOrCreateAsync(cell.SectionId, cell.Room);
                    f.IsCompleted = false;
                    f.ProgressType = ProgressType.Normal;
                    f.UpdatedAt = DateTime.Now;
                    await repo.UpdateAsync(f);
                    cell.State = CellState.Failed;
                    break;
                case "clear":
                    await repo.MarkAsIncompleteAsync(cell.SectionId, cell.Room);
                    cell.State = CellState.Empty;
                    break;
            }
            ComputeStats();
            OnPropertyChanged(nameof(LeadingRoom));
            OnPropertyChanged(nameof(MaxGap));
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }
}

public partial class MatrixRow : ObservableObject
{
    public int Index { get; set; }
    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public ObservableCollection<MatrixCell> Cells { get; } = new();
}

public partial class MatrixCell : ObservableObject
{
    public int SectionId { get; set; }
    public string Room { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    [NotifyPropertyChangedFor(nameof(BgColor))]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    private CellState _state = CellState.Empty;

    public bool IsDone => State != CellState.Empty;
    public string Glyph => State switch
    {
        CellState.Done => "✓",
        CellState.Makeup => "M",
        CellState.Merged => "⊕",
        CellState.Skipped => "↪",
        CellState.Failed => "✕",
        _ => "",
    };
    public string BgColor => State switch
    {
        CellState.Done => "#4CAF50",
        CellState.Makeup => "#2196F3",
        CellState.Merged => "#9C27B0",
        CellState.Skipped => "#FF9800",
        CellState.Failed => "#F44336",
        _ => "#00000000",
    };
}

public enum CellState
{
    Empty, Done, Makeup, Merged, Skipped, Failed
}

public static class CellStateEx
{
    public static CellState FromType(ProgressType t) => t switch
    {
        ProgressType.Normal => CellState.Done,
        ProgressType.Makeup => CellState.Makeup,
        ProgressType.Merged => CellState.Merged,
        ProgressType.Skipped => CellState.Skipped,
        _ => CellState.Done,
    };
}
