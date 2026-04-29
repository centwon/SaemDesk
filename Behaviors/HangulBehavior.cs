using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SaemDesk.Behaviors;

/// <summary>
/// Avalonia TextBox에 UseHangul="True" 첨부 속성을 부여하면
/// 포커스 획득 시 자동으로 한글 IME 모드로 전환합니다.
///
/// 사용법 (XAML):
///   xmlns:b="using:SaemDesk.Behaviors"
///   &lt;TextBox b:HangulBehavior.UseHangul="True" /&gt;
/// </summary>
public static class HangulBehavior
{
    public static readonly AttachedProperty<bool> UseHangulProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "UseHangul",
            typeof(HangulBehavior),
            defaultValue: false);

    // ────────────────────────────────────────────────────
    //  CLR Getter / Setter
    // ────────────────────────────────────────────────────

    public static bool GetUseHangul(TextBox element)   => element.GetValue(UseHangulProperty);
    public static void SetUseHangul(TextBox element, bool value) => element.SetValue(UseHangulProperty, value);

    // ────────────────────────────────────────────────────
    //  정적 생성자 — 속성 변경 핸들러 등록
    // ────────────────────────────────────────────────────

    static HangulBehavior()
    {
        UseHangulProperty.Changed.AddClassHandler<TextBox>((textBox, e) =>
        {
            bool enable = e.NewValue is true;
            if (enable) textBox.GotFocus += OnGotFocus;
            else        textBox.GotFocus -= OnGotFocus;
        });
    }

    private static void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // KoreanImeService를 통해 한글 IME 전환 (App.KoreanIme는 Windows-only)
        try { App.KoreanIme.TryEnableHangul(); }
        catch { /* 비-Windows 환경에서는 no-op */ }
    }
}
