using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 시(0~23) / 분(0~59) 인라인 시간 선택기.
/// 단일 테두리 안에 borderless 입력칸 2개 + 미니 ▲▼ 스테퍼.
/// 스테퍼·휠·↑↓키는 포커스된 칸(시 ↔ 분)에 작용하고, 직접 타이핑도 가능.
/// 공개 API(Time / TimeChanged)는 NumericUpDown 기반 구버전과 동일.
/// </summary>
public partial class CompactTimePicker : UserControl
{
    private bool _updating;
    private TextBox? _lastFocused;

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

    public event EventHandler<TimeSpan>? TimeChanged;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    // 속성 변경 핸들러는 정적 옵저버블 — 타입당 1회만 등록 (인스턴스 생성자에서 등록 금지)
    static CompactTimePicker()
    {
        TimeProperty.Changed.AddClassHandler<CompactTimePicker>((s, _) => s.UpdateDisplay());
    }

    public CompactTimePicker()
    {
        InitializeComponent();

        _lastFocused = HourBox;
        UpdateDisplay();
    }

    // ────────────────────────────────────────────────────
    //  표시 갱신
    // ────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        if (_updating || HourBox is null || MinuteBox is null) return;
        _updating = true;
        HourBox.Text   = Time.Hours.ToString("00");
        MinuteBox.Text = Time.Minutes.ToString("00");
        _updating = false;
    }

    private bool IsHour(object? box) => ReferenceEquals(box, HourBox);

    private int CurrentOf(bool isHour) => isHour ? Time.Hours : Time.Minutes;

    // 칸 값을 Time에 반영하고 표시·이벤트 갱신
    private void ApplySegment(bool isHour, int value)
    {
        _updating = true;
        Time = isHour ? new TimeSpan(value, Time.Minutes, 0)
                      : new TimeSpan(Time.Hours, value, 0);
        HourBox.Text   = Time.Hours.ToString("00");
        MinuteBox.Text = Time.Minutes.ToString("00");
        _updating = false;
        TimeChanged?.Invoke(this, Time);
    }

    // 입력 텍스트를 파싱·클램프해서 커밋 (실패 시 기존 값 유지)
    private void CommitBox(TextBox box)
    {
        bool isHour = IsHour(box);
        int max = isHour ? 23 : 59;
        int parsed = int.TryParse(box.Text, out var n) ? n : CurrentOf(isHour);
        ApplySegment(isHour, Math.Clamp(parsed, 0, max));
    }

    // 한 스텝 증감 (시 ±1 / 분 ±5, 범위 클램프)
    private void Step(TextBox? box, int dir)
    {
        box ??= HourBox;
        bool isHour = IsHour(box);
        int step = isHour ? 1 : 5;
        int max  = isHour ? 23 : 59;
        ApplySegment(isHour, Math.Clamp(CurrentOf(isHour) + dir * step, 0, max));
    }

    // ────────────────────────────────────────────────────
    //  이벤트
    // ────────────────────────────────────────────────────

    private void Segment_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            _lastFocused = tb;
            tb.SelectAll();
        }
    }

    private void Segment_Commit(object? sender, RoutedEventArgs e)
    {
        if (_updating || sender is not TextBox tb) return;
        CommitBox(tb);
    }

    private void Segment_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        switch (e.Key)
        {
            case Key.Up:    Step(tb, +1); e.Handled = true; break;
            case Key.Down:  Step(tb, -1); e.Handled = true; break;
            case Key.Enter: CommitBox(tb); tb.SelectAll(); e.Handled = true; break;
        }
    }

    private void Segment_Wheel(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not TextBox tb) return;
        Step(tb, e.Delta.Y >= 0 ? +1 : -1);
        e.Handled = true;
    }

    private void BtnUp_Click(object? sender, RoutedEventArgs e)   => Step(_lastFocused, +1);
    private void BtnDown_Click(object? sender, RoutedEventArgs e) => Step(_lastFocused, -1);
}
