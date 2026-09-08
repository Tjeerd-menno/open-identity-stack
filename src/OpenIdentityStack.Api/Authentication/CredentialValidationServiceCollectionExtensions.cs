using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Validation;
using OpenIddict.Server;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions;
using OpenIdentityStack.Domain.Common;
using SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Registers authoritative session validation for interactive cookies.
/// </summary>
public static class CredentialValidationServiceCollectionExtensions
{
    public static IServiceCollection AddCredentialValidation(this IServiceCollection services)
    {
        services.AddScoped<ICredentialSessionValidator, CredentialSessionValidator>();
        services.AddScoped<AuthoritativeCredentialValidationHandler>();
        services.AddScoped<CurrentAuthorizationClaimsProjector>();
        services.AddScoped<AuthoritativeIntrospectionHandler>();
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, CredentialCookieOptionsPostConfigure>();
        services.AddOpenIddict()
            .AddValidation(options => options.AddEventHandler(
                OpenIddictValidationHandlerDescriptor.CreateBuilder<OpenIddictValidationEvents.ProcessAuthenticationContext>()
                    .UseScopedHandler<AuthoritativeCredentialValidationHandler>()
                    .SetOrder(int.MaxValue - 1000)
                    .Build()));
        services.AddOpenIddict()
            .AddServer(options => options.AddEventHandler(
                OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleIntrospectionRequestContext>()
                    .UseScopedHandler<AuthoritativeIntrospectionHandler>()
                    .SetOrder(int.MaxValue - 1000)
                    .Build()));
        return services;
    }
}

internal sealed class CredentialCookieOptionsPostConfigure : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!string.Equals(name, "Cookies", StringComparison.Ordinal))
        {
            return;
        }

        Func<CookieValidatePrincipalContext, Task> existingHandler = options.Events.OnValidatePrincipal;
        options.Events.OnValidatePrincipal = async context =>
        {
            await existingHandler(context);
            if (context.Principal is null)
            {
                return;
            }

            string? userIdValue = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Principal.FindFirstValue(Claims.Subject);
            string? sessionIdValue = context.Principal.FindFirstValue("sid")
                ?? context.Principal.FindFirstValue(TokenClaimProjectionService.LegacySessionIdClaim);

            if (!Guid.TryParse(userIdValue, out Guid userId)
                || !Guid.TryParse(sessionIdValue, out Guid sessionId)
                || !await context.HttpContext.RequestServices
                    .GetRequiredService<ICredentialSessionValidator>()
                    .IsValidAsync(new UserId(userId), new SessionId(sessionId), context.HttpContext.RequestAborted))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync("Cookies");
            }
        };
    }
}
