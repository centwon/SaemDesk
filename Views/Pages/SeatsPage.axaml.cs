using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;
using SaemDesk.Views.Dialogs;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 자리 배정 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.PageSeats (WinUI3).
/// Canvas + PhotoCard 방식으로 구현.
/// </summary>
public partial class SeatsPage : UserControl, IDisposable
{
    private bool _disposed;

    // ────────────────────────────────────────────────────
    //  Fields
    // ────────────────────────────────────────────────────

    private ObservableCollection<StudentCardData> _students = new();
    private int _totalStudents;
    private int _totalSeats;
    private readonly List<PhotoCard> _cards = new();
    private int  _jjak = 1;
    private int  _jul  = 5;
    private int  _totalRows;
    private bool _isViewPhoto;
    private int  _grade;
    private int  _classRoom;
    private bool _isInitialized;
    private bool _suppressSeatCheck;
    private bool _isLoadingArrangement;

    private Enrollment? _selectedStudentFromList;

    private double _spaceJul;
    private double _spaceJjak;
    private double _spaceSide;
    private double _spaceRow;

    private readonly List<(string IdA, string IdB)> _exclusionPairs = new();
    private readonly List<(string IdA, string IdB)> _fixedPairs     = new();

    private SeatOptions _options = new();
    private int _savedRoundsCount;

    private SeatService? _seatSvc;

    // ────────────────────────────────────────────────────
    //  Constructor / Lifecycle
    // ────────────────────────────────────────────────────

    public SeatsPage()
    {
        InitializeComponent();

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _seatSvc = new SeatService();

        ClassFilter.ClassChanged    += OnFilterBarChanged;
        StudentList.StudentSelected += OnStudentSelected;

        _jul  = 5;
        _jjak = 1;
        UpdateDotPattern();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ClassFilter.ClassChanged    -= OnFilterBarChanged;
        StudentList.StudentSelected -= OnStudentSelected;
        DetachCardEvents();
        _seatSvc?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _seatSvc?.Dispose();
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────
    //  FilterBar
    // ────────────────────────────────────────────────────

    private async void OnFilterBarChanged(object? sender, ClassChangedEventArgs e)
    {
        _grade     = e.Grade;
        _classRoom = e.Class;

        _students.Clear();
        foreach (var en in e.Students)
            _students.Add(new StudentCardData
            {
                StudentID = en.StudentID,
                Name      = en.Name,
                Number    = en.Number,
                Grade     = en.Grade,
                Class     = en.Class,
                PhotoPath = en.Photo ?? "",
                Sex       = en.Sex   ?? "",
            });

        StudentList.LoadStudents(e.Students.OrderBy(r => r.Number).ToList());
        _totalStudents = _students.Count;

        await TryLoadSavedArrangementAsync();
    }

    // ────────────────────────────────────────────────────
    //  학생 선택 (명렬 클릭)
    // ────────────────────────────────────────────────────

    private void OnStudentSelected(object? sender, Enrollment e)
    {
        _selectedStudentFromList = e;
        SelectedStudentText.Text = $"{e.Number}. {e.Name} 선택됨 — 좌석을 클릭하여 배치하세요. 드래그도 가능합니다.";
        SelectedStudentBar.IsVisible = true;
    }

    private void BtnCloseInfoBar_Click(object? sender, RoutedEventArgs e)
    {
        SelectedStudentBar.IsVisible = false;
        _selectedStudentFromList = null;
    }

    // ────────────────────────────────────────────────────
    //  Canvas 크기 변경
    // ────────────────────────────────────────────────────

    private void Room_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isInitialized) InitSeats();
    }

    // ────────────────────────────────────────────────────
    //  좌석 초기화
    // ────────────────────────────────────────────────────

    private void InitSeats()
    {
        if (Room is null) return;

        _isViewPhoto = ChkViewPhoto.IsChecked == true;
        DetachCardEvents();
        Room.Children.Clear();
        _cards.Clear();

        _totalStudents = _students.Count;
        _spaceSide = 10;
        _spaceJul  = 20;
        _spaceJjak = 10;
        _spaceRow  = 20;

        _totalRows = (int)Math.Ceiling((double)_totalStudents / (_jjak * _jul));
        if (_totalRows == 0) _totalRows = 1;

        double roomW = Room.Bounds.Width;
        double roomH = Room.Bounds.Height;
        if (roomW <= 0 || roomH <= 0) return;

        double cardH = (roomH - _spaceRow - _spaceRow * _totalRows) / _totalRows;
        double cardW = (cardH - 2) / 5 * 3 + 2;

        if (_spaceSide * 2 + _spaceJjak * (_jjak * _jul - 1) + _spaceJul * (_jul - 1)
            + cardW * (_jjak * _jul) > roomW)
        {
            cardW = (roomW - _spaceSide * 2 - _spaceJjak * (_jjak * _jul - 1)
                     - _spaceJul * (_jul - 1)) / (_jjak * _jul);
            cardH = cardW / 3 * 5;
        }

        _spaceSide = (roomW - (_spaceJjak * (_jjak * _jul - 1)
                               + _spaceJul * (_jul - 1)
                               + cardW * (_jjak * _jul))) / 2;

        int idx = 0;
        for (int i = 0; i < _totalRows; i++)
        {
            for (int j = 0; j < _jjak * _jul; j++)
            {
                var card = new PhotoCard
                {
                    IsShowPhoto = _isViewPhoto,
                    No          = idx,
                    Row         = i,
                    Col         = j,
                    StudentData = null,
                    CardWidth   = cardW,
                };

                card.StudentChanged += Card_StudentChanged;
                card.UnUsedChanged  += Card_UnUsedChanged;
                card.FixedChanged   += Card_FixedChanged;

                DragDrop.SetAllowDrop(card, true);
                card.AddHandler(DragDrop.DragOverEvent, Card_DragOver, RoutingStrategies.Tunnel);
                card.AddHandler(DragDrop.DropEvent,     Card_Drop,     RoutingStrategies.Bubble);
                card.Clicked += Card_Clicked;

                double top  = roomH - _spaceRow * (i + 1) - cardH * (i + 1);
                double left = roomW - _spaceSide - (j + 1) * cardW
                              - _spaceJjak * j
                              - Math.Truncate((double)(j / _jjak)) * _spaceJul;

                Canvas.SetLeft(card, left);
                Canvas.SetTop(card,  top);

                _cards.Add(card);
                Room.Children.Add(card);
                idx++;
            }
        }

        _totalSeats = _cards.Count;
        TBTable.Text = $"{_grade}학년 {_classRoom}반";
        _isInitialized = true;

        if (!_suppressSeatCheck) CheckSeat();
    }

    // ────────────────────────────────────────────────────
    //  카드 클릭 — 클릭 기반 배치
    // ────────────────────────────────────────────────────

    private void Card_Clicked(object? sender, EventArgs e)
    {
        if (sender is not PhotoCard card) return;

        if (_selectedStudentFromList != null)
        {
            var student = _students.FirstOrDefault(s => s.StudentID == _selectedStudentFromList.StudentID);
            if (student != null)
            {
                // 이미 다른 카드에 배치돼 있으면 그 카드를 비움
                var prev = _cards.FirstOrDefault(c => c != card
                    && c.StudentData?.StudentID == student.StudentID);
                prev?.ReplaceStudent(null);

                card.StudentData = student;
                _selectedStudentFromList = null;
                SelectedStudentBar.IsVisible = false;
                UpdatePlacedStudents();
            }
        }
    }

    // ────────────────────────────────────────────────────
    //  PhotoCard 드래그-드롭
    // ────────────────────────────────────────────────────

    private void Card_DragOver(object? sender, DragEventArgs e)
    {
        if (PhotoCard.AcceptsDragData(e))
            e.DragEffects = DragDropEffects.Move;
        else
            e.DragEffects = DragDropEffects.None;
        // e.Handled = true 제거 — Drop 이벤트 차단 방지
    }

    private void Card_Drop(object? sender, DragEventArgs e)
    {
        if (sender is not PhotoCard target) return;

        // ── 경우 1: PhotoCard → PhotoCard (카드끼리 교환/이동) ──
        StudentCardData? dragged = null;
        if (e.DataTransfer.Contains(PhotoCard.StudentDragFormat))
            dragged = e.DataTransfer.TryGetValue(PhotoCard.StudentDragFormat);

        if (dragged is not null)
        {
            var source = _cards.FirstOrDefault(c =>
                c.StudentData?.StudentID == dragged.StudentID && c != target);

            if (target.StudentData != null && source != null)
            {
                var tmp = target.StudentData;
                target.ReplaceStudent(dragged);
                source.ReplaceStudent(tmp);
            }
            else if (source != null)
            {
                target.ReplaceStudent(dragged);
                source.ReplaceStudent(null);
            }

            e.DragEffects = DragDropEffects.Move;
            UpdatePlacedStudents();
            return;
        }

        // ── 경우 2: ListStudent(명렬) → PhotoCard ──
        Enrollment? enrollment = null;
        if (e.DataTransfer.Contains(PhotoCard.EnrollmentDragFormat))
            enrollment = e.DataTransfer.TryGetValue(PhotoCard.EnrollmentDragFormat);

        if (enrollment is not null)
        {
            var student = _students.FirstOrDefault(s => s.StudentID == enrollment.StudentID);
            if (student is null) { e.DragEffects = DragDropEffects.None; return; }

            // 이미 다른 카드에 배치돼 있으면 그 카드를 비움
            var prev = _cards.FirstOrDefault(c => c != target
                && c.StudentData?.StudentID == student.StudentID);
            prev?.ReplaceStudent(null);

            // 대상 카드에 이미 다른 학생이 있으면 그 학생은 명렬로 돌아감
            // (선택 해제는 UpdatePlacedStudents 에서 처리)
            target.StudentData = student;

            e.DragEffects = DragDropEffects.Move;
            UpdatePlacedStudents();
        }
    }

    // ────────────────────────────────────────────────────
    //  배치 학생 → 명렬 선택 동기화
    // ────────────────────────────────────────────────────

    /// <summary>
    /// 현재 _cards 에 배치된 학생 ID 를 수집해 ListStudent 의 선택 상태에 반영.
    /// 로딩 중에는 호출을 억제한다.
    /// </summary>
    private void UpdatePlacedStudents()
    {
        if (_isLoadingArrangement) return;

        var placedIds = _cards
            .Where(c => c.StudentData != null)
            .Select(c => c.StudentData!.StudentID);

        StudentList.SetSelectedStudents(placedIds);
    }

    // ────────────────────────────────────────────────────
    //  PhotoCard 이벤트
    // ────────────────────────────────────────────────────

    private void Card_StudentChanged(object? sender, StudentCardEventArgs e)
    {
        if (sender is not PhotoCard card || card.StudentData == null) return;

        // 중복 제거
        foreach (var c in _cards)
        {
            if (c == card) continue;
            if (c.StudentData?.StudentID == card.StudentData.StudentID)
            {
                c.ReplaceStudent(null);
                break;
            }
        }

        UpdatePlacedStudents();
    }

    private void Card_UnUsedChanged(object? sender, EventArgs e)
    {
        if (sender is not PhotoCard card) return;
        _totalSeats += card.IsUnUsed ? -1 : 1;
        if (!_suppressSeatCheck) CheckSeat();
    }

    private void Card_FixedChanged(object? sender, EventArgs e) { }

    private void DetachCardEvents()
    {
        foreach (var c in _cards)
        {
            c.StudentChanged -= Card_StudentChanged;
            c.UnUsedChanged  -= Card_UnUsedChanged;
            c.FixedChanged   -= Card_FixedChanged;
            c.RemoveHandler(DragDrop.DragOverEvent, Card_DragOver);
            c.RemoveHandler(DragDrop.DropEvent,     Card_Drop);
            c.Clicked -= Card_Clicked;
        }
    }

    // ────────────────────────────────────────────────────
    //  좌석 수 검증
    // ────────────────────────────────────────────────────

    private bool CheckSeat()
    {
        if (_totalStudents > _totalSeats)
        {
            _ = ShowInfoAsync($"학생수가 자리수보다 {_totalStudents - _totalSeats}개 많습니다.");
            return false;
        }
        if (_totalStudents < _totalSeats)
        {
            _ = ShowInfoAsync($"자리수가 학생수보다 {_totalSeats - _totalStudents}개 많습니다.");
            return false;
        }
        return true;
    }

    private async Task ShowInfoAsync(string msg)
    {
        if (TopLevel.GetTopLevel(this) is Window w)
        {
            var dlg = new ConfirmDialog("자리 배정", msg);
            await dlg.ShowDialog(w);
        }
    }

    // ────────────────────────────────────────────────────
    //  배치 저장/복원
    // ────────────────────────────────────────────────────

    private async Task TryLoadSavedArrangementAsync()
    {
        if (_seatSvc is null || _grade == 0 || _classRoom == 0) return;

        var saved = await _seatSvc.LoadAsync(
            Settings.SchoolCode.Value, Settings.WorkYear.Value, _grade, _classRoom);

        _options = await _seatSvc.LoadOptionsAsync(
            Settings.SchoolCode.Value, Settings.WorkYear.Value, _grade, _classRoom);
        _exclusionPairs.Clear();
        _exclusionPairs.AddRange(_options.ExclusionPairs.Select(p => (p.IdA, p.IdB)));
        _fixedPairs.Clear();
        _fixedPairs.AddRange(_options.FixedPairs.Select(p => (p.IdA, p.IdB)));

        if (saved is null)
        {
            _savedRoundsCount = 0;
            BtnLock.IsChecked = false;
            await Dispatcher.UIThread.InvokeAsync(InitSeats, DispatcherPriority.Background);
            return;
        }

        // ── 저장값으로 UI 동기화 (_isLoadingArrangement 로 UpdatePlacedStudents 억제) ──
        _isLoadingArrangement = true;
        try
        {
            _jul  = saved.Jul;
            _jjak = saved.Jjak;

            // NBoxJul/ChkJJak 변경 → ValueChanged 발화 → _jul/_jjak 재대입 방지를 위해
            // 플래그가 켜진 상태에서 UI 값 설정
            NBoxJul.Value      = _jul;
            ChkJJak.IsChecked  = _jjak == 2;
            _isViewPhoto       = saved.ShowPhoto;
            ChkViewPhoto.IsChecked = _isViewPhoto;
            TboxMessage.Text   = saved.Message ?? "";
            UpdateDotPattern();

            _suppressSeatCheck = true;
            try
            {
                await Dispatcher.UIThread.InvokeAsync(InitSeats, DispatcherPriority.Background);

                foreach (var a in saved.Assignments)
                {
                    var card = _cards.FirstOrDefault(c => c.Row == a.Row && c.Col == a.Col);
                    if (card is null) continue;
                    if (a.IsUnUsed) card.IsUnUsed = true;
                    if (a.IsHidden) card.IsHidden = true;
                    if (!string.IsNullOrEmpty(a.StudentID))
                    {
                        var s = _students.FirstOrDefault(x => x.StudentID == a.StudentID);
                        if (s is not null) card.ReplaceStudent(s);
                    }
                    if (a.IsFixed) card.IsFixed = true;
                }
            }
            finally { _suppressSeatCheck = false; }

            CheckSeat();
            BtnLock.IsChecked = saved.IsLocked;
            ApplyLockState(saved.IsLocked);
        }
        finally
        {
            _isLoadingArrangement = false;
        }

        // 로딩 완료 후 한 번만 동기화
        UpdatePlacedStudents();
    }

    // ────────────────────────────────────────────────────
    //  버튼 핸들러
    // ────────────────────────────────────────────────────

    private void NBoxJul_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoadingArrangement) return;
        if (e.NewValue is null) return;
        _jul = (int)e.NewValue;
        UpdateDotPattern();
    }

    private void ChkJJak_Click(object? sender, RoutedEventArgs e)
    {
        if (_isLoadingArrangement) return;
        _jjak = ChkJJak.IsChecked == true ? 2 : 1;
        UpdateDotPattern();
    }

    private void UpdateDotPattern()
    {
        if (TxtDotPattern is null || _jul == 0) return;
        TxtDotPattern.Text = string.Join("  ",
            Enumerable.Range(0, _jul).Select(_ => new string('●', _jjak)));
    }

    private void BtnInit_Click(object? sender, RoutedEventArgs e)
    {
        StudentList.ClearSelection();
        InitSeats();
    }

    private async void BtnArrange_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        await ArrangeSeatAsync();
    }

    private async Task ArrangeSeatAsync()
    {
        if (!CheckSeat()) return;
        if (BtnLock.IsChecked == true) return;

        foreach (var c in _cards)
        {
            if (c.IsFixed && c.StudentData is not null) continue;
            c.ReplaceStudent(null);
        }

        var rand = new Random();

        HashSet<(string, string)> recentPairs = new();
        Dictionary<string, HashSet<(int, int)>> recentPositions = new();
        if (_seatSvc is not null)
        {
            if (_options.RecentPairAvoidRounds > 0)
                recentPairs = await _seatSvc.GetRecentPairsAsync(
                    Settings.SchoolCode.Value, Settings.WorkYear.Value,
                    _grade, _classRoom, _options.RecentPairAvoidRounds);
            if (_options.RecentPositionAvoidRounds > 0)
                recentPositions = await _seatSvc.GetRecentPositionsAsync(
                    Settings.SchoolCode.Value, Settings.WorkYear.Value,
                    _grade, _classRoom, _options.RecentPositionAvoidRounds);
        }

        int attempts = _options.MaxAttempts > 0 ? _options.MaxAttempts : 500;

        for (int a = 0; a < attempts; a++)
        {
            if (a > 0)
                foreach (var c in _cards)
                    if (!c.IsFixed) c.ReplaceStudent(null);

            var placedIds = new HashSet<string>();

            foreach (var (idA, idB) in _fixedPairs)
            {
                var emptyNeighbors = GetEmptyNeighborPairs();
                if (!emptyNeighbors.Any()) continue;
                var (ca, cb) = emptyNeighbors[rand.Next(emptyNeighbors.Count)];
                var sA = _students.FirstOrDefault(s => s.StudentID == idA);
                var sB = _students.FirstOrDefault(s => s.StudentID == idB);
                if (sA is null || sB is null) continue;
                ca.ReplaceStudent(sA);
                cb.ReplaceStudent(sB);
                placedIds.Add(idA);
                placedIds.Add(idB);
            }

            foreach (var frontId in _options.FrontPriorityStudentIds)
            {
                if (placedIds.Contains(frontId)) continue;
                var student = _students.FirstOrDefault(s => s.StudentID == frontId);
                if (student is null) continue;

                int maxRow = _options.FrontPriorityMaxRow;
                var frontSeats = _cards
                    .Where(c => !c.IsFixed && !c.IsUnUsed && c.StudentData is null
                                && c.Row <= maxRow
                                && !IsForbiddenPos(frontId, c.Row, c.Col, recentPositions))
                    .ToList();
                if (!frontSeats.Any())
                    frontSeats = _cards
                        .Where(c => !c.IsFixed && !c.IsUnUsed && c.StudentData is null
                                    && c.Row <= maxRow).ToList();
                if (!frontSeats.Any()) continue;

                frontSeats[rand.Next(frontSeats.Count)].ReplaceStudent(student);
                placedIds.Add(frontId);
            }

            var remaining = _students
                .Where(s => !placedIds.Contains(s.StudentID)
                            && !_cards.Any(c => c.StudentData?.StudentID == s.StudentID))
                .OrderBy(_ => rand.Next()).ToList();

            foreach (var student in remaining)
            {
                var empty = _cards
                    .Where(c => !c.IsFixed && !c.IsUnUsed && c.StudentData is null)
                    .ToList();
                if (!empty.Any()) break;

                var filtered = empty
                    .Where(c => !IsForbiddenPos(student.StudentID, c.Row, c.Col, recentPositions))
                    .ToList();
                var pool = filtered.Any() ? filtered : empty;
                pool[rand.Next(pool.Count)].ReplaceStudent(student);
            }

            if (IsValidArrangement(recentPairs)) break;
        }

        await SeatAnimationAsync();
        await SeatAssignAsync();
        UpdatePlacedStudents();
    }

    private List<(PhotoCard A, PhotoCard B)> GetEmptyNeighborPairs()
    {
        var result = new List<(PhotoCard A, PhotoCard B)>();
        foreach (var a in _cards)
        {
            if (a.IsFixed || a.IsUnUsed || a.StudentData is not null) continue;
            foreach (var b in _cards)
            {
                if (b == a || b.IsFixed || b.IsUnUsed || b.StudentData is not null) continue;
                if (AreNeighbors(a, b) &&
                    string.Compare(a.StudentData?.StudentID ?? $"{a.Row}:{a.Col}",
                                   b.StudentData?.StudentID ?? $"{b.Row}:{b.Col}",
                                   StringComparison.Ordinal) < 0)
                    result.Add((a, b));
            }
        }
        return result;
    }

    private static bool IsForbiddenPos(
        string studentId, int row, int col,
        Dictionary<string, HashSet<(int, int)>> recent)
        => recent.TryGetValue(studentId, out var set) && set.Contains((row, col));

    private bool IsValidArrangement(HashSet<(string, string)> recentPairs)
    {
        foreach (var (idA, idB) in _exclusionPairs)
        {
            var cA = _cards.FirstOrDefault(c => c.StudentData?.StudentID == idA);
            var cB = _cards.FirstOrDefault(c => c.StudentData?.StudentID == idB);
            if (cA is not null && cB is not null && AreNeighbors(cA, cB))
                return false;
        }

        foreach (var (idA, idB) in _fixedPairs)
        {
            var cA = _cards.FirstOrDefault(c => c.StudentData?.StudentID == idA);
            var cB = _cards.FirstOrDefault(c => c.StudentData?.StudentID == idB);
            if (cA is not null && cB is not null && !AreNeighbors(cA, cB))
                return false;
        }

        if (_jjak < 2) return true;

        var pairs = new List<(PhotoCard A, PhotoCard B)>();
        foreach (var a in _cards)
        {
            if (a.StudentData is null) continue;
            foreach (var b in _cards)
            {
                if (b.StudentData is null || a == b) continue;
                if (!AreNeighbors(a, b)) continue;
                if (string.Compare(a.StudentData.StudentID, b.StudentData.StudentID,
                    StringComparison.Ordinal) >= 0) continue;
                pairs.Add((a, b));
            }
        }

        foreach (var (a, b) in pairs)
        {
            var ia  = a.StudentData!.StudentID;
            var ib  = b.StudentData!.StudentID;
            var key = string.Compare(ia, ib, StringComparison.Ordinal) < 0 ? (ia, ib) : (ib, ia);
            if (recentPairs.Contains(key)) return false;
        }

        if (_options.PreferMixedGenderPair)
            foreach (var (a, b) in pairs)
            {
                var sa = a.StudentData!.Sex ?? "";
                var sb = b.StudentData!.Sex ?? "";
                if (!string.IsNullOrEmpty(sa) && sa == sb) return false;
            }

        return true;
    }

    private bool AreNeighbors(PhotoCard a, PhotoCard b)
    {
        if (a.Row != b.Row) return false;
        return a.Col / _jjak == b.Col / _jjak && Math.Abs(a.Col - b.Col) == 1;
    }

    private async Task SeatAnimationAsync()
    {
        if (Room is null) return;
        var rand  = new Random();
        int speed = 30;
        double w  = Room.Bounds.Width;
        double h  = Room.Bounds.Height;

        for (int rep = 0; rep < 3; rep++)
        {
            foreach (var card in _cards)
            {
                Canvas.SetLeft(card, rand.Next(0, Math.Max(1, (int)(w - card.Bounds.Width))));
                Canvas.SetTop(card,  rand.Next(0, Math.Max(1, (int)(h - card.Bounds.Height))));
                await Task.Delay(speed);
            }
        }
    }

    private async Task SeatAssignAsync()
    {
        if (Room is null) return;
        double w = Room.Bounds.Width;
        double h = Room.Bounds.Height;

        foreach (var card in _cards)
        {
            double top  = h - _spaceRow * (card.Row + 1) - card.Bounds.Height * (card.Row + 1);
            double left = w - _spaceSide - (card.Col + 1) * card.Bounds.Width
                          - _spaceJjak * card.Col
                          - Math.Truncate((double)(card.Col / _jjak)) * _spaceJul;

            Canvas.SetLeft(card, left);
            Canvas.SetTop(card,  top);
            await Task.Delay(150);
        }
    }

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _seatSvc is null || _grade == 0 || _classRoom == 0) return;

        var arr = new SeatArrangement
        {
            SchoolCode = Settings.SchoolCode.Value,
            Year       = Settings.WorkYear.Value,
            Grade      = _grade,
            Class      = _classRoom,
            Jul        = _jul,
            Jjak       = _jjak,
            Rows       = _totalRows,
            ShowPhoto  = _isViewPhoto,
            Message    = TboxMessage.Text ?? "",
            IsLocked   = BtnLock.IsChecked == true,
        };

        foreach (var c in _cards)
            arr.Assignments.Add(new SeatAssignment
            {
                Row       = c.Row,
                Col       = c.Col,
                StudentID = c.StudentData?.StudentID,
                IsUnUsed  = c.IsUnUsed,
                IsHidden  = c.IsHidden,
                IsFixed   = c.IsFixed,
            });

        try
        {
            await _seatSvc.SaveAsync(arr, _options, _jjak);
            _savedRoundsCount++;
            SelectedStudentText.Text     = $"{_grade}학년 {_classRoom}반 좌석 배치가 저장되었습니다.";
            SelectedStudentBar.IsVisible = true;
        }
        catch (Exception ex) { Debug.WriteLine($"[SeatsPage] Save: {ex.Message}"); }
    }

    private void BtnLock_Click(object? sender, RoutedEventArgs e)
        => ApplyLockState(BtnLock.IsChecked == true);

    private void ApplyLockState(bool locked)
    {
        foreach (var c in _cards) DragDrop.SetAllowDrop(c, !locked);
        BtnArrange.IsEnabled = !locked;
    }

    private void ChkViewPhoto_Click(object? sender, RoutedEventArgs e)
    {
        _isViewPhoto = ChkViewPhoto.IsChecked == true;
        foreach (var c in _cards) c.IsShowPhoto = _isViewPhoto;
    }

    private async void BtnOptions_Click(object? sender, RoutedEventArgs e)
    {
        if (_students.Count == 0) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new SeatOptionsDialog(_students, _options, _savedRoundsCount);
        await dlg.ShowDialog(owner);

        _options = dlg.Result;
        _exclusionPairs.Clear();
        _exclusionPairs.AddRange(_options.ExclusionPairs.Select(p => (p.IdA, p.IdB)));
        _fixedPairs.Clear();
        _fixedPairs.AddRange(_options.FixedPairs.Select(p => (p.IdA, p.IdB)));
    }

    private async void BtnPrint_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _cards.Count == 0) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new SeatPrintOptionsDialog();
        await dlg.ShowDialog(owner);
        if (!dlg.IsSuccess) return;

        try
        {
            var svc = new SeatsPrintService();

            var cellData = _cards.Select(c => new SeatsPrintService.SeatCellData
            {
                Row         = c.Row,
                Col         = c.Col,
                IsHidden    = c.IsHidden,
                IsUnUsed    = c.IsUnUsed,
                IsFixed     = c.IsFixed,
                StudentData = c.StudentData,
            }).ToList();

            string pdf = svc.GenerateSeatsPdf(
                cellData, _grade, _classRoom, _jul, _jjak,
                TboxMessage.Text ?? "", _isViewPhoto,
                dlg.Orientation, dlg.IncludeRoster);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = pdf, UseShellExecute = true
            });
        }
        catch (Exception ex) { Debug.WriteLine($"[SeatsPage] Print: {ex.Message}"); }
    }
}
