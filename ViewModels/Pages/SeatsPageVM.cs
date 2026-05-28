using System;
using System.Collections.Generic;
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
/// 자리 배정 페이지 ViewModel — NewSchool PageSeats MVP 포팅.
///
/// 한 반의 좌석 격자(Rows × Jul) 위에 학생을 배치한다.
/// - LoadAsync: 명렬 + 기존 SeatArrangement 로드
/// - 셀 클릭으로 배정/해제(드래그-드롭은 코드비하인드에서)
/// - 자동배정: 미배정 학생을 빈 셀에 무작위 배치
/// - SaveAsync: SeatService.SaveAsync 호출
/// </summary>
public partial class SeatsPageVM : ViewModelBase
{
    /// <summary>학생 ID → 학생 매핑(이름·번호 표시).</summary>
    public Dictionary<string, (string Name, int Number)> StudentMeta { get; } = new();

    /// <summary>현재 학급의 명렬 (번호순).</summary>
    public OptimizedObservableCollection<RosterEntry> Roster { get; } = new();

    /// <summary>좌석 격자 — Rows × Jul. 각 셀의 StudentID 가 빈 문자열이면 미배정.</summary>
    public SeatCell[,]? Grid { get; private set; }

    /// <summary>현재 활성 SeatArrangement(저장에 사용).</summary>
    public SeatArrangement Arrangement { get; private set; } = new();

    /// <summary>옵션(저장 직렬화용).</summary>
    public SeatOptions Options { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;
    [ObservableProperty] private decimal _jul = 5;
    [ObservableProperty] private bool    _jjak;
    [ObservableProperty] private string  _message = string.Empty;
    [ObservableProperty] private bool    _isLocked;

    public string ClassHeaderText
    {
        get
        {
            int g = Settings.HomeGrade.Value;
            int r = Settings.HomeRoom.Value;
            return (g <= 0 || r <= 0) ? "담임 학급 미설정" : $"{g}학년 {r}반";
        }
    }

    public bool IsEmpty => !IsLoading && Roster.Count == 0;

    public SeatsPageVM()
    {
        _ = LoadAsync();
    }

    /// <summary>학급 명렬 + 좌석 배치를 로드.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;
        StatusText = "로드 중…";
        try
        {
            string sc = Settings.SchoolCode.Value;
            int year  = Settings.WorkYear.Value;
            int grade = Settings.HomeGrade.Value;
            int room  = Settings.HomeRoom.Value;

            if (string.IsNullOrEmpty(sc) || year <= 0 || grade <= 0 || room <= 0)
            {
                ErrorText = "학교/학년도/담임 학급 설정을 먼저 완료해 주세요.";
                Roster.Clear();
                Grid = null;
                return;
            }

            // 1) 명렬
            using var enrollSvc = new EnrollmentService();
            var enrolls = await enrollSvc.GetClassRosterAsync(sc, year, grade, room);

            using var stuRepo = new StudentRepository(SchoolDatabase.DbPath);
            var ids = enrolls.Select(e => e.StudentID).ToList();
            var students = await stuRepo.GetByIdsAsync(ids);
            var byId = students.ToDictionary(s => s.StudentID, s => s);

            StudentMeta.Clear();
            var rosterItems = new List<RosterEntry>();
            foreach (var en in enrolls.OrderBy(x => x.Number))
            {
                if (byId.TryGetValue(en.StudentID, out var s))
                {
                    StudentMeta[en.StudentID] = (s.Name, en.Number);
                    rosterItems.Add(new RosterEntry(en.StudentID, en.Number, s.Name));
                }
            }
            Roster.ReplaceAll(rosterItems);

            // 2) 기존 좌석 배치
            using var seatSvc = new SeatService();
            var existing = await seatSvc.LoadAsync(sc, year, grade, room);

            if (existing is null)
            {
                Arrangement = new SeatArrangement
                {
                    SchoolCode = sc, Year = year, Grade = grade, Class = room,
                    Jul  = (int)Jul,
                    Jjak = Jjak ? 2 : 1,
                    Rows = Math.Max(1, (int)Math.Ceiling((double)Roster.Count / Math.Max(1, (int)Jul))),
                    ShowPhoto = false,
                    Message   = string.Empty,
                };
                Options = new SeatOptions();
            }
            else
            {
                Arrangement = existing;
                Jul     = existing.Jul;
                Jjak    = existing.Jjak == 2;
                Message = existing.Message;
                IsLocked = existing.IsLocked;
                Options  = await seatSvc.LoadOptionsAsync(sc, year, grade, room);
            }

            BuildGridFromArrangement();
            StatusText = $"{ClassHeaderText} — 학생 {Roster.Count}명 / 좌석 {Arrangement.Rows}×{Arrangement.Jul}";
        }
        catch (Exception ex)
        {
            ErrorText = $"로드 실패: {ex.Message}";
            Debug.WriteLine($"[SeatsPageVM] {ex}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>Arrangement.Assignments → Grid 2D 배열 빌드.</summary>
    private void BuildGridFromArrangement()
    {
        int rows = Math.Max(1, Arrangement.Rows);
        int cols = Math.Max(1, Arrangement.Jul);
        Grid = new SeatCell[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                Grid[r, c] = new SeatCell { Row = r, Col = c };

        foreach (var a in Arrangement.Assignments)
        {
            if (a.Row < 0 || a.Row >= rows || a.Col < 0 || a.Col >= cols) continue;
            Grid[a.Row, a.Col].StudentID = a.StudentID ?? string.Empty;
            Grid[a.Row, a.Col].IsUnused  = a.IsUnUsed;
            Grid[a.Row, a.Col].IsHidden  = a.IsHidden;
            Grid[a.Row, a.Col].IsFixed   = a.IsFixed;
        }
    }

    /// <summary>Grid + 메타 → Arrangement.Assignments 로 다시 합쳐서 저장.</summary>
    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Grid is null) return;
        try
        {
            Arrangement.Jul     = (int)Jul;
            Arrangement.Jjak    = Jjak ? 2 : 1;
            Arrangement.Rows    = Grid.GetLength(0);
            Arrangement.Message = Message ?? string.Empty;
            Arrangement.IsLocked = IsLocked;

            Arrangement.Assignments.Clear();
            int rows = Grid.GetLength(0);
            int cols = Grid.GetLength(1);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var cell = Grid[r, c];
                    Arrangement.Assignments.Add(new SeatAssignment
                    {
                        ArrangementNo = Arrangement.No,
                        Row = r, Col = c,
                        StudentID = string.IsNullOrEmpty(cell.StudentID) ? null : cell.StudentID,
                        IsUnUsed  = cell.IsUnused,
                        IsHidden  = cell.IsHidden,
                        IsFixed   = cell.IsFixed,
                    });
                }

            using var seatSvc = new SeatService();
            int no = await seatSvc.SaveAsync(Arrangement, Options, Arrangement.Jjak);
            Arrangement.No = no;
            StatusText = $"저장 완료 — {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ErrorText = $"저장 실패: {ex.Message}";
        }
    }

    /// <summary>좌석 초기화 — 모든 학생 해제(미배정 풀로 복귀).</summary>
    [RelayCommand]
    public void Clear()
    {
        if (Grid is null || IsLocked) return;
        int rows = Grid.GetLength(0);
        int cols = Grid.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                Grid[r, c].StudentID = string.Empty;
        StatusText = "초기화 완료 (저장 전).";
    }

    /// <summary>자동배정 — 미배정 학생을 빈 셀에 무작위 배치.</summary>
    [RelayCommand]
    public void AutoArrange()
    {
        if (Grid is null || IsLocked) return;

        // 현재 어떤 학생이 어디에 배정돼 있는지 수집
        var placed = new HashSet<string>();
        int rows = Grid.GetLength(0);
        int cols = Grid.GetLength(1);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!string.IsNullOrEmpty(Grid[r, c].StudentID))
                    placed.Add(Grid[r, c].StudentID);

        var unplaced = Roster.Select(x => x.StudentID).Where(id => !placed.Contains(id)).ToList();
        var rand = new Random();
        for (int i = unplaced.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (unplaced[i], unplaced[j]) = (unplaced[j], unplaced[i]);
        }

        // 빈 셀 채우기
        int idx = 0;
        for (int r = 0; r < rows && idx < unplaced.Count; r++)
            for (int c = 0; c < cols && idx < unplaced.Count; c++)
            {
                if (Grid[r, c].IsUnused || Grid[r, c].IsFixed) continue;
                if (string.IsNullOrEmpty(Grid[r, c].StudentID))
                    Grid[r, c].StudentID = unplaced[idx++];
            }
        StatusText = $"자동배정 완료 ({unplaced.Count}명)";
    }

    /// <summary>줄(Jul) 변경 시 격자 폭 재계산.</summary>
    partial void OnJulChanged(decimal value)
    {
        if (Roster.Count == 0) return;
        Arrangement.Jul = Math.Max(1, (int)value);
        Arrangement.Rows = Math.Max(1, (int)Math.Ceiling((double)Roster.Count / Arrangement.Jul));
        BuildGridFromArrangement();
        OnPropertyChanged(nameof(Grid));
    }

    /// <summary>두 좌석의 학생을 서로 교환(드래그-드롭에서 사용).</summary>
    public void SwapSeats(int r1, int c1, int r2, int c2)
    {
        if (Grid is null || IsLocked) return;
        if (r1 == r2 && c1 == c2) return;
        if (r1 < 0 || r2 < 0 || c1 < 0 || c2 < 0) return;
        if (r1 >= Grid.GetLength(0) || r2 >= Grid.GetLength(0)) return;
        if (c1 >= Grid.GetLength(1) || c2 >= Grid.GetLength(1)) return;
        (Grid[r1, c1].StudentID, Grid[r2, c2].StudentID) = (Grid[r2, c2].StudentID, Grid[r1, c1].StudentID);
    }

    /// <summary>학생 풀에서 좌석으로 직접 배정. 대상 좌석에 다른 학생이 있으면 그 학생은 명렬로 돌아감.</summary>
    public void AssignStudentToSeat(string studentId, int row, int col)
    {
        if (Grid is null || IsLocked || string.IsNullOrEmpty(studentId)) return;
        if (row < 0 || col < 0 || row >= Grid.GetLength(0) || col >= Grid.GetLength(1)) return;

        // 이미 그 학생이 다른 좌석에 있으면 거기를 비운다(이동)
        for (int r = 0; r < Grid.GetLength(0); r++)
            for (int c = 0; c < Grid.GetLength(1); c++)
                if (Grid[r, c].StudentID == studentId)
                    Grid[r, c].StudentID = string.Empty;
        Grid[row, col].StudentID = studentId;
    }

    /// <summary>특정 좌석을 비움.</summary>
    public void ClearSeat(int row, int col)
    {
        if (Grid is null || IsLocked) return;
        if (row < 0 || col < 0 || row >= Grid.GetLength(0) || col >= Grid.GetLength(1)) return;
        Grid[row, col].StudentID = string.Empty;
    }

    // ────────────────────────────────────────────────────
    //  자동배정 — 최근 짝·위치 이력 회피
    // ────────────────────────────────────────────────────

    /// <summary>이력을 고려한 자동배정. 여러 시도 후 패널티 점수가 가장 낮은 배치 채택.</summary>
    [RelayCommand]
    public async Task SmartArrangeAsync()
    {
        if (Grid is null || IsLocked || Roster.Count == 0) return;

        try
        {
            string sc = Settings.SchoolCode.Value;
            int year  = Settings.WorkYear.Value;
            int grade = Settings.HomeGrade.Value;
            int room  = Settings.HomeRoom.Value;

            using var seatSvc = new SeatService();
            // 최근 5라운드의 짝 + 위치 이력 회피
            var pairs   = await seatSvc.GetRecentPairsAsync(sc, year, grade, room, 5);
            var posDict = await seatSvc.GetRecentPositionsAsync(sc, year, grade, room, 5);

            int rows = Grid.GetLength(0);
            int cols = Grid.GetLength(1);

            // 빈 좌석(IsUnused/IsFixed 제외)에 들어갈 학생 후보
            var fixedIds = new HashSet<string>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (Grid[r, c].IsFixed && !string.IsNullOrEmpty(Grid[r, c].StudentID))
                        fixedIds.Add(Grid[r, c].StudentID);

            var candidates = Roster
                .Select(x => x.StudentID)
                .Where(id => !fixedIds.Contains(id))
                .ToList();

            int jjak = Jjak ? 2 : 1;
            int bestScore = int.MaxValue;
            string?[,]? bestLayout = null;
            var rand = new Random();

            const int Attempts = 80;
            for (int n = 0; n < Attempts; n++)
            {
                // 셔플
                var pool = candidates.OrderBy(_ => rand.Next()).ToList();

                // 빈 셀에 채우기
                var layout = new string?[rows, cols];
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        layout[r, c] = Grid[r, c].IsFixed ? Grid[r, c].StudentID : null;

                int idx = 0;
                for (int r = 0; r < rows && idx < pool.Count; r++)
                    for (int c = 0; c < cols && idx < pool.Count; c++)
                    {
                        if (Grid[r, c].IsUnused || Grid[r, c].IsFixed) continue;
                        layout[r, c] = pool[idx++];
                    }

                int score = ScoreLayout(layout, jjak, pairs, posDict, rows, cols);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestLayout = layout;
                    if (score == 0) break;
                }
            }

            if (bestLayout is null)
            {
                AutoArrange(); // fallback
                return;
            }

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    Grid[r, c].StudentID = bestLayout[r, c] ?? string.Empty;

            StatusText = $"자동배정 완료 — 패널티 {bestScore} (시도 {Attempts}회 중 최선)";
        }
        catch (Exception ex)
        {
            ErrorText = $"자동배정 실패: {ex.Message}";
            Debug.WriteLine($"[SeatsPageVM.SmartArrange] {ex}");
        }
    }

    /// <summary>배치 패널티 점수 — 낮을수록 좋음.</summary>
    private static int ScoreLayout(
        string?[,] layout, int jjak,
        HashSet<(string, string)> recentPairs,
        Dictionary<string, HashSet<(int Row, int Col)>> recentPos,
        int rows, int cols)
    {
        int score = 0;

        // 1) 최근 짝과 다시 짝 = +5
        if (jjak == 2)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c + 1 < cols; c += 2)
                {
                    var a = layout[r, c]; var b = layout[r, c + 1];
                    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) continue;
                    if (recentPairs.Contains((a, b)) || recentPairs.Contains((b, a)))
                        score += 5;
                }
        }
        else
        {
            // 좌우 인접도 약하게 패널티
            for (int r = 0; r < rows; r++)
                for (int c = 0; c + 1 < cols; c++)
                {
                    var a = layout[r, c]; var b = layout[r, c + 1];
                    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) continue;
                    if (recentPairs.Contains((a, b)) || recentPairs.Contains((b, a)))
                        score += 2;
                }
        }

        // 2) 최근 위치와 동일 = +1
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var id = layout[r, c];
                if (string.IsNullOrEmpty(id)) continue;
                if (recentPos.TryGetValue(id, out var set) && set.Contains((r, c)))
                    score += 1;
            }
        return score;
    }
}

/// <summary>좌석 한 칸 — 화면 표시·편집용.</summary>
public sealed class SeatCell
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string StudentID { get; set; } = string.Empty;
    public bool IsUnused { get; set; }
    public bool IsHidden { get; set; }
    public bool IsFixed  { get; set; }
}

/// <summary>학생 풀 항목.</summary>
public sealed record RosterEntry(string StudentID, int Number, string Name);
