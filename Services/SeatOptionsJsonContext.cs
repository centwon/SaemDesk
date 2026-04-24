using System.Text.Json.Serialization;
using SaemDesk.Models;

namespace SaemDesk.Services;

/// <summary>
/// Native AOT 호환 JSON Serialization Context for SeatOptions.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(SeatOptions))]
[JsonSerializable(typeof(SeatOptions.PairRule))]
internal partial class SeatOptionsJsonContext : JsonSerializerContext
{
}
