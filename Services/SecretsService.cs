namespace SaemDesk.Services;

/// <summary>
/// API 키·OAuth 자격증명을 제공하는 통합 서비스.
/// 빌드 시 secrets.props 의 값이 BuildSecrets.g.cs 상수로 주입된다.
/// secrets.props 가 없거나 키가 비어 있으면 빈 문자열 — 호출부에서 기능 비활성 처리.
/// </summary>
public static class SecretsService
{
    /// <summary>Google OAuth 클라이언트 ID</summary>
    public static string GoogleClientId => BuildSecrets.GoogleClientId;

    /// <summary>Google OAuth 클라이언트 Secret</summary>
    public static string GoogleClientSecret => BuildSecrets.GoogleClientSecret;

    /// <summary>나이스 데이터포털 Open API 인증키</summary>
    public static string NeisApiKey => BuildSecrets.NeisApiKey;
}
