using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class GetRoleQueryHandlerTests
{
    private readonly IRoleRepository roleRepository;
    private readonly GetRoleQueryHandler handler;

    public GetRoleQueryHandlerTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.handler = new GetRoleQueryHandler(this.roleRepository);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleNotFound_ReturnsNotFound()
    {
        var query = new GetRoleQuery(RoleId.Create());
        this.roleRepository.GetByIdAsync(query.RoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        Result<RoleDto> result = await this.handler.HandleAsync(query);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleExists_ReturnsMappedDto()
    {
        Role role = Role.Create("admin", "Admin", "Admin role").Value;
        role.SetPermissions(["app:resource:read"]);
        var query = new GetRoleQuery(role.Id);
        this.roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        Result<RoleDto> result = await this.handler.HandleAsync(query);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(role.Id.Value);
        result.Value.Name.ShouldBe("admin");
        result.Value.DisplayName.ShouldBe("Admin");
        result.Value.Permissions.ShouldBe(["app:resource:read"]);
    }
}
