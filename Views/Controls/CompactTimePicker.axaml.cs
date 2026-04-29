using System;
using Avalonia;
using Avalonia.Controls;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 시(0~23) / 분(0~59) NumericUpDown 쌍으로 구성된 컴팩트 시간 선택기.
/// WinUI3 CompactTimePicker(NumberBox 기반) → Avalonia NumericUpDown 기반으로 이식.
/// </summary>
public partial class CompactTimePicker : UserControl
{
    private bool _updating;

    // ────────────────────────────────────────────────────
    //  StyledProperty
    // ────────────────────────────────────────────────────

    public static readonly StyledProperty<TimeSpan> TimeProperty =
        AvaloniaProperty.Register<CompactTimePicker, TimeSpan>(
            nameof(Time),
            defaultValue: TimeSpan.Zero);

    public TimeSpan Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    // ────────────────────────────────────────────────────
    //  이벤트
    // ────────────────────────────────────────────────────

    public event EventHandler<TimeSpan>? TimeChanged;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public CompactTimePicker()
    {
        InitializeComponent();

        TimeProperty.Changed.AddClassHandler<CompactTimePicker>((s, _) => s.UpdateDisplay());

        // UserControl: named elements are available after InitializeComponent()
        UpdateDisplay();
    }

    // ────────────────────────────────────────────────────
    //  내부 로직
    // ────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        if (_updating || HourBox is null || MinuteBox is null) return;
        _updating = true;
        HourBox.Value   = Time.Hours;
        MinuteBox.Value = Time.Minutes;
        _updating = false;
    }

    private void HourBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_updating) return;
        int hour = e.NewValue is null ? 0 : Math.Clamp((int)e.NewValue.Value, 0, 23);
        _updating = true;
        Time = new TimeSpan(hour, Time.Minutes, 0);
        _updating = false;
        TimeChanged?.Invoke(this, Time);
    }

    private void MinuteBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_updating) return;
        int minute = e.NewValue is null ? 0 : Math.Clamp((int)e.NewValue.Value, 0, 59);
        _updating = true;
        Time = new TimeSpan(Time.Hours, minute, 0);
        _updating = false;
        TimeChanged?.Invoke(this, Time);
    }
}
