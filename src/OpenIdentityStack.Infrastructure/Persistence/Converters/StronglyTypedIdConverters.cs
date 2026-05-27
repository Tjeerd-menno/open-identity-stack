using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;
namespace OpenIdentityStack.Infrastructure.Persistence.Converters;

/// <summary>
/// EF Core value converter for UserId.
/// </summary>
public class UserIdConverter : ValueConverter<UserId, Guid>
{
    public UserIdConverter() : base(id => id.Value, guid => new UserId(guid)) { }
}

/// <summary>
/// EF Core value converter for RoleId.
/// </summary>
public class RoleIdConverter : ValueConverter<RoleId, Guid>
{
    public RoleIdConverter() : base(id => id.Value, guid => new RoleId(guid)) { }
}

/// <summary>
/// EF Core value converter for GroupId.
/// </summary>
public class GroupIdConverter : ValueConverter<GroupId, Guid>
{
    public GroupIdConverter() : base(id => id.Value, guid => new GroupId(guid)) { }
}

/// <summary>
/// EF Core value converter for ServiceAccountId.
/// </summary>
public class ServiceAccountIdConverter : ValueConverter<ServiceAccountId, Guid>
{
    public ServiceAccountIdConverter() : base(id => id.Value, guid => new ServiceAccountId(guid)) { }
}

/// <summary>
/// EF Core value converter for SessionId.
/// </summary>
public class SessionIdConverter : ValueConverter<SessionId, Guid>
{
    public SessionIdConverter() : base(id => id.Value, guid => new SessionId(guid)) { }
}

/// <summary>
/// EF Core value converter for ProviderId.
/// </summary>
public class ProviderIdConverter : ValueConverter<ProviderId, Guid>
{
    public ProviderIdConverter() : base(id => id.Value, guid => new ProviderId(guid)) { }
}

/// <summary>
/// EF Core value converter for UpstreamProviderId.
/// </summary>
public class UpstreamProviderIdConverter : ValueConverter<UpstreamProviderId, Guid>
{
    public UpstreamProviderIdConverter() : base(id => id.Value, guid => new UpstreamProviderId(guid)) { }
}

/// <summary>
/// EF Core value converter for ClientId.
/// </summary>
public class ClientIdConverter : ValueConverter<ClientId, Guid>
{
    public ClientIdConverter() : base(id => id.Value, guid => new ClientId(guid)) { }
}

/// <summary>
/// EF Core value converter for ApplicationId.
/// </summary>
public class ApplicationIdConverter : ValueConverter<DomainApplicationId, Guid>
{
    public ApplicationIdConverter() : base(id => id.Value, guid => new DomainApplicationId(guid)) { }
}

/// <summary>
/// EF Core value converter for AuthenticationSettingsId.
/// </summary>
public class AuthenticationSettingsIdConverter : ValueConverter<AuthenticationSettingsId, Guid>
{
    public AuthenticationSettingsIdConverter() : base(id => id.Value, guid => new AuthenticationSettingsId(guid)) { }
}

/// <summary>
/// EF Core value converter for RegisteredApplicationId.
/// </summary>
public class RegisteredApplicationIdConverter : ValueConverter<RegisteredApplicationId, Guid>
{
    public RegisteredApplicationIdConverter() : base(id => id.Value, guid => new RegisteredApplicationId(guid)) { }
}

/// <summary>
/// EF Core value converter for ApplicationPermissionId.
/// </summary>
public class ApplicationPermissionIdConverter : ValueConverter<ApplicationPermissionId, Guid>
{
    public ApplicationPermissionIdConverter() : base(id => id.Value, guid => new ApplicationPermissionId(guid)) { }
}

/// <summary>
/// EF Core value converter for DelegatedMaintainerId.
/// </summary>
public class DelegatedMaintainerIdConverter : ValueConverter<DelegatedMaintainerId, Guid>
{
    public DelegatedMaintainerIdConverter() : base(id => id.Value, guid => new DelegatedMaintainerId(guid)) { }
}
