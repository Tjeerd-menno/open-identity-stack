using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

using OpenIdentityStack.Api.Configuration;

namespace OpenIdentityStack.Api.UnitTests.Configuration;

public sealed class ForwardedHeadersConfigurationTests
{
    [Fact]
    public void Configure_WithNoAllowlist_KeepsExistingTrustedDefaults()
    {
        // Arrange
        var options = new ForwardedHeadersOptions();
        IPAddress[] originalProxies = options.KnownProxies.ToArray();
        string[] originalNetworks = options.KnownIPNetworks
            .Select(static network => $"{network.BaseAddress}/{network.PrefixLength}")
            .ToArray();
        var sut = new ConfigureForwardedHeadersOptions(Options.Create(new ForwardedHeadersConfiguration()));

        // Act
        sut.Configure(options);

        // Assert
        options.ForwardedHeaders.ShouldBe(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost);
        options.KnownProxies.ToArray().ShouldBe(originalProxies);
        options.KnownIPNetworks
            .Select(static network => $"{network.BaseAddress}/{network.PrefixLength}")
            .ToArray()
            .ShouldBe(originalNetworks);
    }

    [Fact]
    public void Configure_WithConfiguredAllowlist_AddsConfiguredEntries()
    {
        // Arrange
        var options = new ForwardedHeadersOptions();
        var sut = new ConfigureForwardedHeadersOptions(Options.Create(new ForwardedHeadersConfiguration
        {
            KnownProxies = ["203.0.113.10"],
            KnownNetworks = ["10.20.0.0/16"]
        }));

        // Act
        sut.Configure(options);

        // Assert
        options.KnownProxies.ShouldContain(proxy => proxy.Equals(IPAddress.Parse("203.0.113.10")));
        options.KnownIPNetworks.ShouldContain(network =>
            network.BaseAddress.Equals(IPAddress.Parse("10.20.0.0")) && network.PrefixLength == 16);
    }

    [Fact]
    public void Validate_WithInvalidKnownProxy_Fails()
    {
        // Arrange
        var sut = new ForwardedHeadersConfigurationValidator();

        // Act
        ValidateOptionsResult result = sut.Validate(
            Options.DefaultName,
            new ForwardedHeadersConfiguration
            {
                KnownProxies = ["not-an-ip"]
            });

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain("ForwardedHeaders:KnownProxies[0] value 'not-an-ip' is not a valid IP address.");
    }

    [Fact]
    public void Validate_WithInvalidKnownNetwork_Fails()
    {
        // Arrange
        var sut = new ForwardedHeadersConfigurationValidator();

        // Act
        ValidateOptionsResult result = sut.Validate(
            Options.DefaultName,
            new ForwardedHeadersConfiguration
            {
                KnownNetworks = ["10.20.0.0/not-a-prefix"]
            });

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain("ForwardedHeaders:KnownNetworks[0] value '10.20.0.0/not-a-prefix' is not a valid CIDR network.");
    }
}
