using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Queries;
/// <summary>
/// Handler for getting user roles.
/// </summary>
public sealed class GetUserRolesQueryHandler : IGetUserRolesQueryHandler
{
    private readonly IUserRepository userRepository;
    private readonly IRoleRepository roleRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserRolesQueryHandler"/> class.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="roleRepository">The role repository.</param>
    public GetUserRolesQueryHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository)
    {
        this.userRepository = userRepository;
        this.roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<Result<GetUserRolesResponse>> HandleAsync(
        UserId userId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        // Validate user exists
        User? user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return DomainError.NotFound("UserNotFound", "User not found.");
        }

        // Get the user's roles
        IReadOnlyList<Role> roles = await this.roleRepository.GetUserRolesAsync(
            userId,
            activeOnly,
            cancellationToken);

        // Map to role DTOs
        var roleDtos = roles.Select(r => new RoleDto(
            r.Id.Value,
            r.Name,
            r.DisplayName,
            r.Description,
            r.IsSystemRole,
            r.IsActive,
            r.Permissions)).ToList();

        return new GetUserRolesResponse(userId.Value, roleDtos);
    }
}
