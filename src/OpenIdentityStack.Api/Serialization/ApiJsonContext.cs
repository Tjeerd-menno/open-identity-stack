using System.Text.Json.Serialization;

using OpenIdentityStack.Api.Authentication;

namespace OpenIdentityStack.Api.Serialization;

/// <summary>
/// Source-generated serialization metadata for the JSON payloads the API reads and writes outside
/// of the MVC and OpenIddict formatters.
/// </summary>
[JsonSerializable(typeof(SessionMonitoringCookieService.SessionMonitoringCookiePayload))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
