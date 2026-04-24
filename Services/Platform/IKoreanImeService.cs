using System;

namespace SaemDesk.Services.Platform;

/// <summary>
/// 한글 IME 강제 전환 서비스 인터페이스 — 플랫폼 독립적.
/// Windows 구현: WindowsKoreanImeService (KoreanImeHelper P/Invoke 래퍼)
/// </summary>
public interface IKoreanImeService
{
    /// <summary>
    /// 현재 포커스 윈도우(또는 지정 HWND)를 한글 IME 모드로 전환 시도.
    /// </summary>
    void TryEnableHangul(IntPtr? hwnd = null);
}
