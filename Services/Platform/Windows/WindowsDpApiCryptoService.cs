#pragma warning disable CA1416 // Windows-only: DPAPI — SaemDesk is Windows-only for Phase 1-2
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace SaemDesk.Services.Platform.Windows;

/// <summary>
/// Windows DPAPI(ProtectedData) 기반 암호화 서비스 구현.
/// ICryptoService를 구현하여 GoogleAuthService, StudentRepository 등에서 사용.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsDpApiCryptoService : ICryptoService
{
    public byte[] Protect(byte[] data)
        => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] data)
        => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
