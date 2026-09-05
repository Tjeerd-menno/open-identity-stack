using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Application.Federation.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Api.Federation;

/// <summary>
/// Minimal API endpoints for managing upstream OIDC providers.
/// </summary>
internal static class ProvidersApi
{
    public static IEndpointRouteBuilder MapProvidersApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/providers")
            .WithTags(nameof(ProvidersApi));

        // Provider CRUD
        group.MapGet(string.Empty, ListProviders)
            .RequireAuthorization(Permissions.Providers.Read)
            .Produces<IReadOnlyList<ProviderResponse>>(StatusCodes.Status200OK)
            .WithName("ListProviders")
            .WithSummary("Lists all upstream providers");

        group.MapGet("{id:guid}", GetProvider)
            .RequireAuthorization(Permissions.Providers.Read)
            .Produces<ProviderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetProvider")
            .WithSummary("Gets a provider by ID");

        group.MapPost(string.Empty, CreateProvider)
            .RequireAuthorization(Permissions.Providers.Write)
            .Produces<ProviderResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("CreateProvider")
            .WithSummary("Creates a new upstream provider");

        group.MapPatch("{id:guid}", UpdateProvider)
            .RequireAuthorization(Permissions.Providers.Write)
            .Produces<ProviderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("UpdateProvider")
            .WithSummary("Updates an existing provider");

        group.MapDelete("{id:guid}", DeleteProvider)
            .RequireAuthorization(Permissions.Providers.Delete)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteProvider")
            .WithSummary("Deletes a provider");

        // Provider status
        group.MapPost("{id:guid}/disable", DisableProvider)
            .RequireAuthorization(Permissions.Providers.Write)
            .Produces<ProviderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DisableProvider")
            .WithSummary("Disables an upstream provider");

        group.MapPost("{id:guid}/enable", EnableProvider)
            .RequireAuthorization(Permissions.Providers.Write)
            .Produces<ProviderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("EnableProvider")
            .WithSummary("Enables an upstream provider");

        return app;
    }

    private static async Task<IResult> ListProviders(
        [FromServices] IListProvidersQueryHandler listProvidersQueryHandler,
        [FromQuery] bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var query = new ListProvidersQuery(includeDisabled);
        Result<IReadOnlyList<ProviderDto>> result = await listProvidersQueryHandler.HandleAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        var responses = result.Value.Select(MapToResponse).ToList();
        return TypedResults.Ok(responses);
    }

    private static async Task<IResult> GetProvider(
        [FromServices] IUpstreamProviderRepository providerRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var providerId = UpstreamProviderId.From(id);
        UpstreamProvider? provider = await providerRepository.GetByIdAsync(providerId, cancellationToken);

        if (provider is null)
        {
            return TypedResults.NotFound(new { error = "Provider not found." });
        }

        return TypedResults.Ok(MapToResponse(provider));
    }

    private static async Task<IResult> CreateProvider(
        [FromServices] ICreateProviderUseCase createProviderUseCase,
        HttpContext context,
        [FromBody] CreateProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateProviderCommand(
            request.Name,
            request.DisplayName ?? request.Name,
            request.Authority,
            request.ClientId,
            request.ClientSecret,
            request.Scopes,
            request.JitProvisioningEnabled,
            context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier));

        Result<CreateProviderResult> result = await createProviderUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.StartsWith("Forbidden.", StringComparison.Ordinal))
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, detail: result.Error.Description);
            }
            if (result.Error.Code.Contains("Conflict", StringComparison.Ordinal) || result.Error.Code.Contains("Exists", StringComparison.Ordinal))
            {
                return TypedResults.Conflict(new { error = result.Error.Description });
            }

            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        var response = new ProviderResponse
        {
            Id = result.Value.Id,
            Name = result.Value.Name,
            DisplayName = result.Value.DisplayName,
            Authority = result.Value.Authority,
            ClientId = result.Value.ClientId,
            Scopes = result.Value.Scopes,
            JitProvisioningEnabled = result.Value.JitProvisioningEnabled,
            Status = result.Value.Status,
            CreatedAt = result.Value.CreatedAt
        };

        return TypedResults.Created($"/api/admin/providers/{response.Id}", response);
    }

    private static async Task<IResult> UpdateProvider(
        [FromServices] IUpdateProviderUseCase updateProviderUseCase,
        [FromServices] IAuditLog auditLog,
        HttpContext httpContext,
        Guid id,
        [FromBody] UpdateProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Authority is not null)
        {
            await auditLog.LogAsync(httpContext.User.FindFirst("sub")?.Value ?? "management", "Federation.AuthorityReplacementRejected", "UpstreamProvider", id.ToString(), "Provider replacement requires a new registration and explicit identity migration.", cancellationToken);
            return TypedResults.BadRequest(new { error = "Provider authority cannot be replaced. Register a new provider and migrate identities explicitly." });
        }

        var command = new UpdateProviderCommand(
            id,
            request.DisplayName,
            request.ClientId,
            request.ClientSecret,
            request.Scopes,
            request.JitProvisioningEnabled,
            request.Status,
            context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier));

        Result<UpdateProviderResult> result = await updateProviderUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.StartsWith("Forbidden.", StringComparison.Ordinal))
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, detail: result.Error.Description);
            }
            if (result.Error.Code.Contains("NotFound", StringComparison.Ordinal))
            {
                return TypedResults.NotFound(new { error = result.Error.Description });
            }

            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        var response = new ProviderResponse
        {
            Id = result.Value.Id,
            Name = result.Value.Name,
            DisplayName = result.Value.DisplayName,
            Authority = result.Value.Authority,
            ClientId = result.Value.ClientId,
            Scopes = result.Value.Scopes,
            JitProvisioningEnabled = result.Value.JitProvisioningEnabled,
            Status = result.Value.Status,
            CreatedAt = result.Value.CreatedAt
        };

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> DeleteProvider(
        [FromServices] IUpstreamProviderRepository providerRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var providerId = UpstreamProviderId.From(id);
        UpstreamProvider? provider = await providerRepository.GetByIdAsync(providerId, cancellationToken);

        if (provider is null)
        {
            return TypedResults.NotFound(new { error = "Provider not found." });
        }

        // Soft delete by disabling
        Result result = provider.Disable();
        if (result.IsFailure && !result.Error.Code.Contains("AlreadyDisabled", StringComparison.Ordinal))
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        await providerRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> DisableProvider(
        [FromServices] IUpstreamProviderRepository providerRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var providerId = UpstreamProviderId.From(id);
        UpstreamProvider? provider = await providerRepository.GetByIdAsync(providerId, cancellationToken);

        if (provider is null)
        {
            return TypedResults.NotFound(new { error = "Provider not found." });
        }

        Result result = provider.Disable();
        if (result.IsFailure && !result.Error.Code.Contains("AlreadyDisabled", StringComparison.Ordinal))
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        await providerRepository.SaveChangesAsync(cancellationToken);

        // Return the updated resource for immediate client feedback
        return TypedResults.Ok(MapToResponse(provider));
    }

    private static async Task<IResult> EnableProvider(
        [FromServices] IUpstreamProviderRepository providerRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var providerId = UpstreamProviderId.From(id);
        UpstreamProvider? provider = await providerRepository.GetByIdAsync(providerId, cancellationToken);

        if (provider is null)
        {
            return TypedResults.NotFound(new { error = "Provider not found." });
        }

        Result result = provider.Enable();
        if (result.IsFailure && !result.Error.Code.Contains("AlreadyActive", StringComparison.Ordinal))
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        await providerRepository.SaveChangesAsync(cancellationToken);

        // Return the updated resource for immediate client feedback
        return TypedResults.Ok(MapToResponse(provider));
    }

    private static ProviderResponse MapToResponse(ProviderDto dto)
    {
        return new ProviderResponse
        {
            Id = dto.Id,
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            Authority = dto.Authority,
            ClientId = dto.ClientId,
            Scopes = dto.Scopes,
            JitProvisioningEnabled = dto.JitProvisioningEnabled,
            Status = dto.Status,
            CreatedAt = dto.CreatedAt
        };
    }

    private static ProviderResponse MapToResponse(UpstreamProvider provider)
    {
        return new ProviderResponse
        {
            Id = provider.Id.Value,
            Name = provider.Name,
            DisplayName = provider.DisplayName,
            Authority = provider.Authority,
            ClientId = provider.ClientId,
            Scopes = [],
            JitProvisioningEnabled = provider.JitProvisioningEnabled,
            Status = provider.Status.ToString(),
            CreatedAt = provider.CreatedAt
        };
    }
}
