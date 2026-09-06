using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>Records trusted issuance evidence so per-user revocation cannot confuse a client identifier with a user subject.</summary>
public sealed class ApplicationTokenSubjectMetadata(OpenIdentityStackDbContext dbContext) :
    IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        if (!context.CreateTokenEntry || context.Request?.IsClientCredentialsGrantType() != true
            || context.TokenType != OpenIddictConstants.TokenTypeIdentifiers.AccessToken
            || context.Principal.GetClaim(TokenSubjectClaims.Kind) != TokenSubjectClaims.Application
            || context.Principal.GetClaim(UserCredentialClaims.Revision) is not null
            || !string.Equals(context.Principal.GetClaim(OpenIddictConstants.Claims.Subject), context.Request.ClientId, StringComparison.Ordinal))
        {
            return;
        }

        string tokenId = context.Principal.GetTokenId() ?? throw new InvalidOperationException("The application token entry was not created.");
        OpenIddictEntityFrameworkCoreToken token = await dbContext.Set<OpenIddictEntityFrameworkCoreToken>()
            .SingleAsync(entry => entry.Id == tokenId, context.CancellationToken);
        Dictionary<string, JsonElement> properties = string.IsNullOrEmpty(token.Properties)
            ? [] : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(token.Properties)!;
        properties[TokenSubjectClaims.Kind] = JsonSerializer.SerializeToElement(TokenSubjectClaims.Application);
        token.Properties = JsonSerializer.Serialize(properties);
        // The tracked entry saves only the metadata property, never stale token status.
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
