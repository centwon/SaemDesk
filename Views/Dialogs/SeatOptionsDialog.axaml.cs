using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using SaemDesk.Models;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 좌석 배치 옵션 다이얼로그 (탭 구조).
/// 원본: NewSchool.Dialogs.SeatOptionsDialog (WinUI3 ContentDialog + Pivot).
/// Pivot → TabControl 로 교체.
/// </summary>
public partial class SeatOptionsDialog : Window
{
    public SeatOptions Result { get; private set; }

    private readonly Dictionary<string, string> _nameLookup;
    private readonly List<SeatOptions.PairRule> _exclusionPairs = new();
    private readonly List<SeatOptions.PairRule> _fixedPairs     = new();
    private readonly List<string>               _frontIds        = new();

    private record StudentItem(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    public SeatOptionsDialog(
        IEnumerable<StudentCardData> students,
        SeatOptions initial,
        int savedRoundsCount)
    {
        InitializeComponent();

        var studentList = students.OrderBy(s => s.Number).ToList();
        _nameLookup = studentList.ToDictionary(s => s.StudentID, s => $"{s.Name}({s.Number})");

        Result = CloneOptions(initial);

        // UI 초기값 반영
        ChkRecentPair.IsChecked  = Result.RecentPairAvoidRounds     > 0;
        NbxRecentPair.Value      = Result.RecentPairAvoidRounds     > 0 ? Result.RecentPairAvoidRounds     : 1;
        ChkRecentPos.IsChecked   = Result.RecentPositionAvoidRounds > 0;
        NbxRecentPos.Value       = Result.RecentPositionAvoidRounds > 0 ? Result.RecentPositionAvoidRounds : 1;
        ChkMixedGender.IsChecked = Result.PreferMixedGenderPair;
        NbxFrontMaxRow.Value     = Result.FrontPriorityMaxRow;

        _frontIds.AddRange(Result.FrontPriorityStudentIds);
        _exclusionPairs.AddRange(Result.ExclusionPairs.Select(p => new SeatOptions.PairRule { IdA = p.IdA, IdB = p.IdB }));
        _fixedPairs.AddRange(Result.FixedPairs.Select(p => new SeatOptions.PairRule { IdA = p.IdA, IdB = p.IdB }));

        // 시도 횟수
        int attempts = Result.MaxAttempts <= 0 ? 500 : Result.MaxAttempts;
        foreach (var obj in CbxAttempts.Items)
        {
            if (obj is ComboBoxItem ci && ci.Tag?.ToString() == attempts.ToString())
            {
                CbxAttempts.SelectedItem = ci;
                break;
            }
        }
        if (CbxAttempts.SelectedIndex < 0) CbxAttempts.SelectedIndex = 1; // 500 기본

        // 학생 콤보박스
        var items = studentList.Select(s => new StudentItem(s.StudentID, _nameLookup[s.StudentID])).ToList();
        CbxPairA.ItemsSource     = items;
        CbxPairB.ItemsSource     = items;
        CbxFrontStudent.ItemsSource = items;

        HistoryInfo.Text = savedRoundsCount > 0
            ? $"누적된 배치 회차: {savedRoundsCount}회"
            : "아직 저장된 배치가 없습니다. 첫 저장 이후부터 이력 기반 옵션이 활성화됩니다.";

        RefreshPairList();
        RefreshFrontList();
    }

    private static SeatOptions CloneOptions(SeatOptions src) => new()
    {
        RecentPairAvoidRounds      = src.RecentPairAvoidRounds,
        RecentPositionAvoidRounds  = src.RecentPositionAvoidRounds,
        PreferMixedGenderPair      = src.PreferMixedGenderPair,
        FrontPriorityStudentIds    = new List<string>(src.FrontPriorityStudentIds),
        FrontPriorityMaxRow        = src.FrontPriorityMaxRow,
        ExclusionPairs             = src.ExclusionPairs.Select(p => new SeatOptions.PairRule { IdA = p.IdA, IdB = p.IdB }).ToList(),
        FixedPairs                 = src.FixedPairs.Select(p =>     new SeatOptions.PairRule { IdA = p.IdA, IdB = p.IdB }).ToList(),
        MaxAttempts                = src.MaxAttempts <= 0 ? 500 : src.MaxAttempts,
    };

    // ────────────────────────────────────────────────────
    //  짝 제약
    // ────────────────────────────────────────────────────

    private void OnAddExclusion(object? sender, RoutedEventArgs e)
    {
        if (!TryGetPair(out var a, out var b) || IsDup(a, b)) return;
        _exclusionPairs.Add(new SeatOptions.PairRule { IdA = a, IdB = b });
        ClearPairSelection(); RefreshPairList();
    }

    private void OnAddFixed(object? sender, RoutedEventArgs e)
    {
        if (!TryGetPair(out var a, out var b) || IsDup(a, b)) return;
        _fixedPairs.Add(new SeatOptions.PairRule { IdA = a, IdB = b });
        ClearPairSelection(); RefreshPairList();
    }

    private bool TryGetPair(out string a, out string b)
    {
        a = b = string.Empty;
        if (CbxPairA.SelectedItem is not StudentItem sa ||
            CbxPairB.SelectedItem is not StudentItem sb) return false;
        if (sa.Id == sb.Id) return false;
        a = sa.Id; b = sb.Id; return true;
    }

    private bool IsDup(string a, string b)
    {
        bool eq(SeatOptions.PairRule p) =>
            (p.IdA == a && p.IdB == b) || (p.IdA == b && p.IdB == a);
        return _exclusionPairs.Any(eq) || _fixedPairs.Any(eq);
    }

    private void ClearPairSelection()
    {
        CbxPairA.SelectedIndex = -1;
        CbxPairB.SelectedIndex = -1;
    }

    private void RefreshPairList()
    {
        PairListView.Items.Clear();
        for (int i = 0; i < _exclusionPairs.Count; i++)
        {
            var p = _exclusionPairs[i];
            PairListView.Items.Add(MakeRow($"🚫  {GetName(p.IdA)}  ↔  {GetName(p.IdB)}", i, true));
        }
        for (int i = 0; i < _fixedPairs.Count; i++)
        {
            var p = _fixedPairs[i];
            PairListView.Items.Add(MakeRow($"📌  {GetName(p.IdA)}  ↔  {GetName(p.IdB)}", i, false));
        }
        PairEmpty.IsVisible = PairListView.Items.Count == 0;
    }

    private Grid MakeRow(string text, int index, bool isExclusion)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        var tb   = new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        var btn  = new Button   { Content = "🗑", Padding = new Avalonia.Thickness(6, 2), Tag = index };
        btn.Click += isExclusion
            ? (s, _) => { if (s is Button b && b.Tag is int i) { _exclusionPairs.RemoveAt(i); RefreshPairList(); } }
            : (s, _) => { if (s is Button b && b.Tag is int i) { _fixedPairs.RemoveAt(i);     RefreshPairList(); } };
        Grid.SetColumn(tb, 0); Grid.SetColumn(btn, 1);
        grid.Children.Add(tb); grid.Children.Add(btn);
        return grid;
    }

    // ────────────────────────────────────────────────────
    //  앞자리 우선
    // ────────────────────────────────────────────────────

    private void OnAddFront(object? sender, RoutedEventArgs e)
    {
        if (CbxFrontStudent.SelectedItem is not StudentItem s) return;
        if (_frontIds.Contains(s.Id)) return;
        _frontIds.Add(s.Id);
        CbxFrontStudent.SelectedIndex = -1;
        RefreshFrontList();
    }

    private void RefreshFrontList()
    {
        FrontListView.Items.Clear();
        for (int i = 0; i < _frontIds.Count; i++)
        {
            var id   = _frontIds[i];
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            var tb   = new TextBlock { Text = "🪑 " + GetName(id), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            var btn  = new Button   { Content = "🗑", Padding = new Avalonia.Thickness(6, 2), Tag = i };
            int idx  = i;
            btn.Click += (_, _) => { _frontIds.RemoveAt(idx); RefreshFrontList(); };
            Grid.SetColumn(tb, 0); Grid.SetColumn(btn, 1);
            grid.Children.Add(tb); grid.Children.Add(btn);
            FrontListView.Items.Add(grid);
        }
    }

    // ────────────────────────────────────────────────────
    //  확인 / 취소
    // ────────────────────────────────────────────────────

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Result.RecentPairAvoidRounds      = ChkRecentPair.IsChecked == true ? (int)(NbxRecentPair.Value ?? 1) : 0;
        Result.RecentPositionAvoidRounds  = ChkRecentPos.IsChecked  == true ? (int)(NbxRecentPos.Value  ?? 1) : 0;
        Result.PreferMixedGenderPair      = ChkMixedGender.IsChecked == true;
        Result.FrontPriorityMaxRow        = (int)(NbxFrontMaxRow.Value ?? 1);
        Result.FrontPriorityStudentIds    = new List<string>(_frontIds);
        Result.ExclusionPairs             = new List<SeatOptions.PairRule>(_exclusionPairs);
        Result.FixedPairs                 = new List<SeatOptions.PairRule>(_fixedPairs);

        if (CbxAttempts.SelectedItem is ComboBoxItem ci && ci.Tag?.ToString() is string s && int.TryParse(s, out var v))
            Result.MaxAttempts = v;
        else
            Result.MaxAttempts = 500;

        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private string GetName(string id) => _nameLookup.GetValueOrDefault(id, id);
}
