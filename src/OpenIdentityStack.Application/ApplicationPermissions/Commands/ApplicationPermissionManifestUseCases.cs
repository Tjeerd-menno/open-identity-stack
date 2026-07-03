using System.Globalization;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.ApplicationPermissions;
using SharedKernel;

namespace OpenIdentityStack.Application.ApplicationPermissions.Commands;

public sealed class ApplicationPermissionManifestUseCases
{
    public static readonly DomainError VersionNotNewer = DomainError.Conflict(
        "PermissionManifest.VersionNotNewer",
        "Manifest version must be strictly newer than the current manifest version.");

    public static readonly DomainError DestructiveManifestChangeNotSupportedYet = DomainError.Conflict(
        "PermissionManifest.DestructiveManifestChangeNotSupportedYet",
        "This manifest omits existing permissions. Destructive manifest changes are not supported in this slice.");

    public static readonly DomainError ManualApplicationCannotBeManifestBacked = DomainError.Validation(
        "PermissionManifest.ManualApplicationCannotBeManifestBacked",
        "Manually registered applications cannot be backed by a permission manifest.");

    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IPermissionAssignmentStore permissionAssignmentStore;
    private readonly IApplicationPermissionTransactionRunner transactionRunner;
    private readonly IRemotePermissionManifestFetcher? remoteManifestFetcher;

    public ApplicationPermissionManifestUseCases(
        IApplicationPermissionRegistryRepository repository,
        IApplicationPermissionAuthorizationService authorizationService,
        IApplicationPermissionAuditWriter auditWriter,
        IDateTimeProvider dateTimeProvider,
        IPermissionAssignmentStore permissionAssignmentStore,
        IApplicationPermissionTransactionRunner transactionRunner,
        IRemotePermissionManifestFetcher? remoteManifestFetcher = null)
    {
        this.repository = repository;
        this.authorizationService = authorizationService;
        this.auditWriter = auditWriter;
        this.dateTimeProvider = dateTimeProvider;
        this.permissionAssignmentStore = permissionAssignmentStore;
        this.transactionRunner = transactionRunner;
        this.remoteManifestFetcher = remoteManifestFetcher;
    }

    public async Task<Result<RegisteredApplicationDto>> CreateAsync(CreateApplicationPermissionManifestCommand command, CancellationToken cancellationToken = default)
    {
        Result validation = ApplicationPermissionManifestValidator.Validate(command.Manifest, command.ManifestBaseUrl);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        if (!command.IsImported && !string.IsNullOrWhiteSpace(command.ManifestBaseUrl))
        {
            return ManualApplicationCannotBeManifestBacked;
        }

        if (!await this.authorizationService.CanRegisterApplicationAsync(command.ActorId, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync("CreateApplicationPermissionManifest", command.ActorId, null, "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("PermissionManifest.Forbidden", "Actor cannot register application permissions.");
        }

        string normalizedIdentifier = command.Manifest.Application.Id.Trim().ToLowerInvariant();
        if (await this.repository.ExistsByIdentifierAsync(normalizedIdentifier, cancellationToken).ConfigureAwait(false))
        {
            return DomainError.Conflict("PermissionManifest.IdentifierConflict", $"An application with identifier '{normalizedIdentifier}' already exists.");
        }

        string? normalizedManifestBaseUrl = NormalizeManifestBaseUrl(command.ManifestBaseUrl);
        if (!string.IsNullOrEmpty(normalizedManifestBaseUrl)
            && await this.repository.ExistsByManifestBaseUrlAsync(normalizedManifestBaseUrl, cancellationToken).ConfigureAwait(false))
        {
            return DomainError.Conflict("PermissionManifest.ManifestBaseUrlConflict", "Manifest base URL is already trusted by another registered application.");
        }

        Result<RegisteredApplication> applicationResult = RegisteredApplication.Register(
            normalizedIdentifier,
            command.Manifest.Application.DisplayName,
            command.Manifest.Application.Description,
            command.OwnerId,
            command.OwnerType,
            command.Manifest.Permissions.Select(permission => (permission.Key, permission.DisplayName, permission.Description, permission.Category)),
            command.ActorId,
            this.dateTimeProvider,
            schemaVersion: command.Manifest.SchemaVersion,
            manifestVersion: command.Manifest.Application.Version,
            manifestBaseUrl: normalizedManifestBaseUrl);

        if (applicationResult.IsFailure)
        {
            return applicationResult.Error;
        }

        RegisteredApplication application = applicationResult.Value;
        await this.repository.AddAsync(application, cancellationToken).ConfigureAwait(false);
        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("CreateApplicationPermissionManifest", command.ActorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
        return MapToDto(application);
    }

    public async Task<Result<RegisteredApplicationDto>> GetDetailAsync(GetRegisteredApplicationQuery query, CancellationToken cancellationToken = default)
    {
        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(query.ApplicationId), cancellationToken).ConfigureAwait(false);
        return application is null
            ? DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{query.ApplicationId}' not found.")
            : MapToDto(application);
    }

    public async Task<Result<RegisteredApplicationDto>> ApplyAsync(ApplyApplicationPermissionManifestCommand command, CancellationToken cancellationToken = default)
    {
        Result<RegisteredApplication> updateValidation = await this.ValidateManifestUpdateAsync(command, cancellationToken).ConfigureAwait(false);
        if (updateValidation.IsFailure)
        {
            return updateValidation.Error;
        }

        RegisteredApplication application = updateValidation.Value;

        foreach (PermissionManifestPermissionDeclaration requestedPermission in command.Manifest.Permissions)
        {
            ApplicationPermission? existing = application.Permissions.FirstOrDefault(permission => permission.PermissionKey == requestedPermission.Key);
            if (existing is null)
            {
                Result<ApplicationPermission> addResult = application.AddPermission(
                    requestedPermission.Key,
                    requestedPermission.DisplayName,
                    requestedPermission.Description,
                    requestedPermission.Category,
                    command.ActorId,
                    this.dateTimeProvider);
                if (addResult.IsFailure)
                {
                    return addResult.Error;
                }
            }
            else
            {
                Result updateResult = application.UpdatePermission(
                    existing.Id,
                    requestedPermission.DisplayName,
                    requestedPermission.Description,
                    requestedPermission.Category,
                    command.ActorId,
                    this.dateTimeProvider);
                if (updateResult.IsFailure)
                {
                    return updateResult.Error;
                }
            }
        }

        Result metadataResult = application.ApplyManifestMetadata(
            command.Manifest.Application.DisplayName,
            command.Manifest.Application.Description,
            command.Manifest.SchemaVersion,
            command.Manifest.Application.Version,
            command.ActorId,
            this.dateTimeProvider);
        if (metadataResult.IsFailure)
        {
            return metadataResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("ApplyApplicationPermissionManifest", command.ActorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);
        return MapToDto(application);
    }

    public async Task<Result<ManifestPreviewDto>> PreviewChangesAsync(ApplyApplicationPermissionManifestCommand command, CancellationToken cancellationToken = default)
    {
        Result<ManifestChangePlan> planResult = await this.CreateManifestChangePlanAsync(command, cancellationToken).ConfigureAwait(false);
        if (planResult.IsFailure)
        {
            return planResult.Error;
        }

        ManifestChangePlan plan = planResult.Value;
        IReadOnlyList<PermissionAssignmentImpactDto> impacts = plan.Removals.Count == 0
            ? []
            : await this.permissionAssignmentStore.PreviewRemovalImpactAsync(plan.AssignmentRemovalPlan, cancellationToken).ConfigureAwait(false);

        return new ManifestPreviewDto(
            plan.Application.Id.Value,
            plan.Application.ManifestVersion,
            command.Manifest.Application.Version,
            plan.Additions.Count > 0 || plan.MetadataUpdates.Count > 0 || plan.Removals.Count > 0,
            true,
            plan.Removals.Count > 0,
            plan.Additions.Select(permission => ToPermissionDto(plan.Application, permission)).ToList(),
            plan.MetadataUpdates.Select(permission => ToPermissionDto(plan.Application, permission)).ToList(),
            plan.Removals.Select(permission => ToPermissionDto(plan.Application, permission)).ToList(),
            impacts);
    }

    public async Task<Result<ManifestApplyDto>> ApplyChangesAsync(ApplyApplicationPermissionManifestCommand command, CancellationToken cancellationToken = default)
    {
        return await this.transactionRunner
            .ExecuteAsync(token => this.ApplyChangesCoreAsync(command, token), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<ManifestPreviewDto>> PreviewRemoteChangesAsync(RemoteApplicationPermissionManifestCommand command, CancellationToken cancellationToken = default)
    {
        Result<ApplyApplicationPermissionManifestCommand> fetchedCommand = await this.CreateRemoteApplyCommandAsync(command, cancellationToken).ConfigureAwait(false);
        return fetchedCommand.IsFailure
            ? fetchedCommand.Error
            : await this.PreviewChangesAsync(fetchedCommand.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ManifestApplyDto>> ApplyRemoteChangesAsync(RemoteApplicationPermissionManifestCommand command, CancellationToken cancellationToken = default)
    {
        Result<ApplyApplicationPermissionManifestCommand> fetchedCommand = await this.CreateRemoteApplyCommandAsync(command, cancellationToken).ConfigureAwait(false);
        return fetchedCommand.IsFailure
            ? fetchedCommand.Error
            : await this.ApplyChangesAsync(fetchedCommand.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ApplyApplicationPermissionManifestCommand>> CreateRemoteApplyCommandAsync(
        RemoteApplicationPermissionManifestCommand command,
        CancellationToken cancellationToken)
    {
        if (this.remoteManifestFetcher is null)
        {
            return DomainError.Validation("PermissionManifest.RemoteFetcherUnavailable", "Remote manifest fetcher is not configured.");
        }

        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(command.ApplicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{command.ApplicationId}' not found.");
        }

        if (!await this.authorizationService.CanManageApplicationAsync(command.ActorId, application, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync("ApplyApplicationPermissionManifest", command.ActorId, application.Id.Value.ToString(), "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("PermissionManifest.Forbidden", "Actor cannot manage this registered application.");
        }

        if (command.ExpectedConcurrencyToken.HasValue && application.ConcurrencyToken != command.ExpectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("PermissionManifest.ConcurrencyConflict", "The registered application was modified by another request.");
        }

        if (string.IsNullOrWhiteSpace(application.ManifestBaseUrl))
        {
            return DomainError.Validation("PermissionManifest.ManifestBaseUrlRequired", "A trusted manifest base URL is required for remote import.");
        }

        Result<PermissionManifestDocument> manifestResult = await this.remoteManifestFetcher
            .FetchAsync(application.ManifestBaseUrl, application.ApplicationIdentifier, cancellationToken)
            .ConfigureAwait(false);
        if (manifestResult.IsFailure)
        {
            return manifestResult.Error;
        }

        return new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            manifestResult.Value,
            command.ActorId,
            command.ExpectedConcurrencyToken);
    }

    private async Task<Result<ManifestApplyDto>> ApplyChangesCoreAsync(ApplyApplicationPermissionManifestCommand command, CancellationToken cancellationToken)
    {
        Result<ManifestChangePlan> planResult = await this.CreateManifestChangePlanAsync(command, cancellationToken).ConfigureAwait(false);
        if (planResult.IsFailure)
        {
            return planResult.Error;
        }

        ManifestChangePlan plan = planResult.Value;
        IReadOnlyList<PermissionAssignmentImpactDto> assignmentImpacts = [];
        if (plan.Removals.Count > 0)
        {
            Result<IReadOnlyList<PermissionAssignmentImpactDto>> assignmentResult = await this.permissionAssignmentStore
                .RemoveAssignmentsAsync(plan.AssignmentRemovalPlan, command.ActorId, cancellationToken)
                .ConfigureAwait(false);
            if (assignmentResult.IsFailure)
            {
                return assignmentResult.Error;
            }

            assignmentImpacts = assignmentResult.Value;
        }

        foreach (ApplicationPermission removal in plan.Removals)
        {
            Result removeResult = plan.Application.RemovePermission(
                removal.Id,
                command.ActorId,
                $"Manifest {command.Manifest.Application.Version} omitted permission.",
                this.dateTimeProvider);
            if (removeResult.IsFailure)
            {
                return removeResult.Error;
            }
        }

        foreach (PermissionManifestPermissionDeclaration requestedPermission in command.Manifest.Permissions)
        {
            ApplicationPermission? existing = plan.Application.Permissions.FirstOrDefault(permission =>
                !permission.IsRemoved && permission.PermissionKey == requestedPermission.Key);
            if (existing is null)
            {
                Result<ApplicationPermission> addResult = plan.Application.AddPermission(
                    requestedPermission.Key,
                    requestedPermission.DisplayName,
                    requestedPermission.Description,
                    requestedPermission.Category,
                    command.ActorId,
                    this.dateTimeProvider);
                if (addResult.IsFailure)
                {
                    return addResult.Error;
                }
            }
            else
            {
                Result updateResult = plan.Application.UpdatePermission(
                    existing.Id,
                    requestedPermission.DisplayName,
                    requestedPermission.Description,
                    requestedPermission.Category,
                    command.ActorId,
                    this.dateTimeProvider);
                if (updateResult.IsFailure)
                {
                    return updateResult.Error;
                }
            }
        }

        Result metadataResult = plan.Application.ApplyManifestMetadata(
            command.Manifest.Application.DisplayName,
            command.Manifest.Application.Description,
            command.Manifest.SchemaVersion,
            command.Manifest.Application.Version,
            command.ActorId,
            this.dateTimeProvider);
        if (metadataResult.IsFailure)
        {
            return metadataResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("ApplyApplicationPermissionManifest", command.ActorId, plan.Application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);

        var removedDtos = plan.Removals.Select(permission => ToPermissionDto(plan.Application, permission)).ToList();
        var operationResult = new DestructiveOperationResultDto(
            removedDtos,
            assignmentImpacts.Where(static impact => impact.ImpactKind == "exactRemoved").ToList(),
            assignmentImpacts.Where(static impact => impact.ImpactKind == "wildcardRemoved").ToList(),
            assignmentImpacts.Where(static impact => impact.ImpactKind == "wildcardImpacted").ToList(),
            plan.MetadataUpdates.Count > 0,
            true);

        return new ManifestApplyDto(MapToDto(plan.Application), operationResult);
    }

    public async Task<Result<RegisteredApplicationDto>> PreviewAsync(ApplyApplicationPermissionManifestCommand command, CancellationToken cancellationToken = default)
    {
        Result<RegisteredApplication> updateValidation = await this.ValidateManifestUpdateAsync(command, cancellationToken).ConfigureAwait(false);
        return updateValidation.IsSuccess ? MapToDto(updateValidation.Value) : updateValidation.Error;
    }

    private async Task<Result<RegisteredApplication>> ValidateManifestUpdateAsync(ApplyApplicationPermissionManifestCommand command, CancellationToken cancellationToken)
    {
        Result<RegisteredApplication> preconditions = await this.ValidateManifestUpdatePreconditionsAsync(
            command,
            allowEmptyPermissions: false,
            cancellationToken).ConfigureAwait(false);
        if (preconditions.IsFailure)
        {
            return preconditions.Error;
        }

        RegisteredApplication application = preconditions.Value;
        var requestedKeys = command.Manifest.Permissions.Select(permission => permission.Key).ToHashSet(StringComparer.Ordinal);
        if (application.Permissions.Any(permission => !permission.IsRemoved && !requestedKeys.Contains(permission.PermissionKey)))
        {
            return DestructiveManifestChangeNotSupportedYet;
        }

        return application;
    }

    private async Task<Result<ManifestChangePlan>> CreateManifestChangePlanAsync(ApplyApplicationPermissionManifestCommand command, CancellationToken cancellationToken)
    {
        Result<RegisteredApplication> preconditions = await this.ValidateManifestUpdatePreconditionsAsync(
            command,
            allowEmptyPermissions: true,
            cancellationToken).ConfigureAwait(false);
        if (preconditions.IsFailure)
        {
            return preconditions.Error;
        }

        RegisteredApplication application = preconditions.Value;
        var requestedKeys = command.Manifest.Permissions.Select(permission => permission.Key).ToHashSet(StringComparer.Ordinal);
        var activePermissions = application.Permissions.Where(static permission => !permission.IsRemoved).ToList();
        var removals = activePermissions.Where(permission => !requestedKeys.Contains(permission.PermissionKey)).ToList();
        var permissionCreateResults = command.Manifest.Permissions
            .Where(permission => activePermissions.All(existing => existing.PermissionKey != permission.Key))
            .Select(permission => ApplicationPermission.Create(
                application.Id,
                application.ApplicationIdentifier,
                permission.Key,
                permission.DisplayName,
                permission.Description,
                permission.Category,
                command.ActorId,
                this.dateTimeProvider))
            .ToList();

        Result<ApplicationPermission>? firstFailure = permissionCreateResults.FirstOrDefault(result => result.IsFailure);
        if (firstFailure is not null && firstFailure.IsFailure)
        {
            return firstFailure.Error;
        }

        var additions = permissionCreateResults
            .Select(permissionResult => permissionResult.Value)
            .ToList();
        var metadataUpdates = command.Manifest.Permissions
            .Select(permission => activePermissions.FirstOrDefault(existing => existing.PermissionKey == permission.Key))
            .Where(static permission => permission is not null)
            .Cast<ApplicationPermission>()
            .ToList();

        var remainingAfterRemoval = activePermissions.Except(removals).ToList();
        var collapsedWildcards = removals
            .Select(permission => permission.PermissionKey.Split(':', 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(resource => !remainingAfterRemoval.Any(permission => permission.PermissionKey.StartsWith($"{resource}:", StringComparison.OrdinalIgnoreCase)))
            .Select(resource => $"{application.ApplicationIdentifier}:{resource}:*")
            .ToList();

        var removalPlan = new PermissionAssignmentRemovalPlan(
            removals.Select(permission => permission.FullPermissionKey).ToList(),
            collapsedWildcards);

        return new ManifestChangePlan(application, additions, metadataUpdates, removals, removalPlan);
    }

    private async Task<Result<RegisteredApplication>> ValidateManifestUpdatePreconditionsAsync(
        ApplyApplicationPermissionManifestCommand command,
        bool allowEmptyPermissions,
        CancellationToken cancellationToken)
    {
        Result validation = ApplicationPermissionManifestValidator.Validate(command.Manifest, null, allowEmptyPermissions);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        RegisteredApplication? application = await this.repository.GetByIdAsync(new RegisteredApplicationId(command.ApplicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return DomainError.NotFound("RegisteredApplication.NotFound", $"Application '{command.ApplicationId}' not found.");
        }

        if (!await this.authorizationService.CanManageApplicationAsync(command.ActorId, application, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync("ApplyApplicationPermissionManifest", command.ActorId, application.Id.Value.ToString(), "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("PermissionManifest.Forbidden", "Actor cannot manage this registered application.");
        }

        if (command.ExpectedConcurrencyToken.HasValue && application.ConcurrencyToken != command.ExpectedConcurrencyToken.Value)
        {
            return DomainError.Conflict("PermissionManifest.ConcurrencyConflict", "The registered application was modified by another request.");
        }

        if (string.IsNullOrWhiteSpace(application.ManifestBaseUrl))
        {
            return DomainError.Validation("PermissionManifest.ManifestBaseUrlRequired", "A trusted manifest base URL is required for manifest updates.");
        }

        if (CompareSemVer(command.Manifest.Application.Version, application.ManifestVersion) <= 0)
        {
            return VersionNotNewer;
        }

        return application;
    }

    private static int CompareSemVer(string left, string right)
    {
        var parsedLeft = ParsedSemVer.Parse(left);
        var parsedRight = ParsedSemVer.Parse(right);

        int coreComparison = parsedLeft.Major != parsedRight.Major
            ? parsedLeft.Major.CompareTo(parsedRight.Major)
            : parsedLeft.Minor != parsedRight.Minor
                ? parsedLeft.Minor.CompareTo(parsedRight.Minor)
                : parsedLeft.Patch.CompareTo(parsedRight.Patch);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        if (parsedLeft.Prerelease is null && parsedRight.Prerelease is null)
        {
            return 0;
        }

        if (parsedLeft.Prerelease is null)
        {
            return 1;
        }

        if (parsedRight.Prerelease is null)
        {
            return -1;
        }

        return CompareSemVerPreRelease(parsedLeft.Prerelease, parsedRight.Prerelease);
    }

    private static int CompareSemVerPreRelease(string? left, string? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        string[] parsedLeft = left?.Split('.', StringSplitOptions.None) ?? Array.Empty<string>();
        string[] parsedRight = right?.Split('.', StringSplitOptions.None) ?? Array.Empty<string>();

        int maxLength = Math.Min(parsedLeft.Length, parsedRight.Length);
        for (int i = 0; i < maxLength; i++)
        {
            int segmentComparison = CompareSemVerIdentifier(parsedLeft[i], parsedRight[i]);
            if (segmentComparison != 0)
            {
                return segmentComparison;
            }
        }

        return parsedLeft.Length.CompareTo(parsedRight.Length);
    }

    private static int CompareSemVerIdentifier(string left, string right)
    {
        bool leftIsNumeric = int.TryParse(left, out int leftValue);
        bool rightIsNumeric = int.TryParse(right, out int rightValue);

        if (leftIsNumeric && rightIsNumeric)
        {
            return leftValue.CompareTo(rightValue);
        }

        if (leftIsNumeric)
        {
            return -1;
        }

        if (rightIsNumeric)
        {
            return 1;
        }

        return string.Compare(left, right, StringComparison.Ordinal);
    }

    private static string? NormalizeManifestBaseUrl(string? manifestBaseUrl)
    {
        return string.IsNullOrWhiteSpace(manifestBaseUrl)
            ? null
            : manifestBaseUrl.Trim().TrimEnd('/');
    }

    private static RegisteredApplicationDto MapToDto(RegisteredApplication application)
    {
        var permissions = application.Permissions.Where(static p => !p.IsRemoved).Select(p => new ApplicationPermissionDto(
            p.Id.Value,
            p.PermissionKey,
            p.FullPermissionKey,
            p.DisplayName,
            p.Description,
            p.Category,
            p.CreatedAt,
            p.ModifiedAt,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.ManifestVersion)).ToList();

        var maintainers = application.Maintainers.Select(m => new DelegatedMaintainerDto(
            m.Id.Value,
            m.PrincipalId,
            m.PrincipalType.ToString(),
            m.GrantedBy,
            m.GrantedAt)).ToList();

        return new RegisteredApplicationDto(
            application.Id.Value,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.Description,
            application.OwnerId,
            application.OwnerType.ToString(),
            application.Status.ToString(),
            application.CreatedAt,
            application.ModifiedAt,
            application.ConcurrencyToken,
            permissions,
            maintainers,
            application.SchemaVersion,
            application.ManifestVersion,
            application.ManifestBaseUrl);
    }

    private static ApplicationPermissionDto ToPermissionDto(RegisteredApplication application, ApplicationPermission permission)
    {
        return new ApplicationPermissionDto(
            permission.Id.Value,
            permission.PermissionKey,
            permission.FullPermissionKey,
            permission.DisplayName,
            permission.Description,
            permission.Category,
            permission.CreatedAt,
            permission.ModifiedAt,
            application.ApplicationIdentifier,
            application.DisplayName,
            application.ManifestVersion);
    }

    private sealed record ManifestChangePlan(
        RegisteredApplication Application,
        IReadOnlyList<ApplicationPermission> Additions,
        IReadOnlyList<ApplicationPermission> MetadataUpdates,
        IReadOnlyList<ApplicationPermission> Removals,
        PermissionAssignmentRemovalPlan AssignmentRemovalPlan);

    private readonly record struct ParsedSemVer(int Major, int Minor, int Patch, string? Prerelease)
    {
        public static ParsedSemVer Parse(string value)
        {
            string[] versionAndPrerelease = value.Split('-', 2);
            string[] core = versionAndPrerelease[0].Split('.');
            return new ParsedSemVer(
                int.Parse(core[0], CultureInfo.InvariantCulture),
                int.Parse(core[1], CultureInfo.InvariantCulture),
                int.Parse(core[2], CultureInfo.InvariantCulture),
                versionAndPrerelease.Length == 2 ? versionAndPrerelease[1] : null);
        }
    }
}
