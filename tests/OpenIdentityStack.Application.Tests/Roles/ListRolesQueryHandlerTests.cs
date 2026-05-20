using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class ListRolesQueryHandlerTests
{
    private readonly IRoleRepository roleRepository;
    private readonly ListRolesQueryHandler handler;

    public ListRolesQueryHandlerTests()
    {
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.handler = new ListRolesQueryHandler(this.roleRepository);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMappedRolesAndPaginationMetadata()
    {
        Role admin = Role.CreateSystemRole("admin", "Admin", "System admin").Value;
        Role support = Role.Create("support", "Support", "Support role").Value;
        support.SetPermissions(["tickets:read"]);
        var query = new ListRolesQuery(
            Page: 2,
            PageSize: 5,
            IncludeInactive: true,
            Search: "adm");
        this.roleRepository.GetAllAsync(true, 5, 5, "adm", Arg.Any<CancellationToken>())
            .Returns([admin, support]);
        this.roleRepository.GetCountAsync(true, "adm", Arg.Any<CancellationToken>())
            .Returns(12);

        Result<ListRolesResponse> result = await this.handler.HandleAsync(query);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(12);
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(5);
        result.Value.TotalPages.ShouldBe(3);
        result.Value.HasNextPage.ShouldBeTrue();
        result.Value.HasPreviousPage.ShouldBeTrue();
        result.Value.Items[0].Name.ShouldBe(admin.Name);
        result.Value.Items[0].DisplayName.ShouldBe(admin.DisplayName);
        result.Value.Items[0].Description.ShouldBe(admin.Description);
        result.Value.Items[0].IsSystemRole.ShouldBeTrue();
        result.Value.Items[0].IsActive.ShouldBeTrue();
        result.Value.Items[1].Permissions.ShouldBe(["tickets:read"]);
    }

    [Fact]
    public async Task HandleAsync_ClampsInvalidPagingArguments()
    {
        var query = new ListRolesQuery(Page: -5, PageSize: 500);
        using var cts = new CancellationTokenSource();
        this.roleRepository.GetAllAsync(false, 0, 100, null, cts.Token).Returns([]);
        this.roleRepository.GetCountAsync(false, null, cts.Token).Returns(0);

        Result<ListRolesResponse> result = await this.handler.HandleAsync(query, cts.Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(1);
        result.Value.PageSize.ShouldBe(100);
        result.Value.Items.ShouldBeEmpty();
        await this.roleRepository.Received(1).GetAllAsync(false, 0, 100, null, cts.Token);
        await this.roleRepository.Received(1).GetCountAsync(false, null, cts.Token);
    }
}
