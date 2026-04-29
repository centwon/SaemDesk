using System.Text.Json.Serialization;

namespace SaemDesk.Views.Controls;

/// <summary>Native AOT 호환 JSON 직렬화 컨텍스트 — JoditEditor JS ↔ C# 통신용.</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(string))]
internal partial class JoditEditorJsonContext : JsonSerializerContext { }
