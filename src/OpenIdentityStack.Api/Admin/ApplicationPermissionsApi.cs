using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Api.Admin.Requests.ApplicationPermissions;
using OpenIdentityStack.Api.Admin.Responses.ApplicationPermissions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Api.Admin;

internal static class ApplicationPermissionsApi
{
    public static IEndpointRouteBuilder MapApplicationPermissionsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/application-permissions")
            .WithTags(nameof(ApplicationPermissionsApi));

        group.MapPost("applications", RegisterApplicationManifest)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("RegisterApplicationPermissionManifest")
            .WithSummary("Registers an application permission manifest");

        group.MapPost("applications/{id:guid}/manifest/preview", PreviewApplicationManifest)
            .RequireAuthorization(Permissions.ApplicationPermissions.Read)
            .Produces<ManifestPreviewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("PreviewApplicationPermissionManifest")
            .WithSummary("Previews an inline application permission manifest update");

        group.MapPost("applications/{id:guid}/manifest", ApplyApplicationManifest)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<ManifestApplyDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("ApplyApplicationPermissionManifest")
            .WithSummary("Applies an inline application permission manifest update");

        group.MapPost("applications/{id:guid}/import/preview", PreviewRemoteApplicationManifest)
            .RequireAuthorization(Permissions.ApplicationPermissions.Read)
            .Produces<ManifestPreviewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("PreviewRemoteApplicationPermissionManifest")
            .WithSummary("Previews a remote application permission manifest update");

        group.MapPost("applications/{id:guid}/import", ApplyRemoteApplicationManifest)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<ManifestApplyDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("ApplyRemoteApplicationPermissionManifest")
            .WithSummary("Applies a remote application permission manifest update");

        group.MapPost("applications/import", ImportApplicationManifest)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<RegisterApplicationResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("ImportApplicationPermissionManifest")
            .WithSummary("Imports an application permission manifest from .well-known/permissions");

        group.MapGet("applications", ListApplications)
            .RequireAuthorization(Permissions.ApplicationPermissions.Read)
            .Produces<RegisteredApplicationListResponse>(StatusCodes.Status200OK)
            .WithName("ListRegisteredApplications")
            .WithSummary("Lists registered applications");

        group.MapGet("applications/{id:guid}", GetApplication)
            .RequireAuthorization(Permissions.ApplicationPermissions.Read)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetRegisteredApplication")
            .WithSummary("Gets a registered application");

        group.MapGet("catalog", ListCatalog)
            .RequireAuthorization(Permissions.ApplicationPermissions.Read)
            .Produces<AssignablePermissionCatalogResponse>(StatusCodes.Status200OK)
            .WithName("ListAssignablePermissionCatalog")
            .WithSummary("Lists assignable application permissions");

        group.MapPatch("applications/{id:guid}", UpdateApplication)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("UpdateRegisteredApplication")
            .WithSummary("Updates registered application metadata");

        group.MapPost("applications/{id:guid}/permissions", AddPermission)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("AddRegisteredApplicationPermission")
            .WithSummary("Adds a permission to a registered application");

        group.MapPatch("permissions/{permissionId:guid}", UpdatePermission)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("UpdateRegisteredApplicationPermission")
            .WithSummary("Updates registered permission metadata");

        group.MapGet("permissions/{permissionId:guid}/deletion-impact", GetPermissionDeletionImpact)
            .RequireAuthorization(Permissions.ApplicationPermissions.Read)
            .Produces<DeletionImpactDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetApplicationPermissionDeletionImpact")
            .WithSummary("Previews the impact of deleting a registered permission");

        group.MapDelete("permissions/{permissionId:guid}", DeletePermission)
            .RequireAuthorization(Permissions.ApplicationPermissions.Admin)
            .Produces<DestructiveOperationResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteApplicationPermission")
            .WithSummary("Deletes a registered permission and removes assignments");

        group.MapGet("applications/{id:guid}/deletion-impact", GetApplicationDeletionImpact)
            .RequireAuthorization(Permissions.ApplicationPermissions.Read)
            .Produces<DeletionImpactDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetRegisteredApplicationDeletionImpact")
            .WithSummary("Previews the impact of deleting a registered application");

        group.MapDelete("applications/{id:guid}", DeleteApplication)
            .RequireAuthorization(Permissions.ApplicationPermissions.Admin)
            .Produces<DestructiveOperationResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteRegisteredApplication")
            .WithSummary("Deletes a registered application and removes assignments");

        group.MapPost("applications/{id:guid}/lifecycle", ChangeApplicationLifecycle)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("ChangeRegisteredApplicationLifecycle")
            .WithSummary("Changes registered application lifecycle status");

        group.MapPost("applications/{id:guid}/disable", DisableApplication)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("DisableRegisteredApplication")
            .WithSummary("Disables a registered application");

        group.MapPost("applications/{id:guid}/enable", EnableApplication)
            .RequireAuthorization(Permissions.ApplicationPermissions.Write)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("EnableRegisteredApplication")
            .WithSummary("Enables a registered application");

        group.MapGet("permissions/{permissionId:guid}/dependencies", GetPermissionDependencies)
            .RequireAuthorization(Permissions.ApplicationPermissions.Read)
            .Produces<IReadOnlyList<RoleAssignmentDependency>>(StatusCodes.Status200OK)
            .WithName("GetRegisteredApplicationPermissionDependencies")
            .WithSummary("Lists role dependencies for a registered permission");

        group.MapPatch("permissions/{permissionId:guid}/replacement", UpdateRemovedPermissionReplacement)
            .RequireAuthorization(Permissions.ApplicationPermissions.Admin)
            .Produces<RemovedPermissionDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateRemovedApplicationPermissionReplacement")
            .WithSummary("Annotates removed permission replacement guidance");

        group.MapGet("history", ListApplicationPermissionHistory)
            .RequireAuthorization(Permissions.ApplicationPermissions.Admin)
            .Produces<ApplicationPermissionHistoryDto>(StatusCodes.Status200OK)
            .WithName("ListApplicationPermissionHistory")
            .WithSummary("Lists deleted application and removed permission history");

        group.MapGet("diagnostics", ListApplicationPermissionDiagnostics)
            .RequireAuthorization(Permissions.ApplicationPermissions.Admin)
            .Produces<PermissionDiagnosticsDto>(StatusCodes.Status200OK)
            .WithName("ListApplicationPermissionDiagnostics")
            .WithSummary("Lists application permission assignment diagnostics");

        group.MapPost("applications/{id:guid}/ownership", TransferOwnership)
            .RequireAuthorization(Permissions.ApplicationPermissions.Admin)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("TransferRegisteredApplicationOwnership")
            .WithSummary("Transfers registered application ownership");

        group.MapPost("applications/{id:guid}/maintainers", AddMaintainer)
            .RequireAuthorization(Permissions.ApplicationPermissions.Admin)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("AddRegisteredApplicationMaintainer")
            .WithSummary("Adds a delegated maintainer");

        group.MapDelete("applications/{id:guid}/maintainers/{principalId}", RemoveMaintainer)
            .RequireAuthorization(Permissions.ApplicationPermissions.Admin)
            .Produces<RegisteredApplicationDto>(StatusCodes.Status200OK)
            .WithName("RemoveRegisteredApplicationMaintainer")
            .WithSummary("Removes a delegated maintainer");

        return app;
    }

    private static async Task<IResult> RegisterApplicationManifest(
        [FromServices] ApplicationPermissionManifestUseCases manifestUseCases,
        HttpContext context,
        [FromBody] CreatePermissionManifestRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Manifest.Application is null)
        {
            return TypedResults.BadRequest(new { error = "PermissionManifest.ApplicationRequired", message = "Application is required." });
        }

        if (request.Manifest.Permissions is null)
        {
            return TypedResults.BadRequest(new { error = "PermissionManifest.PermissionsRequired", message = "Permissions are required." });
        }

        if (!Enum.TryParse(request.OwnerType, ignoreCase: true, out OwnerType ownerType))
        {
            return TypedResults.BadRequest(new { error = "PermissionManifest.OwnerTypeInvalid", message = "Owner type must be user or group." });
        }

        string actorId = GetActorId(context);
        var command = new CreateApplicationPermissionManifestCommand(
            ToManifestDocument(request.Manifest),
            request.OwnerId,
            ownerType,
            request.ManifestBaseUrl,
            actorId);

        Result<RegisteredApplicationDto> result = await manifestUseCases.CreateAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return ToErrorResult(result.Error);
        }

        return TypedResults.Created($"/api/admin/application-permissions/applications/{result.Value.Id}", result.Value);
    }

    private static async Task<IResult> PreviewApplicationManifest(
        [FromServices] ApplicationPermissionManifestUseCases manifestUseCases,
        HttpContext context,
        Guid id,
        [FromBody] ManifestUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ApplyApplicationPermissionManifestCommand(
            id,
            ToManifestDocument(request.Manifest),
            GetActorId(context),
            request.ConcurrencyToken);

        Result<ManifestPreviewDto> result = await manifestUseCases.PreviewChangesAsync(command, cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ApplyApplicationManifest(
        [FromServices] ApplicationPermissionManifestUseCases manifestUseCases,
        HttpContext context,
        Guid id,
        [FromBody] ManifestUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ApplyApplicationPermissionManifestCommand(
            id,
            ToManifestDocument(request.Manifest),
            GetActorId(context),
            request.ConcurrencyToken);

        Result<ManifestApplyDto> result = await manifestUseCases.ApplyChangesAsync(command, cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> PreviewRemoteApplicationManifest(
        [FromServices] ApplicationPermissionManifestUseCases manifestUseCases,
        HttpContext context,
        Guid id,
        [FromBody] RemoteImportRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RemoteApplicationPermissionManifestCommand(
            id,
            GetActorId(context),
            request.ConcurrencyToken);

        Result<ManifestPreviewDto> result = await manifestUseCases.PreviewRemoteChangesAsync(command, cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ApplyRemoteApplicationManifest(
        [FromServices] ApplicationPermissionManifestUseCases manifestUseCases,
        HttpContext context,
        Guid id,
        [FromBody] RemoteImportRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RemoteApplicationPermissionManifestCommand(
            id,
            GetActorId(context),
            request.ConcurrencyToken);

        Result<ManifestApplyDto> result = await manifestUseCases.ApplyRemoteChangesAsync(command, cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ImportApplicationManifest(
        [FromServices] ApplicationPermissionManifestUseCases manifestUseCases,
        [FromServices] IHttpClientFactory httpClientFactory,
        HttpContext context,
        [FromBody] ImportPermissionManifestRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateWellKnownPermissionsEndpoint(request.Endpoint, out Uri? endpoint))
        {
            return TypedResults.BadRequest(new { error = "PermissionManifest.EndpointInvalid", message = "Endpoint must be an absolute HTTP(S) URL ending in /.well-known/permissions." });
        }

        using HttpClient httpClient = httpClientFactory.CreateClient();
        using HttpResponseMessage response = await httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return TypedResults.BadRequest(new { error = "PermissionManifest.EndpointFetchFailed", message = "The permissions manifest endpoint could not be fetched." });
        }

        PermissionManifestRequest? manifest = await response.Content.ReadFromJsonAsync<PermissionManifestRequest>(cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return TypedResults.BadRequest(new { error = "PermissionManifest.InvalidJson", message = "The permissions manifest endpoint did not return a valid manifest." });
        }

        var createRequest = new CreatePermissionManifestRequest(manifest, GetActorId(context), OwnerType.User.ToString(), endpoint!.GetLeftPart(UriPartial.Authority));
        return await RegisterApplicationManifest(manifestUseCases, context, createRequest, cancellationToken).ConfigureAwait(false);
    }

    private static PermissionManifestDocument ToManifestDocument(PermissionManifestRequest request)
    {
        return new PermissionManifestDocument(
            request.SchemaVersion,
            new PermissionManifestApplicationDeclaration(
                request.Application.Id,
                request.Application.DisplayName,
                request.Application.Description,
                request.Application.Version),
            request.Permissions.Select(permission => new PermissionManifestPermissionDeclaration(
                permission.Key,
                permission.DisplayName,
                permission.Description,
                permission.Category)).ToList());
    }

    private static async Task<IResult> ListApplications(
        [FromServices] IListRegisteredApplicationsQueryHandler handler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? owner = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        PagedResult<RegisteredApplicationSummaryDto> result = await handler.HandleAsync(
            new ListRegisteredApplicationsQuery(page, pageSize, status, owner, search),
            cancellationToken);
        return TypedResults.Ok(new RegisteredApplicationListResponse(result.Items, result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }

    private static async Task<IResult> GetApplication(
        [FromServices] IGetRegisteredApplicationQueryHandler handler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result<RegisteredApplicationDto> result = await handler.HandleAsync(new GetRegisteredApplicationQuery(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ToErrorResult(result.Error);
    }

    private static async Task<IResult> ListCatalog(
        [FromServices] IListAssignablePermissionCatalogQueryHandler handler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? applicationIdentifier = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        PagedResult<ApplicationPermissionDto> result = await handler.HandleAsync(
            new ListAssignablePermissionCatalogQuery(page, pageSize, applicationIdentifier, search),
            cancellationToken);
        return TypedResults.Ok(new AssignablePermissionCatalogResponse(result.Items, result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }

    private static async Task<IResult> UpdateApplication(
        [FromServices] IUpdateRegisteredApplicationUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] UpdateRegisteredApplicationRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new UpdateRegisteredApplicationCommand(id, request.DisplayName, request.Description, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> AddPermission(
        [FromServices] IAddApplicationPermissionUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] AddApplicationPermissionRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new AddApplicationPermissionCommand(id, request.PermissionKey, request.DisplayName, request.Description, request.Category, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> UpdatePermission(
        [FromServices] IUpdateApplicationPermissionUseCase useCase,
        HttpContext context,
        Guid permissionId,
        [FromBody] UpdateApplicationPermissionRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new UpdateApplicationPermissionCommand(permissionId, request.DisplayName, request.Description, request.Category, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ChangeApplicationLifecycle(
        [FromServices] IChangeRegisteredApplicationLifecycleUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] ChangeApplicationLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Status, ignoreCase: true, out ApplicationLifecycleStatus status))
        {
            return TypedResults.BadRequest(new { error = "Invalid application lifecycle status." });
        }

        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new ChangeRegisteredApplicationLifecycleCommand(id, status, GetActorId(context), request.AcknowledgeDependencies, request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> GetPermissionDeletionImpact(
        [FromServices] IPreviewApplicationPermissionDeletionImpactUseCase useCase,
        HttpContext context,
        Guid permissionId,
        CancellationToken cancellationToken)
    {
        Result<DeletionImpactDto> result = await useCase.ExecuteAsync(
            new PreviewDeleteApplicationPermissionCommand(permissionId, GetActorId(context)),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> DeletePermission(
        [FromServices] IDeleteApplicationPermissionUseCase useCase,
        HttpContext context,
        Guid permissionId,
        [FromBody] DeleteApplicationPermissionRequest request,
        CancellationToken cancellationToken)
    {
        Result<DestructiveOperationResultDto> result = await useCase.ExecuteAsync(
            new DeleteApplicationPermissionCommand(permissionId, request.Reason, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> GetApplicationDeletionImpact(
        [FromServices] IPreviewRegisteredApplicationDeletionImpactUseCase useCase,
        HttpContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result<DeletionImpactDto> result = await useCase.ExecuteAsync(
            new PreviewDeleteRegisteredApplicationCommand(id, GetActorId(context)),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> DeleteApplication(
        [FromServices] IDeleteRegisteredApplicationUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] DeleteRegisteredApplicationRequest request,
        CancellationToken cancellationToken)
    {
        Result<DestructiveOperationResultDto> result = await useCase.ExecuteAsync(
            new DeleteRegisteredApplicationCommand(id, request.Reason, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> DisableApplication(
        [FromServices] IChangeRegisteredApplicationLifecycleUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] ApplicationLifecycleActionRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new ChangeRegisteredApplicationLifecycleCommand(id, ApplicationLifecycleStatus.Disabled, GetActorId(context), request.AcknowledgeDependencies, request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> EnableApplication(
        [FromServices] IChangeRegisteredApplicationLifecycleUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] ApplicationLifecycleActionRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new ChangeRegisteredApplicationLifecycleCommand(id, ApplicationLifecycleStatus.Active, GetActorId(context), request.AcknowledgeDependencies, request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> GetPermissionDependencies(
        [FromServices] IGetPermissionDependenciesQueryHandler handler,
        Guid permissionId,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<RoleAssignmentDependency>> result = await handler.HandleAsync(new GetPermissionDependenciesQuery(permissionId), cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> UpdateRemovedPermissionReplacement(
        [FromServices] IUpdateRemovedPermissionReplacementUseCase useCase,
        HttpContext context,
        Guid permissionId,
        [FromBody] ReplacementGuidanceRequest request,
        CancellationToken cancellationToken)
    {
        Result<RemovedPermissionDetailDto> result = await useCase.ExecuteAsync(
            new UpdateRemovedPermissionReplacementCommand(
                permissionId,
                request.ReplacementFullPermissionKey,
                request.ReplacementNote,
                GetActorId(context),
                request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ListApplicationPermissionHistory(
        [FromServices] IListApplicationPermissionHistoryQueryHandler handler,
        HttpContext context,
        [FromQuery] string? applicationIdentifier = null,
        [FromQuery] bool includeApplications = true,
        [FromQuery] bool includePermissions = true,
        CancellationToken cancellationToken = default)
    {
        Result<ApplicationPermissionHistoryDto> result = await handler.HandleAsync(
            new ListApplicationPermissionHistoryQuery(applicationIdentifier, includeApplications, includePermissions, GetActorId(context)),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ListApplicationPermissionDiagnostics(
        [FromServices] IListApplicationPermissionDiagnosticsQueryHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        Result<PermissionDiagnosticsDto> result = await handler.HandleAsync(
            new ListApplicationPermissionDiagnosticsQuery(GetActorId(context)),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> TransferOwnership(
        [FromServices] ITransferRegisteredApplicationOwnershipUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] TransferApplicationOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.OwnerType, ignoreCase: true, out OwnerType ownerType))
        {
            return TypedResults.BadRequest(new { error = "Invalid owner type." });
        }

        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new TransferRegisteredApplicationOwnershipCommand(id, request.OwnerId, ownerType, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> AddMaintainer(
        [FromServices] IAddDelegatedMaintainerUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] AddDelegatedMaintainerRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.PrincipalType, ignoreCase: true, out OwnerType principalType))
        {
            return TypedResults.BadRequest(new { error = "Invalid principal type." });
        }

        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new AddDelegatedMaintainerCommand(id, request.PrincipalId, principalType, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> RemoveMaintainer(
        [FromServices] IRemoveDelegatedMaintainerUseCase useCase,
        HttpContext context,
        Guid id,
        string principalId,
        [FromQuery] uint? concurrencyToken,
        CancellationToken cancellationToken)
    {
        Result<RegisteredApplicationDto> result = await useCase.ExecuteAsync(
            new RemoveDelegatedMaintainerCommand(id, principalId, GetActorId(context), concurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static string GetActorId(HttpContext context)
    {
        return context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";
    }

    private static bool TryCreateWellKnownPermissionsEndpoint(string endpoint, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        if (!parsed.AbsolutePath.EndsWith("/.well-known/permissions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static IResult ToErrorResult(DomainError error)
    {
        if (error.Code.StartsWith("NotFound.", StringComparison.Ordinal))
        {
            return TypedResults.NotFound(new { error = error.Code, message = error.Description });
        }

        if (error.Code.StartsWith("Conflict.", StringComparison.Ordinal))
        {
            return TypedResults.Conflict(new { error = error.Code, message = error.Description });
        }

        if (error.Code.StartsWith("Forbidden.", StringComparison.Ordinal))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: error.Code, detail: error.Description);
        }

        return TypedResults.BadRequest(new { error = error.Code, message = error.Description });
    }

    private static IResult ToResult<T>(Result<T> result)
    {
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ToErrorResult(result.Error);
    }
}
