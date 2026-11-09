extern alias AppHostProject;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

public sealed class AppHostConfigurationTests
{
    [Theory]
    [InlineData("Development", false, "true")]
    [InlineData("Development", true, null)]
    [InlineData("Production", false, null)]
    [InlineData("Production", true, null)]
    [InlineData("Staging", false, null)]
    [InlineData("Testing", false, null)]
    public async Task ManagementWebBootstrap_IsEnabledOnlyForLocalDevelopment(
        string environment, bool publish, string? expected)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        string[] args = publish ? ["--publisher=manifest"] : [];
        await using IDistributedApplicationTestingBuilder builder =
            await DistributedApplicationTestingBuilder.CreateAsync<AppHostProject::Projects.OpenIdentityStack_AppHost>(
                args, (_, settings) => settings.EnvironmentName = environment, cancellation.Token);
        builder.ExecutionContext.IsRunMode.ShouldBe(!publish);

        ProjectResource migrator = builder.Resources.OfType<ProjectResource>()
            .Single(resource => resource.Name == "openidentitystack-db-migrator");
        var variables = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, migrator, variables, cancellation.Token);
        foreach (EnvironmentCallbackAnnotation annotation in migrator.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        variables.GetValueOrDefault("Seed__AdministrativeAccess__BootstrapManagementWeb").ShouldBe(expected);
    }
}
