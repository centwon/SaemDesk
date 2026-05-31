using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 동아리 부원 관리 다이얼로그.
/// 원본: NewSchool.Dialogs.ClubEnrollmentDialog (WinUI3 ContentDialog).
/// 좌측 학생 ↔ 우측 부원, 학년/반 필터, 이름 검색.
/// </summary>
public partial class ClubEnrollmentDialog : Window
{
    private readonly Club                    _club;
    private readonly List<Enrollment>        _all      = new();
    private readonly List<ClubEnrollment>    _original = new();
    private readonly HashSet<string>         _toAdd    = new();
    private readonly HashSet<string>         _toRemove = new();

    public bool IsSuccess { get; private set; }

    public ClubEnrollmentDialog(Club club)
    {
        InitializeComponent();
        _club = club;
        Title = $"부원 관리 — {club.ClubName}";

        InitClassFilter();
        Opened += async (_, _) => await LoadDataAsync();
    }

    // ────────────────────────────────────────────────────
    //  초기화
    // ────────────────────────────────────────────────────

    private void InitClassFilter()
    {
        TxtClubName.Text     = _club.ClubName;
        TxtActivityRoom.Text = string.IsNullOrEmpty(_club.ActivityRoom) ? "" : $"📍 {_club.ActivityRoom}";

        CBoxGradeFilter.SelectedIndex = 0;

        var classItems = new List<ComboBoxItem> { new ComboBoxItem { Content = "전체", Tag = "0" } };
        for (int i = 1; i <= 15; i++)
            classItems.Add(new ComboBoxItem { Content = $"{i}반", Tag = i.ToString() });
        CBoxClassFilter.ItemsSource    = classItems;
        CBoxClassFilter.SelectedIndex  = 0;
    }

    // ────────────────────────────────────────────────────
    //  데이터 로드
    // ────────────────────────────────────────────────────

    private async Task LoadDataAsync()
    {
        try
        {
            await LoadAllStudentsAsync();
            await LoadEnrollmentsAsync();
            RefreshLists();
        }
        catch (Exception ex) { Debug.WriteLine($"[ClubEnrollment] LoadData: {ex.Message}"); }
    }

    private async Task LoadAllStudentsAsync()
    {
        _all.Clear();
        using var svc = new EnrollmentService();
        for (int g = 1; g <= 3; g++)
        {
            for (int c = 1; c <= 15; c++)
            {
                try
                {
                    var roster = await svc.GetClassRosterAsync(
                        Settings.SchoolCode.Value, _club.Year, g, c);
                    _all.AddRange(roster);
                }
                catch { /* 해당 학급 없음 */ }
            }
        }
    }

    private async Task LoadEnrollmentsAsync()
    {
        _original.Clear();
        using var repo = new ClubEnrollmentRepository(SchoolDatabase.DbPath);
        var list = await repo.GetByClubAsync(_club.No);
        _original.AddRange(list);
    }

    // ────────────────────────────────────────────────────
    //  목록 갱신
    // ────────────────────────────────────────────────────

    private void RefreshLists()
    {
        var enrolledIds = _original.Select(e => e.StudentID)
                                    .Union(_toAdd)
                                    .Except(_toRemove)
                                    .ToHashSet();

        int    filterGrade = GetGrade();
        int    filterClass = GetClass();
        string search      = TxtSearch.Text?.Trim().ToLower() ?? "";

        var avail = _all
            .Where(s => !enrolledIds.Contains(s.StudentID))
            .Where(s => filterGrade == 0 || s.Grade == filterGrade)
            .Where(s => filterClass == 0 || s.Class == filterClass)
            .OrderBy(s => s.Grade).ThenBy(s => s.Class).ThenBy(s => s.Number).ToList();
        ListAvailable.LoadStudents(avail);

        var enrolled = _all
            .Where(s => enrolledIds.Contains(s.StudentID))
            .Where(s => string.IsNullOrEmpty(search) || s.Name.ToLower().Contains(search))
            .OrderBy(s => s.Grade).ThenBy(s => s.Class).ThenBy(s => s.Number).ToList();
        ListEnrolled.LoadStudents(enrolled);

        TxtAvailableCount.Text  = $"({avail.Count}명)";
        TxtRegisteredCount.Text = $"({enrolled.Count}명)";
        TxtEnrolledCount.Text   = $"부원: {enrolled.Count}명";
    }

    private int GetGrade()
    {
        if (CBoxGradeFilter.SelectedItem is ComboBoxItem i && i.Tag != null)
            return int.Parse(i.Tag.ToString()!);
        return 0;
    }

    private int GetClass()
    {
        if (CBoxClassFilter.SelectedItem is ComboBoxItem i && i.Tag != null)
            return int.Parse(i.Tag.ToString()!);
        return 0;
    }

    // ────────────────────────────────────────────────────
    //  필터 이벤트
    // ────────────────────────────────────────────────────

    private void CBoxGradeFilter_SelectionChanged(object? s, SelectionChangedEventArgs e) => RefreshLists();
    private void CBoxClassFilter_SelectionChanged(object? s, SelectionChangedEventArgs e) => RefreshLists();
    private void TxtSearch_TextChanged(object? s, TextChangedEventArgs e)                  => RefreshLists();

    // ────────────────────────────────────────────────────
    //  추가 / 제거
    // ────────────────────────────────────────────────────

    private void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var s in ListAvailable.GetSelectedStudents())
        {
            _toRemove.Remove(s.StudentID);
            if (!_original.Any(o => o.StudentID == s.StudentID))
                _toAdd.Add(s.StudentID);
        }
        ListAvailable.DeselectAll();
        RefreshLists();
    }

    private void BtnRemove_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var s in ListEnrolled.GetSelectedStudents())
        {
            _toAdd.Remove(s.StudentID);
            if (_original.Any(o => o.StudentID == s.StudentID))
                _toRemove.Add(s.StudentID);
        }
        ListEnrolled.DeselectAll();
        RefreshLists();
    }

    // ────────────────────────────────────────────────────
    //  저장 / 취소
    // ────────────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var repo = new ClubEnrollmentRepository(SchoolDatabase.DbPath);

            foreach (var id in _toAdd)
                await repo.CreateAsync(new ClubEnrollment
                {
                    StudentID = id,
                    ClubNo    = _club.No,
                    Status    = ClubEnrollmentStatus.Active,
                });

            foreach (var id in _toRemove)
            {
                var orig = _original.FirstOrDefault(o => o.StudentID == id);
                if (orig is not null) await repo.DeleteAsync(orig.No);
            }

            IsSuccess = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowInfo($"저장 오류: {ex.Message}", false);
            Debug.WriteLine($"[ClubEnrollment] Save: {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void ShowInfo(string msg, bool success)
    {
        StatusBar.Message  = msg;
        StatusBar.Severity = success
            ? SaemDesk.Views.Controls.InfoBarSeverity.Success
            : SaemDesk.Views.Controls.InfoBarSeverity.Warning;
        StatusBar.IsOpen   = true;
    }
}
