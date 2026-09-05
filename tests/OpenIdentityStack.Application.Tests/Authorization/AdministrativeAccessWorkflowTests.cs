using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class AdministrativeAccessWorkflowTests
{
    private readonly IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
    private readonly IApplicationRepository applications = Substitute.For<IApplicationRepository>();
    private readonly IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
    private readonly DomainApplication client;
    private readonly AdministrativeAccessWorkflow workflow;

    public AdministrativeAccessWorkflowTests()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        this.client = DomainApplication.Create("admin-integration", "Admin integration", null, ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential, ["client_credentials"], ["ois.admin"], [], [], false, false, clock).Value;
        this.applications.GetByIdAsync(this.client.Id, Arg.Any<CancellationToken>()).Returns(this.client);
        this.approval.RequireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(DomainError.Forbidden("AdministrativeApproval.HumanRequired", "Human approval is required.")));
        this.workflow = new AdministrativeAccessWorkflow(this.resources, this.applications, this.approval);
    }

    [Fact]
    public async Task InitialApprovalFailsBeforeMutationWithoutHumanApproval()
    {
        Result<AdministrativeAccessDto> result = await this.workflow.SaveAsync(this.client.Id.Value, new([], ["users:read"], null), "machine");
        result.IsFailure.ShouldBeTrue();
        this.resources.DidNotReceive().AddGrant(Arg.Any<ClientResourceGrant>());
        await this.resources.DidNotReceiveWithAnyArgs().SaveChangesAsync(default!, default!, default!, default, default);
    }

    [Theory]
    [InlineData("users:read", "users:*", false)]
    [InlineData("users:*", "*", false)]
    [InlineData("*", "users:read", true)]
    [InlineData("users:*", "users:read", true)]
    [InlineData("users:read", "users:read", true)]
    public async Task OnlyCeilingExpansionNeedsFreshApproval(string previous, string proposed, bool allowed)
    {
        ClientResourceGrant grant = ClientResourceGrant.Create(this.client.Id, ProtectedResource.AdministrativeResourceId, [previous], []).Value;
        this.resources.GetGrantAsync(this.client.Id, ProtectedResource.AdministrativeResourceId, Arg.Any<CancellationToken>()).Returns(grant);
        Result<AdministrativeAccessDto> result = await this.workflow.SaveAsync(this.client.Id.Value, new([proposed], [], grant.Revision), "operator");
        result.IsSuccess.ShouldBe(allowed);
        if (!allowed) { grant.DelegatedPermissions.ShouldBe([previous]); }
    }

    [Fact]
    public async Task ApprovedInitialEntitlementPersistsAndRecordsCompletion()
    {
        this.approval.RequireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        Result<AdministrativeAccessDto> result = await this.workflow.SaveAsync(this.client.Id.Value, new(["users:read"], ["audit-logs:read"], null), "operator");
        result.IsSuccess.ShouldBeTrue();
        result.Value.Approved.ShouldBeTrue();
        this.resources.Received(1).AddGrant(Arg.Is<ClientResourceGrant>(grant => grant.ResourceId == ProtectedResource.AdministrativeResourceId));
        await this.approval.Received(1).RecordOutcomeAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevocationPreservesAnEmptyGrantAndRevision()
    {
        ClientResourceGrant grant = ClientResourceGrant.Create(this.client.Id, ProtectedResource.AdministrativeResourceId, ["*"], ["*"]).Value;
        this.resources.GetGrantAsync(this.client.Id, ProtectedResource.AdministrativeResourceId, Arg.Any<CancellationToken>()).Returns(grant);
        Result<AdministrativeAccessDto> result = await this.workflow.SaveAsync(this.client.Id.Value, new([], [], grant.Revision), "operator");
        result.IsSuccess.ShouldBeTrue();
        result.Value.Approved.ShouldBeFalse();
        result.Value.Revision.ShouldBe(2);
    }

    [Fact]
    public async Task ConcurrentPersistenceChangeReturnsConflictWithoutSuccessAudit()
    {
        this.approval.RequireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        this.resources.SaveChangesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ProtectedResource?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ResourceAccessConflictException(new InvalidOperationException("Concurrent write"))));
        Result<AdministrativeAccessDto> result = await this.workflow.SaveAsync(this.client.Id.Value, new(["users:read"], [], null), "operator");
        result.Error.Code.ShouldBe("Conflict.AdministrativeAccess.Conflict");
        await this.approval.DidNotReceive().RecordOutcomeAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StaleRevisionAndNonPlatformPermissionsCannotChangeEntitlement()
    {
        ClientResourceGrant grant = ClientResourceGrant.Create(this.client.Id, ProtectedResource.AdministrativeResourceId, ["users:read"], []).Value;
        this.resources.GetGrantAsync(this.client.Id, ProtectedResource.AdministrativeResourceId, Arg.Any<CancellationToken>()).Returns(grant);
        (await this.workflow.SaveAsync(this.client.Id.Value, new([], [], 999), "operator")).IsFailure.ShouldBeTrue();
        (await this.workflow.SaveAsync(this.client.Id.Value, new(["business:orders:read"], [], grant.Revision), "operator")).IsFailure.ShouldBeTrue();
        grant.Revision.ShouldBe(1);
    }
}
