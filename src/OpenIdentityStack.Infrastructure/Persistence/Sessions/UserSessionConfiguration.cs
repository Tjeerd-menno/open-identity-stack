using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Sessions;

/// <summary>
/// EF Core configuration for the UserSession aggregate.
/// </summary>
public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(
                id => id.Value,
                value => new SessionId(value))
            .IsRequired();

        builder.Property(s => s.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(s => s.IpAddress)
            .HasMaxLength(45) // IPv6 max length
            .IsRequired();

        builder.Property(s => s.UserAgent)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.LastActivityAt)
            .IsRequired();

        builder.Property(s => s.ExpiresAt)
            .IsRequired();

        builder.Property(s => s.RevokedAt);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // Configure the owned collection of ClientSessions
        builder.OwnsMany(s => s.ClientSessions, clientBuilder =>
        {
            clientBuilder.ToTable("ClientSessions");

            clientBuilder.WithOwner()
                .HasForeignKey("UserSessionId");

            clientBuilder.Property<int>("Id")
                .ValueGeneratedOnAdd();

            clientBuilder.HasKey("Id");

            clientBuilder.Property(c => c.ClientId)
                .HasMaxLength(256)
                .IsRequired();

            clientBuilder.Property(c => c.AuthenticatedAt)
                .IsRequired();

            clientBuilder.Property(c => c.LastAccessAt)
                .IsRequired();

            clientBuilder.Property(c => c.FrontChannelLogoutUri)
                .HasMaxLength(2048);

            clientBuilder.Property(c => c.BackChannelLogoutUri)
                .HasMaxLength(2048);

            clientBuilder.HasIndex("UserSessionId", "ClientId")
                .IsUnique();
        });

        // Indexes for common query patterns
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_UserSessions_UserId");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_UserSessions_Status");

        builder.HasIndex(s => new { s.UserId, s.Status })
            .HasDatabaseName("IX_UserSessions_UserId_Status");

        builder.HasIndex(s => s.ExpiresAt)
            .HasDatabaseName("IX_UserSessions_ExpiresAt");

        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("IX_UserSessions_CreatedAt");

        builder.HasIndex(s => s.LastActivityAt)
            .HasDatabaseName("IX_UserSessions_LastActivityAt");

        // Ignore domain events
        builder.Ignore(s => s.DomainEvents);
    }
}
