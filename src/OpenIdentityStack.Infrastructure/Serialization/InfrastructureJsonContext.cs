using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenIdentityStack.Infrastructure.Serialization;

/// <summary>
/// Source-generated serialization metadata for the JSON payloads persistence and identity adapters
/// read and write outside of the EF Core value-converter pipeline.
/// </summary>
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(string))]
internal sealed partial class InfrastructureJsonContext : JsonSerializerContext;
