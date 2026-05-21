using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Api.Admin.Requests;
using OpenIdentityStack.Api.Admin.Requests.ServicePermissions;
using OpenIdentityStack.Api.Admin.Responses;
using OpenIdentityStack.Api.Admin.Responses.ServicePermissions;
using OpenIdentityStack.Api.Clients;
using OpenIdentityStack.Api.Federation;
using OpenIdentityStack.Api.Groups;
using OpenIdentityStack.Api.ServiceAccounts;
using OpenIdentityStack.Api.Sessions;
using OpenIdentityStack.Api.Settings;
using OpenIdentityStack.Api.Users;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.ServicePermissions.Dtos;

namespace OpenIdentityStack.Api.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(ValidationProblemDetails))]
[JsonSerializable(typeof(AddCertificateRequest))]
[JsonSerializable(typeof(AddCertificateResponse))]
[JsonSerializable(typeof(AddMappingRequest))]
[JsonSerializable(typeof(AddPermissionRequest))]
[JsonSerializable(typeof(AssignablePermissionCatalogResponse))]
[JsonSerializable(typeof(AuthenticationProviderResponse))]
[JsonSerializable(typeof(AuthenticationSettingsResponse))]
[JsonSerializable(typeof(ClientCreatedResponse))]
[JsonSerializable(typeof(ClientListItemResponse))]
[JsonSerializable(typeof(ClientResponse))]
[JsonSerializable(typeof(CreateClientRequest))]
[JsonSerializable(typeof(CreateGroupRequest))]
[JsonSerializable(typeof(CreateProviderRequest))]
[JsonSerializable(typeof(CreateRoleRequest))]
[JsonSerializable(typeof(CreateServiceAccountRequest))]
[JsonSerializable(typeof(CreateUserRequest))]
[JsonSerializable(typeof(CreateUserResponse))]
[JsonSerializable(typeof(DisableUserRequest))]
[JsonSerializable(typeof(GroupListResponse))]
[JsonSerializable(typeof(GroupMappingItem))]
[JsonSerializable(typeof(GroupMappingResponse))]
[JsonSerializable(typeof(GroupMappingsListResponse))]
[JsonSerializable(typeof(GroupResponse))]
[JsonSerializable(typeof(LinkUpstreamIdentityRequest))]
[JsonSerializable(typeof(LinkUpstreamIdentityResponse))]
[JsonSerializable(typeof(ListClientsResponse))]
[JsonSerializable(typeof(ListServiceAccountsResponse))]
[JsonSerializable(typeof(PasswordResetResponse))]
[JsonSerializable(typeof(ProviderResponse))]
[JsonSerializable(typeof(RegisteredServiceDto))]
[JsonSerializable(typeof(RegisteredServiceListResponse))]
[JsonSerializable(typeof(RegisteredServiceSummaryDto))]
[JsonSerializable(typeof(RegisterServicePermissionRequest))]
[JsonSerializable(typeof(RegisterServiceRequest))]
[JsonSerializable(typeof(RegisterServiceResponse))]
[JsonSerializable(typeof(ResetPasswordRequest))]
[JsonSerializable(typeof(RoleResponse))]
[JsonSerializable(typeof(RolesListResponse))]
[JsonSerializable(typeof(RotateSecretRequest))]
[JsonSerializable(typeof(RotateSecretResponse))]
[JsonSerializable(typeof(ServiceAccountCreatedResponse))]
[JsonSerializable(typeof(ServiceAccountResponse))]
[JsonSerializable(typeof(ServicePermissionDto))]
[JsonSerializable(typeof(SessionListResponse))]
[JsonSerializable(typeof(SessionResponse))]
[JsonSerializable(typeof(SetDefaultProviderRequest))]
[JsonSerializable(typeof(SetLocalFallbackRequest))]
[JsonSerializable(typeof(SetRolePermissionsRequest))]
[JsonSerializable(typeof(UpdateClientRequest))]
[JsonSerializable(typeof(UpdateGroupRequest))]
[JsonSerializable(typeof(UpdateProviderRequest))]
[JsonSerializable(typeof(UpdateRoleRequest))]
[JsonSerializable(typeof(UpdateServiceAccountRequest))]
[JsonSerializable(typeof(UpdateUserRequest))]
[JsonSerializable(typeof(UpstreamIdentitiesResponse))]
[JsonSerializable(typeof(UpstreamIdentityResponse))]
[JsonSerializable(typeof(UserListItemResponse))]
[JsonSerializable(typeof(UserListResponse))]
[JsonSerializable(typeof(UserProfileRequest))]
[JsonSerializable(typeof(UserProfileResponse))]
[JsonSerializable(typeof(UserResponse))]
[JsonSerializable(typeof(UserRolesResponse))]
[JsonSerializable(typeof(UserStatusChangeResponse))]
internal sealed partial class OpenIdentityStackApiJsonContext : JsonSerializerContext;
