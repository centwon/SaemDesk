using System;
using System.Runtime.Versioning;

namespace SaemDesk.Services.Platform.Windows;

/// <summary>
/// Windows IME P/Invoke 기반 한글 전환 서비스 구현.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsKoreanImeService : IKoreanImeService
{
    public void TryEnableHangul(IntPtr? hwnd = null)
        => KoreanImeHelper.TryEnableHangul(hwnd);
}
