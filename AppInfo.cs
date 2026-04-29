using System.Reflection;

namespace SaemDesk;

/// <summary>
/// 앱 메타데이터 — csproj 의 &lt;Version&gt; · &lt;Product&gt; · &lt;Company&gt; 와 동기화.
/// AOT 호환: 단순 문자열만 노출하므로 리플렉션 트림 영향 없음.
/// </summary>
public static class AppInfo
{
    /// <summary>표시용 버전 — UI 좌·우하단 등에 사용.</summary>
    public const string Version = "v0.1.0-alpha";

    /// <summary>제품명.</summary>
    public const string Product = "SaemDesk";

    /// <summary>제작사 / 저자.</summary>
    public const string Company = "Centwons";

    /// <summary>저작권 표기.</summary>
    public const string Copyright = "© 2026 Centwons";

    /// <summary>한 줄 설명.</summary>
    public const string Description = "학교 업무 통합 데스크탑";

    /// <summary>어셈블리에서 읽은 informational version (있으면).</summary>
    public static string? InformationalVersion =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
}
