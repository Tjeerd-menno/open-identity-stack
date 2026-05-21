using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServicePermissions.Validators;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.ServicePermissions.Commands;

public interface IRegisterServiceUseCase
{
    Task<Result<RegisterServiceResult>> ExecuteAsync(RegisterServiceCommand command, CancellationToken cancellationToken = default);
}

public sealed class RegisterServiceUseCase : IRegisterServiceUseCase
{
    private readonly IServicePermissionRegistryRepository repository;
    private readonly IServicePermissionAuthorizationService authorizationService;
    private readonly IServicePermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;

    internal RegisterServiceUseCase(IServicePermissionRegistryRepository repository, IDateTimeProvider dateTimeProvider)
        : this(repository, new AllowAllServicePermissionAuthorizationService(), new NoopServicePermissionAuditWriter(), dateTimeProvider)
    {
    }

    public RegisterServiceUseCase(
        IServicePermissionRegistryRepository repository,
        IServicePermissionAuthorizationService authorizationService,
        IServicePermissionAuditWriter auditWriter,
        IDateTimeProvider dateTimeProvider)
    {
        this.repository = repository;
        this.authorizationService = authorizationService;
        this.auditWriter = auditWriter;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<RegisterServiceResult>> ExecuteAsync(RegisterServiceCommand command, CancellationToken cancellationToken = default)
    {
        DomainError? validationError = RegisterServiceValidator.Validate(command);
        if (validationError is not null)
        {
            return validationError;
        }

        if (!await this.authorizationService.CanRegisterServiceAsync(command.ActorId, cancellationToken).ConfigureAwait(false))
        {
            await this.auditWriter.WriteAsync("RegisterService", command.ActorId, null, "Denied", cancellationToken).ConfigureAwait(false);
            return DomainError.Forbidden("RegisterService.Forbidden", "Actor cannot register service permissions.");
        }

        string normalizedIdentifier = command.ServiceIdentifier.Trim().ToLowerInvariant();
        bool exists = await this.repository.ExistsByIdentifierAsync(normalizedIdentifier, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            return DomainError.Conflict("RegisterService.IdentifierConflict", $"A service with identifier '{command.ServiceIdentifier}' already exists.");
        }

        if (!Enum.TryParse(command.OwnerType, ignoreCase: true, out OwnerType ownerType) || !Enum.IsDefined(ownerType))
        {
            return DomainError.Validation("RegisterService.OwnerTypeInvalid", "Owner type is invalid.");
        }

        IEnumerable<(string Key, string DisplayName, string? Description, string? IntendedUse, string? DocUrl)> permissions = command.Permissions.Select(p =>
            (p.PermissionKey, p.DisplayName, p.Description, p.IntendedUse, p.DocumentationUrl));

        Result<RegisteredService> serviceResult = RegisteredService.Register(
            command.ServiceIdentifier,
            command.DisplayName,
            command.Description,
            command.OwnerId,
            ownerType,
            permissions,
            command.ActorId,
            this.dateTimeProvider,
            ReservedServicePermissionNamespaces.ReservedIdentifiers);

        if (serviceResult.IsFailure)
        {
            return serviceResult.Error;
        }

        RegisteredService service = serviceResult.Value;

        await this.repository.AddAsync(service, cancellationToken).ConfigureAwait(false);
        await this.repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await this.auditWriter.WriteAsync("RegisterService", command.ActorId, service.Id.Value.ToString(), "Succeeded", cancellationToken).ConfigureAwait(false);

        return new RegisterServiceResult(service.Id.Value, service.ServiceIdentifier, service.Permissions.Count);
    }

    private sealed class AllowAllServicePermissionAuthorizationService : IServicePermissionAuthorizationService
    {
        public Task<bool> CanRegisterServiceAsync(string actorId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanManageServiceAsync(string actorId, string serviceOwnerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class NoopServicePermissionAuditWriter : IServicePermissionAuditWriter
    {
        public Task WriteAsync(string action, string actorId, string? serviceId, string? result, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
