using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Removes OpenIddict bookkeeping claims from serialized ID tokens.
/// </summary>
public sealed class TrimIdentityTokenInternalClaimsHandler : IOpenIddictServerHandler<GenerateTokenContext>
{
    /// <summary>
    /// Gets the default descriptor for this handler.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<GenerateTokenContext>()
            .UseSingletonHandler<TrimIdentityTokenInternalClaimsHandler>()
            .SetOrder(OpenIddictServerHandlers.Protection.GenerateIdentityModelToken.Descriptor.Order - 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public ValueTask HandleAsync(GenerateTokenContext context)
    {
        if (!IsIdentityToken(context))
        {
            return ValueTask.CompletedTask;
        }

        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            RemoveInternalClaims(identity);
        }

        if (context.SecurityTokenDescriptor.Subject is ClaimsIdentity subject)
        {
            RemoveInternalClaims(subject);
        }

        foreach (string claim in context.SecurityTokenDescriptor.Claims.Keys.Where(claim => claim.StartsWith("oi_", StringComparison.Ordinal)).ToList())
        {
            context.SecurityTokenDescriptor.Claims.Remove(claim);
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsIdentityToken(GenerateTokenContext context) =>
        string.Equals(context.TokenType, "id_token", StringComparison.Ordinal)
        || string.Equals(context.TokenType, "identity_token", StringComparison.Ordinal)
        || context.Principal?.HasClaim(claim => string.Equals(claim.Type, "nonce", StringComparison.Ordinal)) == true
        || context.SecurityTokenDescriptor.Claims.ContainsKey("nonce");

    private static void RemoveInternalClaims(ClaimsIdentity identity)
    {
        foreach (Claim claim in identity.Claims.Where(IsOpenIddictInternalClaim).ToList())
        {
            identity.RemoveClaim(claim);
        }
    }

    private static bool IsOpenIddictInternalClaim(Claim claim) =>
        claim.Type.StartsWith("oi_", StringComparison.Ordinal);
}

/// <summary>
/// Extension methods for registering token claim trimming handlers.
/// </summary>
public static class InternalTokenClaimTrimmingExtensions
{
    /// <summary>
    /// Adds handlers that keep OpenID-facing tokens free of OpenIddict internal state claims.
    /// </summary>
    public static OpenIddictServerBuilder AddInternalTokenClaimTrimming(this OpenIddictServerBuilder builder)
    {
        builder.Services.AddSingleton<TrimIdentityTokenInternalClaimsHandler>();
        builder.AddEventHandler(TrimIdentityTokenInternalClaimsHandler.Descriptor);

        return builder;
    }
}
