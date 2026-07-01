using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Common;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Persistence.Groups;
using OpenIdentityStack.Infrastructure.Persistence.Roles;
using OpenIdentityStack.Infrastructure.Persistence.Sessions;
using OpenIdentityStack.Infrastructure.Persistence.Users;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private static IConfiguration BuildTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing"
            })
            .Build();

    private static IConfiguration BuildAspireConfiguration(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:openidentitystack"] = connectionString
            })
            .Build();

    private static IHostEnvironment BuildHostEnvironment(string environmentName)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    [Fact]
    public void AddInfrastructure_RegistersDateTimeProvider()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");
        ServiceProvider provider = services.BuildServiceProvider();

        IDateTimeProvider dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();
        dateTimeProvider.ShouldNotBeNull();
        dateTimeProvider.ShouldBeOfType<DateTimeProvider>();
    }

    [Fact]
    public void AddInfrastructure_RegistersPasswordHasher()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");
        ServiceProvider provider = services.BuildServiceProvider();

        IPasswordHasher hasher = provider.GetRequiredService<IPasswordHasher>();
        hasher.ShouldNotBeNull();
        hasher.ShouldBeOfType<PasswordHasher>();
    }

    [Fact]
    public void AddInfrastructure_RegistersUserRepository()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");
        ServiceProvider provider = services.BuildServiceProvider();

        IUserRepository repository = provider.GetRequiredService<IUserRepository>();
        repository.ShouldNotBeNull();
        repository.ShouldBeOfType<UserRepository>();
    }

    [Fact]
    public void AddInfrastructure_RegistersRoleRepository()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");
        ServiceProvider provider = services.BuildServiceProvider();

        IRoleRepository repository = provider.GetRequiredService<IRoleRepository>();
        repository.ShouldNotBeNull();
        repository.ShouldBeOfType<RoleRepository>();
    }

    [Fact]
    public void AddInfrastructure_RegistersGroupRepository()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");
        ServiceProvider provider = services.BuildServiceProvider();

        IGroupRepository repository = provider.GetRequiredService<IGroupRepository>();
        repository.ShouldNotBeNull();
        repository.ShouldBeOfType<GroupRepository>();
    }

    [Fact]
    public void AddInfrastructure_RegistersSessionRepository()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");
        ServiceProvider provider = services.BuildServiceProvider();

        ISessionRepository repository = provider.GetRequiredService<ISessionRepository>();
        repository.ShouldNotBeNull();
        repository.ShouldBeOfType<SessionRepository>();
    }

    [Fact]
    public void AddInfrastructure_RegistersUpstreamProviderRepository()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");
        ServiceProvider provider = services.BuildServiceProvider();

        IUpstreamProviderRepository repository = provider.GetRequiredService<IUpstreamProviderRepository>();
        repository.ShouldNotBeNull();
        repository.ShouldBeOfType<UpstreamProviderRepository>();
    }

    [Fact]
    public void AddInfrastructure_RegistersSecretProtector_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISecretProtector));
        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(AesSecretProtector));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddInfrastructure_DoesNotRegisterLegacyClientApplicationRegistrar()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IClientApplicationRegistrar));
        descriptor.ShouldBeNull();
    }

    [Fact]
    public void AddInfrastructureWithAspire_ThrowsWhenConnectionStringMissing()
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() =>
            services.AddInfrastructureWithAspire(
                BuildAspireConfiguration(connectionString: null),
                BuildHostEnvironment("Testing")));
    }

    [Fact]
    public void AddInfrastructureWithAspire_UsesSqliteInTesting_WhenSqliteConnectionStringIsConfigured()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = BuildAspireConfiguration("Data Source=:memory:");

        services.AddInfrastructureWithAspire(configuration, BuildHostEnvironment("Testing"));
        ServiceProvider provider = services.BuildServiceProvider();

        DbContextOptions<OpenIdentityStackDbContext> options = provider.GetRequiredService<DbContextOptions<OpenIdentityStackDbContext>>();
        options.Extensions.Any(e => string.Equals(e.GetType().Name, "SqliteOptionsExtension", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public void AddInfrastructureWithAspire_UsesNpgsqlOutsideTesting_WhenPostgresConnectionStringIsConfigured()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = BuildAspireConfiguration("Host=localhost;Database=test;Username=test;Password=test");

        services.AddInfrastructureWithAspire(configuration, BuildHostEnvironment("Development"));
        ServiceProvider provider = services.BuildServiceProvider();

        DbContextOptions<OpenIdentityStackDbContext> options = provider.GetRequiredService<DbContextOptions<OpenIdentityStackDbContext>>();
        options.Extensions.Any(e => string.Equals(e.GetType().Name, "NpgsqlOptionsExtension", StringComparison.Ordinal)).ShouldBeTrue();
    }
}
