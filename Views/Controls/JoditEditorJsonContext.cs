using System.Text.Json.Serialization;

namespace SaemDesk.Views.Controls;

/// <summary>Native AOT 호환 JSON 직렬화 컨텍스트 — JoditEditor JS ↔ C# 통신용.</summary>
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(JoditMessage))]
internal partial class JoditEditorJsonContext : JsonSerializerContext { }

/// <summary>JoditEditor JS → C# 메시지.</summary>
internal sealed class JoditMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
