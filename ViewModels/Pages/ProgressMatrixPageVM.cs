using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 진도 관리(매트릭스) 페이지 VM —
/// 수업 선택 → 단원 × 강의실(분반) 매트릭스, 셀 클릭 시 토글(Normal ↔ Completed).
/// </summary>
public partial class ProgressMatrixPageVM : ViewModelBase
{
    public ObservableCollection<Course> Courses { get; } = new();
    public ObservableCollection<MatrixRow> Rows { get; } = new();
    public ObservableCollection<string> Rooms { get; } = new();

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
            Courses.Clear();
            foreach (var c in list) Courses.Add(c);
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

            Rooms.Clear();
            foreach (var r in rooms) Rooms.Add(r);

            Rows.Clear();
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
                Rows.Add(row);
            }

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
    private async Task ToggleCellAsync(MatrixCell? cell)
    {
        if (cell is null || SelectedCourse is null) return;
        try
        {
            using var repo = new LessonProgressRepository(SchoolDatabase.DbPath);
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
