using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using SaemDesk.Models;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 짝 분리/고정 조건 설정 다이얼로그.
/// 원본: NewSchool.Dialogs.SeatExclusionDialog (WinUI3 ContentDialog).
/// </summary>
public partial class SeatExclusionDialog : Window
{
    public List<(string IdA, string IdB)> ExclusionPairs { get; }
    public List<(string IdA, string IdB)> FixedPairs     { get; }

    private readonly Dictionary<string, string> _nameLookup;

    private record StudentItem(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    public SeatExclusionDialog(
        IEnumerable<StudentCardData> students,
        List<(string IdA, string IdB)> exclusionPairs,
        List<(string IdA, string IdB)> fixedPairs)
    {
        InitializeComponent();

        ExclusionPairs = new List<(string, string)>(exclusionPairs);
        FixedPairs     = new List<(string, string)>(fixedPairs);

        var items = students
            .OrderBy(s => s.Number)
            .Select(s => new StudentItem(s.StudentID, $"{s.Name}({s.Number})"))
            .ToList();

        _nameLookup = items.ToDictionary(s => s.Id, s => s.Name);

        StudentABox.ItemsSource = items;
        StudentBBox.ItemsSource = items;

        RefreshList();
    }

    private void OnAddExclusion(object? sender, RoutedEventArgs e)
    {
        if (!TryGetPair(out var a, out var b) || IsDuplicate(a, b)) return;
        ExclusionPairs.Add((a, b));
        ClearSelection();
        RefreshList();
    }

    private void OnAddFixed(object? sender, RoutedEventArgs e)
    {
        if (!TryGetPair(out var a, out var b) || IsDuplicate(a, b)) return;
        FixedPairs.Add((a, b));
        ClearSelection();
        RefreshList();
    }

    private bool TryGetPair(out string idA, out string idB)
    {
        idA = idB = string.Empty;
        if (StudentABox.SelectedItem is not StudentItem a ||
            StudentBBox.SelectedItem is not StudentItem b) return false;
        if (a.Id == b.Id) return false;
        idA = a.Id; idB = b.Id;
        return true;
    }

    private bool IsDuplicate(string a, string b) =>
        ExclusionPairs.Any(p => (p.IdA == a && p.IdB == b) || (p.IdA == b && p.IdB == a)) ||
        FixedPairs.Any(p =>     (p.IdA == a && p.IdB == b) || (p.IdA == b && p.IdB == a));

    private void ClearSelection()
    {
        StudentABox.SelectedIndex = -1;
        StudentBBox.SelectedIndex = -1;
    }

    private void RefreshList()
    {
        PairListView.Items.Clear();
        for (int i = 0; i < ExclusionPairs.Count; i++)
        {
            var (a, b) = ExclusionPairs[i];
            PairListView.Items.Add(MakePairRow($"🚫  {GetName(a)}  ↔  {GetName(b)}", i, true));
        }
        for (int i = 0; i < FixedPairs.Count; i++)
        {
            var (a, b) = FixedPairs[i];
            PairListView.Items.Add(MakePairRow($"📌  {GetName(a)}  ↔  {GetName(b)}", i, false));
        }
        EmptyMessage.IsVisible = PairListView.Items.Count == 0;
    }

    private Grid MakePairRow(string text, int index, bool isExclusion)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        var tb   = new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        var btn  = new Button   { Content = "🗑", Padding = new Avalonia.Thickness(6, 2), Tag = index };
        btn.Click += isExclusion
            ? (s, _) => { if (s is Button b && b.Tag is int idx) { ExclusionPairs.RemoveAt(idx); RefreshList(); } }
            : (s, _) => { if (s is Button b && b.Tag is int idx) { FixedPairs.RemoveAt(idx);     RefreshList(); } };
        Grid.SetColumn(tb,  0);
        Grid.SetColumn(btn, 1);
        grid.Children.Add(tb);
        grid.Children.Add(btn);
        return grid;
    }

    private string GetName(string id) => _nameLookup.GetValueOrDefault(id, id);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
