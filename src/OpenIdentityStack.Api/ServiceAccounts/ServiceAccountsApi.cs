using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Application.ServiceAccounts.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Api.ServiceAccounts;

/// <summary>
/// Minimal API endpoints for managing service accounts via Admin API.
/// </summary>
internal static class ServiceAccountsApi
{
    public static IEndpointRouteBuilder MapServiceAccountsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/service-accounts")
            .WithTags(nameof(ServiceAccountsApi));

        // Service Account CRUD
        group.MapPost(string.Empty, CreateServiceAccount)
            .RequireAuthorization(Permissions.ServiceAccounts.Write)
            .Produces<ServiceAccountCreatedResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateServiceAccount")
            .WithSummary("Creates a new service account");

        group.MapGet("{id:guid}", GetServiceAccount)
            .RequireAuthorization(Permissions.ServiceAccounts.Read)
            .Produces<ServiceAccountResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetServiceAccount")
            .WithSummary("Gets a service account by ID");

        group.MapPatch("{id:guid}", UpdateServiceAccount)
            .RequireAuthorization(Permissions.ServiceAccounts.Write)
            .Produces<ServiceAccountResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateServiceAccount")
            .WithSummary("Updates a service account");

        group.MapGet(string.Empty, ListServiceAccounts)
            .RequireAuthorization(Permissions.ServiceAccounts.Read)
            .Produces<ListServiceAccountsResponse>(StatusCodes.Status200OK)
            .WithName("ListServiceAccounts")
            .WithSummary("Lists all service accounts with pagination");

        group.MapDelete("{id:guid}", DeleteServiceAccount)
            .RequireAuthorization(Permissions.ServiceAccounts.Delete)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteServiceAccount")
            .WithSummary("Deletes a service account");

        // Service Account status
        group.MapPost("{id:guid}/disable", DisableServiceAccount)
            .RequireAuthorization(Permissions.ServiceAccounts.Write)
            .Produces<ServiceAccountResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DisableServiceAccount")
            .WithSummary("Disables a service account");

        group.MapPost("{id:guid}/enable", EnableServiceAccount)
            .RequireAuthorization(Permissions.ServiceAccounts.Write)
            .Produces<ServiceAccountResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("EnableServiceAccount")
            .WithSummary("Enables a service account");

        // Credentials and certificates
        group.MapPost("{id:guid}/rotate-secret", RotateSecret)
            .RequireAuthorization(Permissions.ServiceAccounts.RotateSecret)
            .Produces<RotateSecretResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("RotateServiceAccountSecret")
            .WithSummary("Rotates the secret for a service account");

        group.MapPost("{id:guid}/certificates", AddCertificate)
            .RequireAuthorization(Permissions.ServiceAccounts.ManageCertificates)
            .Produces<AddCertificateResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("AddServiceAccountCertificate")
            .WithSummary("Adds a certificate to a service account");

        return app;
    }

    private static async Task<IResult> CreateServiceAccount(
        [FromServices] ICreateServiceAccountUseCase createServiceAccountUseCase,
        [FromBody] CreateServiceAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateServiceAccountCommand(
            request.ClientId,
            request.DisplayName,
            request.AllowedScopes,
            request.AllowedGrantTypes);

        Result<CreateServiceAccountResponse> result = await createServiceAccountUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ServiceAccount.ClientIdExists")
            {
                return TypedResults.Conflict(new { error = result.Error.Code, message = result.Error.Description });
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        var response = new ServiceAccountCreatedResponse(
            result.Value.ServiceAccountId.Value,
            result.Value.ClientId,
            result.Value.DisplayName,
            result.Value.InitialSecret,
            DateTimeOffset.UtcNow);

        return TypedResults.Created($"/api/admin/service-accounts/{result.Value.ServiceAccountId.Value}", response);
    }

    private static async Task<IResult> GetServiceAccount(
        [FromServices] IGetServiceAccountQueryHandler getServiceAccountQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result<ServiceAccountDetails> result = await getServiceAccountQueryHandler.HandleAsync(new ServiceAccountId(id), cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        ServiceAccountDetails detail = result.Value;
        var response = new ServiceAccountResponse(
            detail.Id.Value,
            detail.ClientId,
            detail.DisplayName,
            detail.Status,
            detail.AllowedScopes,
            detail.AllowedGrantTypes,
            detail.CredentialCount,
            detail.CertificateCount,
            detail.CreatedAt,
            detail.ModifiedAt);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> UpdateServiceAccount(
        [FromServices] IUpdateServiceAccountUseCase updateServiceAccountUseCase,
        [FromServices] IGetServiceAccountQueryHandler getServiceAccountQueryHandler,
        Guid id,
        [FromBody] UpdateServiceAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateServiceAccountCommand(
            new ServiceAccountId(id),
            request.DisplayName);

        Result<UpdateServiceAccountResult> result = await updateServiceAccountUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ServiceAccount.NotFound")
            {
                return TypedResults.NotFound(new { error = result.Error.Code, message = result.Error.Description });
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        // Fetch the updated service account to return full details
        Result<ServiceAccountDetails> detailResult = await getServiceAccountQueryHandler.HandleAsync(new ServiceAccountId(id), cancellationToken);

        if (detailResult.IsFailure)
        {
            return TypedResults.NotFound();
        }

        ServiceAccountDetails detail = detailResult.Value;
        var response = new ServiceAccountResponse(
            detail.Id.Value,
            detail.ClientId,
            detail.DisplayName,
            detail.Status,
            detail.AllowedScopes,
            detail.AllowedGrantTypes,
            detail.CredentialCount,
            detail.CertificateCount,
            detail.CreatedAt,
            detail.ModifiedAt);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ListServiceAccounts(
        [FromServices] IListServiceAccountsQueryHandler listServiceAccountsQueryHandler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ServiceAccountStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<ServiceAccountSummary>> result = await listServiceAccountsQueryHandler.HandleAsync(page, pageSize, status, search, cancellationToken);
        PagedResult<ServiceAccountSummary> paged = result.Value;

        var items = paged.Items.Select(s => new ServiceAccountResponse(
            s.Id.Value,
            s.ClientId,
            s.DisplayName,
            s.Status,
            s.AllowedScopes,
            new List<string>(), // Summary doesn't include Grant Types
            s.CredentialCount,
            s.CertificateCount,
            s.CreatedAt,
            s.ModifiedAt)).ToList();

        return TypedResults.Ok(new ListServiceAccountsResponse(
            items,
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages,
            paged.HasPreviousPage,
            paged.HasNextPage));
    }

    private static async Task<IResult> DeleteServiceAccount(
        [FromServices] IDeleteServiceAccountUseCase deleteServiceAccountUseCase,
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteServiceAccountCommand(new ServiceAccountId(id));
        Result result = await deleteServiceAccountUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ServiceAccount.NotFound")
            {
                return TypedResults.NotFound(new { error = result.Error.Code, message = result.Error.Description });
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> DisableServiceAccount(
        [FromServices] IDisableServiceAccountUseCase disableServiceAccountUseCase,
        [FromServices] IGetServiceAccountQueryHandler getServiceAccountQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DisableServiceAccountCommand(new ServiceAccountId(id));
        Result result = await disableServiceAccountUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ServiceAccount.NotFound")
            {
                return TypedResults.NotFound(new { error = result.Error.Code, message = result.Error.Description });
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        // Return the updated resource for immediate client feedback
        return await GetServiceAccountResponseAsync(getServiceAccountQueryHandler, id, cancellationToken);
    }

    private static async Task<IResult> EnableServiceAccount(
        [FromServices] IEnableServiceAccountUseCase enableServiceAccountUseCase,
        [FromServices] IGetServiceAccountQueryHandler getServiceAccountQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new EnableServiceAccountCommand(new ServiceAccountId(id));
        Result result = await enableServiceAccountUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ServiceAccount.NotFound")
            {
                return TypedResults.NotFound(new { error = result.Error.Code, message = result.Error.Description });
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        // Return the updated resource for immediate client feedback
        return await GetServiceAccountResponseAsync(getServiceAccountQueryHandler, id, cancellationToken);
    }

    private static async Task<IResult> RotateSecret(
        [FromServices] IRotateSecretUseCase rotateSecretUseCase,
        Guid id,
        [FromBody] RotateSecretRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RotateSecretCommand(
            new ServiceAccountId(id),
            request.RevokeExisting,
            request.Description,
            request.ExpiresAt);

        Result<Application.ServiceAccounts.Commands.RotateSecretResponse> result = await rotateSecretUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ServiceAccount.NotFound")
            {
                return TypedResults.NotFound(new { error = result.Error.Code, message = result.Error.Description });
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        return TypedResults.Ok(new RotateSecretResponse(result.Value.CredentialId, result.Value.NewSecret));
    }

    private static async Task<IResult> AddCertificate(
        [FromServices] IAddCertificateUseCase addCertificateUseCase,
        Guid id,
        [FromBody] AddCertificateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddCertificateCommand(
            new ServiceAccountId(id),
            request.Thumbprint,
            request.Subject,
            request.ExpiresAt);

        Result<Application.ServiceAccounts.Commands.AddCertificateResponse> result = await addCertificateUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ServiceAccount.NotFound")
            {
                return TypedResults.NotFound(new { error = result.Error.Code, message = result.Error.Description });
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        return TypedResults.Created($"/api/admin/service-accounts/{id}", new AddCertificateResponse(result.Value.CertificateId));
    }

    /// <summary>
    /// Gets the service account response for the given ID.
    /// </summary>
    private static async Task<IResult> GetServiceAccountResponseAsync(
        IGetServiceAccountQueryHandler getServiceAccountQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result<ServiceAccountDetails> detailResult = await getServiceAccountQueryHandler.HandleAsync(new ServiceAccountId(id), cancellationToken);
        if (detailResult.IsFailure)
        {
            return TypedResults.NotFound();
        }

        ServiceAccountDetails detail = detailResult.Value;
        var response = new ServiceAccountResponse(
            detail.Id.Value,
            detail.ClientId,
            detail.DisplayName,
            detail.Status,
            detail.AllowedScopes,
            detail.AllowedGrantTypes,
            detail.CredentialCount,
            detail.CertificateCount,
            detail.CreatedAt,
            detail.ModifiedAt);

        return TypedResults.Ok(response);
    }
}
