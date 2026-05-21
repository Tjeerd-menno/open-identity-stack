using System.Text.Json.Serialization;
using OpenIdentityStack.Infrastructure.ExternalProviders;
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Infrastructure.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(OidcDiscoveryDocument))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(UserInfoResponse))]
[JsonSerializable(typeof(BackChannelLogoutPayload))]
internal sealed partial class InfrastructureJsonContext : JsonSerializerContext;
