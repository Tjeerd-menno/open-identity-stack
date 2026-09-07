using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Audit;

public sealed class BoundedAuditActorTests(AdministrativeAuthorityTestFixture fixture) : IClassFixture<AdministrativeAuthorityTestFixture>
{
    [Theory]
    [InlineData(129)]
    [InlineData(255)]
    public async Task SupportedLongClientCanPersistAuditAcrossRelationalProviders(int length)
    {
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        IAuditLog audit = new AuditLogService(NullLogger<AuditLogService>.Instance, db, clock);
        string actor = "client:" + Guid.NewGuid().ToString("N").PadRight(length, 'x');
        string entityId = Guid.NewGuid().ToString("N");

        await audit.LogAsync(actor, "Authority.Withdrawn", "Role", entityId);
        await audit.LogChangeAsync(actor, "Authority.Withdrawn", "Role", entityId, null, null);

        await using OpenIdentityStackDbContext verify = fixture.CreateDbContext();
        string[] recorded = await verify.AuditLogEntries.Where(entry => entry.EntityId == entityId).Select(entry => entry.UserId).ToArrayAsync();
        recorded.Length.ShouldBe(2);
        recorded.Distinct().Count().ShouldBe(1);
        recorded[0].ShouldStartWith("sha256:");
        recorded[0].Length.ShouldBe(71);
        AuditLogEntry[] materialized = await verify.AuditLogEntries.AsNoTracking().Where(entry => entry.EntityId == entityId).ToArrayAsync();
        materialized.Select(entry => entry.UserId).ShouldBe(recorded);
        foreach (AuditLogEntry entry in materialized)
        {
            (await verify.AuditLogEntries.AsNoTracking().SingleAsync(value => value.Id == entry.Id)).UserId.ShouldBe(entry.UserId);
        }
    }
}
