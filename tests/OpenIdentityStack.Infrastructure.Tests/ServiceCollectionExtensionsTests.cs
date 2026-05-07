using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Common;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Persistence.Groups;
using OpenIdentityStack.Infrastructure.Persistence.Roles;
using OpenIdentityStack.Infrastructure.Persistence.ServiceAccounts;
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
    public void AddInfrastructure_RegistersServiceAccountRepository()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");
        ServiceProvider provider = services.BuildServiceProvider();

        IServiceAccountRepository repository = provider.GetRequiredService<IServiceAccountRepository>();
        repository.ShouldNotBeNull();
        repository.ShouldBeOfType<ServiceAccountRepository>();
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
    public void AddInfrastructure_RegistersClientApplicationRegistrar_AsScoped()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure("Host=localhost;Database=test;Username=test;Password=test", BuildTestConfiguration(), "Testing");

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IClientApplicationRegistrar));
        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(OpenIddictClientApplicationRegistrar));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
