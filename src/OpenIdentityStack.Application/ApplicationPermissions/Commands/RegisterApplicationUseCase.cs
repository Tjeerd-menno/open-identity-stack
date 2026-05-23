using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Commands;

public interface IRegisterApplicationUseCase
{
    Task<Result<RegisterApplicationResult>> ExecuteAsync(RegisterApplicationCommand command, CancellationToken cancellationToken = default);
}

public interface IRegisterPermissionManifestUseCase
{
    Task<Result<RegisterApplicationResult>> ExecuteAsync(RegisterPermissionManifestCommand command, CancellationToken cancellationToken = default);
}

public sealed class RegisterApplicationUseCase : IRegisterApplicationUseCase, IRegisterPermissionManifestUseCase
{
    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;

    internal RegisterApplicationUseCase(IApplicationPermissionRegistryRepository repository, IDateTimeProvider dateTimeProvider)
        : this(repository, new AllowAllApplicationPermissionAuthorizationService(), new NoopApplicationPermissionAuditWriter(), dateTimeProvider)
    {
    }

    public RegisterApplicationUseCase(
        IApplicationPermissionRegistryRepository repository,
        IApplicationPermissionAuthorizationService authorizationService,
        IApplicationPermissionAuditWriter auditWriter,
        IDateTimeProvider dateTimeProvider)
    {
        this.repository = repository;
        this.authorizationService = authorizationService;
        this.auditWriter = auditWriter;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<RegisterApplicationResult>> ExecuteAsync(RegisterApplicationCommand command, CancellationToken cancellationToken = default)
    {
        DomainError? validationError = RegisterApplicationValidator.Validate(command);
        if (validationError is not null)
        {
            return validationError;
        }

        if (!await this.authorizationService.CanRegisterApplicationAsync(command.ActorId, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync("RegisterApplication", command.ActorId, null, "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("RegisterApplication.Forbidden", "Actor cannot register application permissions.");
        }

        string normalizedIdentifier = command.ApplicationIdentifier.Trim().ToLowerInvariant();
        bool exists = await this.repository.ExistsByIdentifierAsync(normalizedIdentifier, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            return DomainError.Conflict("RegisterApplication.IdentifierConflict", $"An application with identifier '{command.ApplicationIdentifier}' already exists.");
        }

        if (!Enum.TryParse(command.OwnerType, ignoreCase: true, out OwnerType ownerType) || !Enum.IsDefined(ownerType))
        {
            return DomainError.Validation("RegisterApplication.OwnerTypeInvalid", "Owner type is invalid.");
        }

        IEnumerable<(string Key, string DisplayName, string? Description, string? IntendedUse, string? DocUrl)> permissions = command.Permissions.Select(p =>
            (p.PermissionKey, p.DisplayName, p.Description, p.IntendedUse, p.DocumentationUrl));

        Result<RegisteredApplication> applicationResult = RegisteredApplication.Register(
            command.ApplicationIdentifier,
            command.DisplayName,
            command.Description,
            command.OwnerId,
            ownerType,
            permissions,
            command.ActorId,
            this.dateTimeProvider,
            ReservedApplicationPermissionNamespaces.ReservedIdentifiers);

        if (applicationResult.IsFailure)
        {
            return applicationResult.Error;
        }

        RegisteredApplication application = applicationResult.Value;

        await this.repository.AddAsync(application, cancellationToken).ConfigureAwait(false);
        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("RegisterApplication", command.ActorId, application.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);

        return new RegisterApplicationResult(application.Id.Value, application.ApplicationIdentifier, application.Permissions.Count);
    }

    public async Task<Result<RegisterApplicationResult>> ExecuteAsync(RegisterPermissionManifestCommand command, CancellationToken cancellationToken = default)
    {
        var applicationCommand = new RegisterApplicationCommand(
            command.Application.Id,
            command.Application.Name,
            command.Application.Version,
            command.ActorId,
            OwnerType.User.ToString(),
            command.ActorId,
            command.Permissions.Select(permission => new RegisterApplicationPermissionInput(
                permission.Name,
                permission.Name,
                permission.Description,
                permission.Category,
                null)).ToList());

        return await this.ExecuteAsync(applicationCommand, cancellationToken).ConfigureAwait(false);
    }

    private sealed class AllowAllApplicationPermissionAuthorizationService : IApplicationPermissionAuthorizationService
    {
        public Task<bool> CanRegisterApplicationAsync(string actorId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanManageApplicationAsync(string actorId, string applicationOwnerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class NoopApplicationPermissionAuditWriter : IApplicationPermissionAuditWriter
    {
        public Task WriteAsync(string action, string actorId, string? applicationId, string? result, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
