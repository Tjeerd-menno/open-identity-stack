using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Federation.Queries;
using OpenIdentityStack.Application.Settings.Commands;
using OpenIdentityStack.Application.Settings.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;
namespace OpenIdentityStack.Api.Settings;

/// <summary>
/// Minimal API endpoints for managing authentication settings.
/// </summary>
internal static class AuthenticationSettingsApi
{
    public static IEndpointRouteBuilder MapAuthenticationSettingsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/authentication-settings")
            .WithTags(nameof(AuthenticationSettingsApi));

        // Get current settings
        group.MapGet(string.Empty, GetSettings)
            .RequireAuthorization(Permissions.System.ManageSettings)
            .Produces<AuthenticationSettingsResponse>(StatusCodes.Status200OK)
            .WithName("GetAuthenticationSettings")
            .WithSummary("Gets the current authentication settings");

        // List available providers
        group.MapGet("providers", ListProviders)
            .RequireAuthorization(Permissions.System.ManageSettings)
            .Produces<IReadOnlyList<AuthenticationProviderResponse>>(StatusCodes.Status200OK)
            .WithName("ListAuthenticationProviders")
            .WithSummary("Lists all available authentication providers");

        // Set default provider
        group.MapPut("default-provider", SetDefaultProvider)
            .RequireAuthorization(Permissions.System.ManageSettings)
            .Produces<AuthenticationSettingsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("SetDefaultProvider")
            .WithSummary("Sets the default authentication provider");

        // Set local fallback
        group.MapPut("local-fallback", SetLocalFallback)
            .RequireAuthorization(Permissions.System.ManageSettings)
            .Produces<AuthenticationSettingsResponse>(StatusCodes.Status200OK)
            .WithName("SetLocalFallback")
            .WithSummary("Enables or disables local fallback authentication for IAM admins");

        return app;
    }

    private static async Task<IResult> GetSettings(
        [FromServices] IGetAuthenticationSettingsQueryHandler queryHandler,
        CancellationToken cancellationToken = default)
    {
        Result<AuthenticationSettingsDto> result = await queryHandler.HandleAsync(
            new GetAuthenticationSettingsQuery(),
            cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        return TypedResults.Ok(new AuthenticationSettingsResponse
        {
            DefaultProviderId = result.Value.DefaultProviderId,
            IsLocalDefault = result.Value.IsLocalDefault,
            LocalFallbackEnabled = result.Value.LocalFallbackEnabled,
            UpdatedAt = result.Value.UpdatedAt
        });
    }

    private static async Task<IResult> ListProviders(
        [FromServices] IListProvidersQueryHandler listProvidersQueryHandler,
        CancellationToken cancellationToken = default)
    {
        // Get external providers
        var query = new ListProvidersQuery(IncludeDisabled: false);
        Result<IReadOnlyList<ProviderDto>> result = await listProvidersQueryHandler.HandleAsync(query, cancellationToken);

        var providers = new List<AuthenticationProviderResponse>
        {
            // Always include local provider
            new()
            {
                Id = AuthenticationSettings.LocalProviderId,
                Name = "local",
                DisplayName = "Local Accounts",
                Type = "local",
                IsActive = true
            }
        };

        if (result.IsSuccess)
        {
            // Add external providers
            foreach (ProviderDto provider in result.Value)
            {
                providers.Add(new AuthenticationProviderResponse
                {
                    Id = provider.Id.ToString(),
                    Name = provider.Name,
                    DisplayName = provider.DisplayName,
                    Type = "external",
                    IsActive = provider.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        return TypedResults.Ok(providers);
    }

    private static async Task<IResult> SetDefaultProvider(
        [FromServices] ISetDefaultProviderUseCase setDefaultProviderUseCase,
        [FromServices] IGetAuthenticationSettingsQueryHandler queryHandler,
        [FromBody] SetDefaultProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new SetDefaultProviderCommand(request.ProviderId);
        Result<SetDefaultProviderResult> result = await setDefaultProviderUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound", StringComparison.Ordinal))
            {
                return TypedResults.NotFound(new { error = result.Error.Description });
            }

            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        // Get updated settings
        Result<AuthenticationSettingsDto> settingsResult = await queryHandler.HandleAsync(
            new GetAuthenticationSettingsQuery(),
            cancellationToken);

        if (settingsResult.IsFailure)
        {
            return TypedResults.BadRequest(new { error = settingsResult.Error.Description });
        }

        return TypedResults.Ok(new AuthenticationSettingsResponse
        {
            DefaultProviderId = settingsResult.Value.DefaultProviderId,
            IsLocalDefault = settingsResult.Value.IsLocalDefault,
            LocalFallbackEnabled = settingsResult.Value.LocalFallbackEnabled,
            UpdatedAt = settingsResult.Value.UpdatedAt
        });
    }

    private static async Task<IResult> SetLocalFallback(
        [FromServices] ISetLocalFallbackUseCase setLocalFallbackUseCase,
        [FromServices] IGetAuthenticationSettingsQueryHandler queryHandler,
        [FromBody] SetLocalFallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new SetLocalFallbackCommand(request.Enabled);
        Result<SetLocalFallbackResult> result = await setLocalFallbackUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        // Get updated settings
        Result<AuthenticationSettingsDto> settingsResult = await queryHandler.HandleAsync(
            new GetAuthenticationSettingsQuery(),
            cancellationToken);

        if (settingsResult.IsFailure)
        {
            return TypedResults.BadRequest(new { error = settingsResult.Error.Description });
        }

        return TypedResults.Ok(new AuthenticationSettingsResponse
        {
            DefaultProviderId = settingsResult.Value.DefaultProviderId,
            IsLocalDefault = settingsResult.Value.IsLocalDefault,
            LocalFallbackEnabled = settingsResult.Value.LocalFallbackEnabled,
            UpdatedAt = settingsResult.Value.UpdatedAt
        });
    }
}
