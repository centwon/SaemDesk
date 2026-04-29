using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 정보 일괄 입력 미리보기 다이얼로그.
/// 원본: NewSchool.Dialogs.BulkStudentInfoPreviewDialog (WinUI3 ContentDialog).
/// </summary>
public partial class BulkStudentInfoPreviewDialog : Window
{
    public bool IsSuccess { get; private set; }

    public BulkStudentInfoPreviewDialog()
    {
        InitializeComponent();
    }

    // ────────────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────────────

    public void SetPreviewData(List<StudentImportPreviewItem> items)
    {
        int matched   = items.Count(i => i.IsMatched);
        int unmatched = items.Count(i => !i.IsMatched);
        int changed   = items.Count(i => i.IsMatched && i.Changes.Count > 0);
        int noChange  = items.Count(i => i.IsMatched && i.Changes.Count == 0);

        TxtSummary.Text = $"전체 {items.Count}명 중 매칭 {matched}명";
        if (changed  > 0) TxtSummary.Text += $" (변경 {changed}명)";
        if (noChange > 0) TxtSummary.Text += $" / 변경 없음 {noChange}명";
        if (unmatched > 0) TxtSummary.Text += $" / 미매칭 {unmatched}명";

        ApplyButton.IsEnabled = changed > 0;

        PreviewListView.Items.Clear();
        foreach (var item in items)
            PreviewListView.Items.Add(BuildRow(item));
    }

    // ────────────────────────────────────────────────────
    //  행 생성
    // ────────────────────────────────────────────────────

    private static Grid BuildRow(StudentImportPreviewItem item)
    {
        var grid = new Grid
        {
            Margin = new Avalonia.Thickness(4),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
        };

        string icon;
        IBrush  iconBrush;
        string  text;
        IBrush  textBrush;

        if (!item.IsMatched)
        {
            icon      = "⚠";
            iconBrush = Brushes.Red;
            text      = $"{item.Number}번 {item.Name} — 매칭 실패 (학급 명부에 없음)";
            textBrush = Brushes.Red;
        }
        else if (item.Changes.Count == 0)
        {
            icon      = "✓";
            iconBrush = Brushes.Gray;
            text      = $"{item.Number}번 {item.Name} — 변경 없음";
            textBrush = Brushes.Gray;
        }
        else
        {
            icon      = "✎";
            iconBrush = SolidColorBrush.Parse("#1E90FF");
            text      = $"{item.Number}번 {item.Name} — {string.Join(", ", item.Changes)}";
            textBrush = Brushes.Black;
        }

        var iconBlock = new TextBlock
        {
            Text              = icon,
            FontSize          = 14,
            Foreground        = iconBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var textBlock = new TextBlock
        {
            Text              = text,
            FontSize          = 12,
            Foreground        = textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping      = Avalonia.Media.TextWrapping.Wrap,
        };

        Grid.SetColumn(iconBlock, 0);
        Grid.SetColumn(textBlock, 1);
        grid.Children.Add(iconBlock);
        grid.Children.Add(textBlock);
        return grid;
    }

    // ────────────────────────────────────────────────────
    //  버튼
    // ────────────────────────────────────────────────────

    private void OnApply(object? sender, RoutedEventArgs e)
    {
        IsSuccess = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}

/// <summary>미리보기 아이템 데이터.</summary>
public class StudentImportPreviewItem
{
    public int    Number { get; set; }
    public string Name   { get; set; } = string.Empty;

    public string? MatchedStudentID { get; set; }
    public bool    IsMatched        => !string.IsNullOrEmpty(MatchedStudentID);

    public List<string> Changes { get; set; } = new();

    /// <summary>변경할 Student 필드 (fieldName → 새값)</summary>
    public Dictionary<string, string?> StudentFields { get; set; } = new();

    /// <summary>변경할 StudentDetail 필드 (fieldName → 새값)</summary>
    public Dictionary<string, string?> DetailFields  { get; set; } = new();
}
