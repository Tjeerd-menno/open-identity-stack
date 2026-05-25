using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Api.Common;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Api.Applications;

/// <summary>
/// Minimal API endpoints for managing unified applications via Admin API.
/// </summary>
internal static class ApplicationsApi
{
    public static IEndpointRouteBuilder MapApplicationsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/applications")
            .WithTags("Applications");

        group.MapPost(string.Empty, CreateApplication)
            .RequireAuthorization(Permissions.Applications.Write)
            .Produces<ApplicationCreatedResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateApplication")
            .WithSummary("Creates an application");

        group.MapGet(string.Empty, ListApplications)
            .RequireAuthorization(Permissions.Applications.Read)
            .Produces<ListApplicationsResponse>(StatusCodes.Status200OK)
            .WithName("ListApplications")
            .WithSummary("Lists applications");

        group.MapGet("policies/profiles", ListApplicationProfilePolicies)
            .RequireAuthorization(Permissions.Applications.Read)
            .Produces<IReadOnlyList<ApplicationProfilePolicyResponse>>(StatusCodes.Status200OK)
            .WithName("ListApplicationProfilePolicies")
            .WithSummary("Lists application profile policies");

        group.MapGet("{id:guid}", GetApplication)
            .RequireAuthorization(Permissions.Applications.Read)
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetApplication")
            .WithSummary("Gets application details");

        group.MapPatch("{id:guid}", UpdateApplicationMetadata)
            .RequireAuthorization(Permissions.Applications.Write)
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateApplicationMetadata")
            .WithSummary("Updates application metadata");

        group.MapPut("{id:guid}/oauth", ConfigureApplicationOAuth)
            .RequireAuthorization(Permissions.Applications.Write)
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("ConfigureApplicationOAuth")
            .WithSummary("Replaces application OAuth configuration");

        group.MapPost("{id:guid}/disable", DisableApplication)
            .RequireAuthorization(Permissions.Applications.Write)
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DisableApplication")
            .WithSummary("Disables an application");

        group.MapPost("{id:guid}/enable", EnableApplication)
            .RequireAuthorization(Permissions.Applications.Write)
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("EnableApplication")
            .WithSummary("Enables an application");

        group.MapDelete("{id:guid}", DeleteApplication)
            .RequireAuthorization(Permissions.Applications.Delete)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteApplication")
            .WithSummary("Deletes an application");

        group.MapGet("{id:guid}/credentials", ListApplicationCredentials)
            .RequireAuthorization(Permissions.Applications.Read)
            .Produces<IReadOnlyList<ApplicationCredentialResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("ListApplicationCredentials")
            .WithSummary("Lists application credential metadata");

        group.MapPost("{id:guid}/credentials/client-secrets", AddApplicationSecret)
            .RequireAuthorization(Permissions.Applications.ManageCredentials)
            .Produces<AddApplicationSecretResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("AddApplicationSecret")
            .WithSummary("Adds or rotates an application client secret");

        group.MapPost("{id:guid}/credentials/certificates", AddApplicationCertificate)
            .RequireAuthorization(Permissions.Applications.ManageCertificates)
            .Produces<AddApplicationCertificateResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("AddApplicationCertificate")
            .WithSummary("Adds an application certificate credential");

        group.MapDelete("{id:guid}/credentials/{credentialId:guid}", RevokeApplicationCredential)
            .RequireAuthorization(Permissions.Applications.ManageCredentials)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("RevokeApplicationCredential")
            .WithSummary("Revokes an application credential");

        return app;
    }

    private static async Task<IResult> CreateApplication(
        [FromServices] ICreateApplicationUseCase createApplicationUseCase,
        [FromServices] IGetApplicationQueryHandler getApplicationQueryHandler,
        [FromBody] CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateApplicationCommand(
            request.ClientId,
            request.DisplayName,
            request.Description,
            request.Profile,
            request.ClientType,
            request.AllowedGrantTypes,
            request.AllowedScopes,
            request.RedirectUris,
            request.PostLogoutRedirectUris,
            request.RequirePkce,
            request.RequireConsent);

        Result<ApplicationCommandResult> result = await createApplicationUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        Result<ApplicationDetails> detailsResult = await getApplicationQueryHandler.HandleAsync(result.Value.Id, cancellationToken);
        if (detailsResult.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(detailsResult.Error);
        }

        ApplicationDetails details = detailsResult.Value;
        var response = new ApplicationCreatedResponse(
            details.Id.Value,
            details.ClientId,
            details.DisplayName,
            details.Profile,
            details.ClientType,
            details.Status,
            InitialSecret: null,
            details.CreatedAt);

        return TypedResults.Created($"/api/admin/applications/{details.Id.Value}", response);
    }

    private static async Task<IResult> ListApplications(
        [FromServices] IListApplicationsQueryHandler listApplicationsQueryHandler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ApplicationProfile? profile = null,
        [FromQuery] ApplicationStatus? status = null,
        [FromQuery] OAuthClientType? clientType = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<ApplicationSummary>> result = await listApplicationsQueryHandler.HandleAsync(
            page,
            pageSize,
            profile,
            status,
            clientType,
            search,
            cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        PagedResult<ApplicationSummary> paged = result.Value;
        var items = paged.Items.Select(summary => new ApplicationListItemResponse(
            summary.Id.Value,
            summary.ClientId,
            summary.DisplayName,
            summary.Profile,
            summary.ClientType,
            summary.Status,
            AllowedGrantTypes: [],
            CredentialCount: 0,
            summary.CreatedAt,
            summary.ModifiedAt)).ToList();

        return TypedResults.Ok(new ListApplicationsResponse(
            items,
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages,
            paged.HasPreviousPage,
            paged.HasNextPage));
    }

    private static async Task<IResult> ListApplicationProfilePolicies(
        [FromServices] IListApplicationProfilePoliciesQueryHandler listApplicationProfilePoliciesQueryHandler,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ApplicationProfilePolicyDetails>> result =
            await listApplicationProfilePoliciesQueryHandler.HandleAsync(cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var responses = result.Value
            .Select(policy => new ApplicationProfilePolicyResponse(
                policy.ApplicationProfile,
                policy.IsSelectable,
                policy.UnavailabilityReason,
                policy.DefaultClientProfile,
                policy.AllowedClientProfiles,
                policy.AllowedGrantTypes,
                policy.DefaultGrantTypes,
                policy.Options,
                policy.RequirePkce,
                policy.DefaultRequirePkce,
                policy.DefaultRequireConsent,
                policy.RequiresRedirectUris))
            .ToList();

        return TypedResults.Ok(responses);
    }

    private static async Task<IResult> GetApplication(
        [FromServices] IGetApplicationQueryHandler getApplicationQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        return await GetApplicationResponseAsync(getApplicationQueryHandler, id, cancellationToken);
    }

    private static async Task<IResult> UpdateApplicationMetadata(
        [FromServices] IUpdateApplicationMetadataUseCase updateApplicationMetadataUseCase,
        [FromServices] IGetApplicationQueryHandler getApplicationQueryHandler,
        Guid id,
        [FromBody] UpdateApplicationMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateApplicationMetadataCommand(new DomainApplicationId(id), request.DisplayName, request.Description);
        Result<ApplicationCommandResult> result = await updateApplicationMetadataUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return await GetApplicationResponseAsync(getApplicationQueryHandler, id, cancellationToken);
    }

    private static async Task<IResult> ConfigureApplicationOAuth(
        [FromServices] IConfigureApplicationOAuthUseCase configureApplicationOAuthUseCase,
        [FromServices] IGetApplicationQueryHandler getApplicationQueryHandler,
        Guid id,
        [FromBody] ConfigureApplicationOAuthRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ConfigureApplicationOAuthCommand(
            new DomainApplicationId(id),
            request.Profile,
            request.ClientType,
            request.AllowedGrantTypes,
            request.AllowedScopes,
            request.RedirectUris,
            request.PostLogoutRedirectUris,
            request.RequirePkce,
            request.RequireConsent);

        Result<ApplicationCommandResult> result = await configureApplicationOAuthUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return await GetApplicationResponseAsync(getApplicationQueryHandler, id, cancellationToken);
    }

    private static async Task<IResult> DisableApplication(
        [FromServices] IDisableApplicationUseCase disableApplicationUseCase,
        [FromServices] IGetApplicationQueryHandler getApplicationQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result result = await disableApplicationUseCase.ExecuteAsync(new DisableApplicationCommand(new DomainApplicationId(id)), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return await GetApplicationResponseAsync(getApplicationQueryHandler, id, cancellationToken);
    }

    private static async Task<IResult> EnableApplication(
        [FromServices] IEnableApplicationUseCase enableApplicationUseCase,
        [FromServices] IGetApplicationQueryHandler getApplicationQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result result = await enableApplicationUseCase.ExecuteAsync(new EnableApplicationCommand(new DomainApplicationId(id)), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return await GetApplicationResponseAsync(getApplicationQueryHandler, id, cancellationToken);
    }

    private static async Task<IResult> DeleteApplication(
        [FromServices] IDeleteApplicationUseCase deleteApplicationUseCase,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result result = await deleteApplicationUseCase.ExecuteAsync(new DeleteApplicationCommand(new DomainApplicationId(id)), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ListApplicationCredentials(
        [FromServices] IListApplicationCredentialsQueryHandler listApplicationCredentialsQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ApplicationCredentialDetails>> result =
            await listApplicationCredentialsQueryHandler.HandleAsync(
                new ListApplicationCredentialsQuery(new DomainApplicationId(id)),
                cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(result.Value.Select(MapCredential).ToList());
    }

    private static async Task<IResult> AddApplicationSecret(
        [FromServices] IAddApplicationSecretUseCase addApplicationSecretUseCase,
        Guid id,
        [FromBody] AddApplicationSecretRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddApplicationSecretCommand(
            new DomainApplicationId(id),
            request.Description,
            request.ExpiresAt,
            request.RevokeExisting);
        Result<ApplicationCredentialCommandResult> result =
            await addApplicationSecretUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(new AddApplicationSecretResponse(
            result.Value.CredentialId,
            result.Value.OneTimeSecret ?? string.Empty));
    }

    private static async Task<IResult> AddApplicationCertificate(
        [FromServices] IAddApplicationCertificateUseCase addApplicationCertificateUseCase,
        Guid id,
        [FromBody] AddApplicationCertificateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddApplicationCertificateCommand(
            new DomainApplicationId(id),
            request.Thumbprint,
            request.Subject,
            request.Description,
            request.ExpiresAt);
        Result<ApplicationCredentialCommandResult> result =
            await addApplicationCertificateUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Created(
            $"/api/admin/applications/{id}/credentials/{result.Value.CredentialId}",
            new AddApplicationCertificateResponse(result.Value.CredentialId));
    }

    private static async Task<IResult> RevokeApplicationCredential(
        [FromServices] IRevokeApplicationCredentialUseCase revokeApplicationCredentialUseCase,
        Guid id,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        Result result = await revokeApplicationCredentialUseCase.ExecuteAsync(
            new RevokeApplicationCredentialCommand(new DomainApplicationId(id), credentialId),
            cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetApplicationResponseAsync(
        IGetApplicationQueryHandler getApplicationQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result<ApplicationDetails> result = await getApplicationQueryHandler.HandleAsync(new DomainApplicationId(id), cancellationToken);
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static ApplicationResponse MapToResponse(ApplicationDetails details) =>
        new(
            details.Id.Value,
            details.ClientId,
            details.DisplayName,
            details.Description,
            details.Profile,
            details.ClientType,
            details.Status,
            details.RedirectUris,
            details.PostLogoutRedirectUris,
            details.AllowedScopes,
            details.AllowedGrantTypes,
            details.RequirePkce,
            details.RequireConsent,
            CredentialCount: 0,
            CertificateCount: 0,
            details.RequiresMigrationReview,
            details.MigrationSource,
            details.CreatedAt,
            details.ModifiedAt);

    private static ApplicationCredentialResponse MapCredential(ApplicationCredentialDetails credential) =>
        new(
            credential.Id,
            credential.ApplicationId.Value,
            credential.Type,
            credential.Thumbprint,
            credential.Subject,
            credential.Description,
            credential.ExpiresAt,
            credential.CreatedAt,
            credential.LastUsedAt,
            credential.RevokedAt);
}
