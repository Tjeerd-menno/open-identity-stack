using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OpenIdentityStack.Infrastructure.Persistence.Converters;

/// <summary>
/// Value converter for JsonDocument to enable storage in databases that don't support native JSON types.
/// Serializes JsonDocument to string for storage and deserializes back on retrieval.
/// Works with both PostgreSQL (which will use its native jsonb support) and SQLite (which stores as TEXT).
/// </summary>
public sealed class JsonDocumentConverter : ValueConverter<JsonDocument?, string?>
{
    public JsonDocumentConverter()
        : base(
            v => v != null ? v.RootElement.GetRawText() : null,
            v => v != null ? JsonDocument.Parse(v) : null)
    {
    }
}
