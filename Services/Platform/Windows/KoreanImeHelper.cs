using System;
using System.Runtime.InteropServices;

namespace SaemDesk.Services.Platform.Windows;

/// <summary>
/// Win32 P/Invoke 기반 한글 IME 강제 전환 유틸리티.
/// Avalonia TextBox 첨부 속성(UseHangul)은 Phase 4 Controls에서 구현.
/// </summary>
public static class KoreanImeHelper
{
    #region Win32 Interop

    private const int    IME_CMODE_NATIVE  = 0x0001;
    private const ushort VK_HANGUL         = 0x15;
    private const ushort VK_IME_ON         = 0x16;
    private const uint   INPUT_KEYBOARD    = 1;
    private const uint   KEYEVENTF_KEYUP   = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint   dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmGetConversionStatus(IntPtr hIMC, out int conversion, out int sentence);

    #endregion

    /// <summary>
    /// 현재 포커스 HWND를 기준으로 한글 IME 모드가 아니면 전환 시도.
    /// Avalonia TextBox의 GotFocus 핸들러에서 호출한다.
    /// </summary>
    public static void TryEnableHangul(IntPtr? windowHandle = null)
    {
        try
        {
            IntPtr hwnd = windowHandle ?? GetFocus();
            if (hwnd == IntPtr.Zero) return;

            bool isHangul = false;
            IntPtr hIMC = ImmGetContext(hwnd);
            if (hIMC != IntPtr.Zero)
            {
                try
                {
                    if (ImmGetConversionStatus(hIMC, out int conv, out _))
                        isHangul = (conv & IME_CMODE_NATIVE) != 0;
                }
                finally { ImmReleaseContext(hwnd, hIMC); }
            }

            if (isHangul) return;

            if (SendKey(VK_IME_ON) == 0)
                SendKey(VK_HANGUL);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KoreanImeHelper] IME 전환 실패: {ex.Message}");
        }
    }

    private static uint SendKey(ushort vk)
    {
        var inputs = new INPUT[2];
        inputs[0] = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = vk } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } };
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }
}
