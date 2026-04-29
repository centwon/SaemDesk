using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 년도 + 12개 월 버튼 팝업으로 월을 선택하는 컨트롤.
/// WinUI3 MonthPicker (Flyout+Storyboard) → Avalonia Popup 기반 이식.
/// </summary>
public partial class MonthPicker : UserControl
{
    // ────────────────────────────────────────────────────
    //  StyledProperty
    // ────────────────────────────────────────────────────

    public static readonly StyledProperty<DateTime> SelectedMonthProperty =
        AvaloniaProperty.Register<MonthPicker, DateTime>(
            nameof(SelectedMonth),
            defaultValue: new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1));

    public DateTime SelectedMonth
    {
        get => GetValue(SelectedMonthProperty);
        set => SetValue(SelectedMonthProperty, value);
    }

    // ────────────────────────────────────────────────────
    //  이벤트
    // ────────────────────────────────────────────────────

    public event EventHandler<DateTime>? SelectedMonthChanged;

    // ────────────────────────────────────────────────────
    //  내부 필드
    // ────────────────────────────────────────────────────

    private int _displayYear;
    private List<Button>? _monthButtons;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public MonthPicker()
    {
        InitializeComponent();
        _displayYear = DateTime.Now.Year;

        SelectedMonthProperty.Changed.AddClassHandler<MonthPicker>((s, e) =>
        {
            if (e.NewValue is DateTime dt)
            {
                s.UpdateButtonText();
                s.UpdateMonthHighlight();
                s.SelectedMonthChanged?.Invoke(s, dt);
            }
        });

        // UserControl: named elements are available after InitializeComponent()
        CacheMonthButtons();
        UpdateButtonText();
        UpdateYearLabel();
        UpdateMonthHighlight();
    }

    // ────────────────────────────────────────────────────
    //  이벤트 핸들러
    // ────────────────────────────────────────────────────

    private void BtnMonth_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _displayYear = SelectedMonth.Year;
        UpdateYearLabel();
        UpdateMonthHighlight();
        MonthPopup.IsOpen = !MonthPopup.IsOpen;
    }

    private void BtnPrev_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _displayYear--;
        UpdateYearLabel();
        UpdateMonthHighlight();
    }

    private void BtnNext_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _displayYear++;
        UpdateYearLabel();
        UpdateMonthHighlight();
    }

    private void MonthBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int month))
        {
            SelectedMonth = new DateTime(_displayYear, month, 1);
            MonthPopup.IsOpen = false;
        }
    }

    // ────────────────────────────────────────────────────
    //  비주얼 업데이트
    // ────────────────────────────────────────────────────

    private void CacheMonthButtons()
    {
        _monthButtons =
        [
            M1, M2, M3, M4, M5, M6,
            M7, M8, M9, M10, M11, M12
        ];
    }

    private void UpdateButtonText()
    {
        if (BtnMonth is not null)
            BtnMonth.Content = SelectedMonth.ToString("yyyy년 M월");
    }

    private void UpdateYearLabel()
    {
        if (YearLabel is not null)
            YearLabel.Text = $"{_displayYear}년";
    }

    private void UpdateMonthHighlight()
    {
        if (_monthButtons is null) return;

        for (int i = 0; i < _monthButtons.Count; i++)
        {
            bool isSelected = _displayYear == SelectedMonth.Year && (i + 1) == SelectedMonth.Month;
            var btn = _monthButtons[i];
            btn.BorderBrush     = isSelected ? SolidColorBrush.Parse("#0078D4") : Brushes.Transparent;
            btn.FontWeight      = isSelected ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
            btn.Foreground      = isSelected ? SolidColorBrush.Parse("#0078D4") : null;
        }
    }
}
