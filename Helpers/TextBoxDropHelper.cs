using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SaemDesk.Models;
using SaemDesk.Views.Controls;

namespace SaemDesk.Helpers;

/// <summary>
/// Avalonia TextBox 에 드래그앤드롭 텍스트 수신 기능을 추가하는 스태틱 헬퍼.
/// NewSchool.Helpers.TextBoxDropHelper(WinUI3) 를 Avalonia 12 용으로 이식.
///
/// 사용법 1 — 코드비하인드에서 명시적 호출:
///   TextBoxDropHelper.Enable(myTextBox);
///
/// 사용법 2 — AXAML Attached Property:
///   xmlns:helpers="using:SaemDesk.Helpers"
///   helpers:TextBoxDropHelper.EnableTextDrop="True"
/// </summary>
public static class TextBoxDropHelper
{
    // ────────────────────────────────────────────────────
    //  Attached Property
    // ────────────────────────────────────────────────────

    public static readonly AttachedProperty<bool> EnableTextDropProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "EnableTextDrop",
            typeof(TextBoxDropHelper),
            defaultValue: false,
            inherits: false);

    public static bool GetEnableTextDrop(AvaloniaObject obj)
        => obj.GetValue(EnableTextDropProperty);

    public static void SetEnableTextDrop(AvaloniaObject obj, bool value)
        => obj.SetValue(EnableTextDropProperty, value);

    static TextBoxDropHelper()
    {
        EnableTextDropProperty.Changed.AddClassHandler<TextBox>((tb, e) =>
        {
            if (e.NewValue is true) Attach(tb);
            else                    Detach(tb);
        });
    }

    // ────────────────────────────────────────────────────
    //  코드비하인드에서 명시적 호출용
    // ────────────────────────────────────────────────────

    public static void Enable(TextBox textBox)  => Attach(textBox);
    public static void Disable(TextBox textBox) => Detach(textBox);

    // ────────────────────────────────────────────────────
    //  내부
    // ────────────────────────────────────────────────────

    private static void Attach(TextBox tb)
    {
        DragDrop.SetAllowDrop(tb, true);
        tb.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        tb.AddHandler(DragDrop.DragOverEvent,  OnDragOver);
        tb.AddHandler(DragDrop.DropEvent,      OnDrop);
    }

    private static void Detach(TextBox tb)
    {
        DragDrop.SetAllowDrop(tb, false);
        tb.RemoveHandler(DragDrop.DragEnterEvent, OnDragEnter);
        tb.RemoveHandler(DragDrop.DragOverEvent,  OnDragOver);
        tb.RemoveHandler(DragDrop.DropEvent,      OnDrop);
    }

    // ────────────────────────────────────────────────────
    //  Avalonia 12 API: e.DataTransfer, DataFormat.Text
    // ────────────────────────────────────────────────────

    private static void OnDragEnter(object? sender, DragEventArgs e)
        => e.DragEffects = HasText(e) ? DragDropEffects.Copy : DragDropEffects.None;

    private static void OnDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = HasText(e) ? DragDropEffects.Copy : DragDropEffects.None;

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not TextBox tb) return;

        // Enrollment 드래그: 이름(번호) 형태로 변환
        string? text = null;
        if (e.DataTransfer.Contains(ListStudent.EnrollmentDragFormat))
        {
            var enrollment = e.DataTransfer.TryGetValue(ListStudent.EnrollmentDragFormat);
            if (enrollment is not null)
                text = $"{enrollment.Name}({enrollment.Number})";
        }

        // 일반 텍스트 드래그 fallback
        if (text == null)
            text = e.DataTransfer.TryGetText();

        if (string.IsNullOrEmpty(text)) return;

        int caret = tb.CaretIndex;

        if (tb.SelectionEnd > tb.SelectionStart)
        {
            int start   = tb.SelectionStart;
            string curr = tb.Text ?? string.Empty;
            tb.Text       = curr.Remove(start, tb.SelectionEnd - start).Insert(start, text);
            tb.CaretIndex = start + text.Length;
        }
        else
        {
            string curr = tb.Text ?? string.Empty;
            tb.Text       = curr.Insert(caret, text);
            tb.CaretIndex = caret + text.Length;
        }

        e.Handled = true;
    }

    // Enrollment 포맷 또는 일반 텍스트 포맷 허용
    private static bool HasText(DragEventArgs e)
        => e.DataTransfer.Contains(ListStudent.EnrollmentDragFormat)
        || e.DataTransfer.Formats.Any(f => f == DataFormat.Text);
}
