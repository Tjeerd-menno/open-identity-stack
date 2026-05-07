using System.Globalization;
using System.Net;
using System.Net.Sockets;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SystemNetIPNetwork = System.Net.IPNetwork;

namespace OpenIdentityStack.Api.Configuration;

public sealed class ForwardedHeadersConfiguration
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }

    public string[] KnownProxies { get; set; } = [];

    public string[] KnownNetworks { get; set; } = [];
}

public sealed class ForwardedHeadersConfigurationValidator : IValidateOptions<ForwardedHeadersConfiguration>
{
    public ValidateOptionsResult Validate(string? name, ForwardedHeadersConfiguration options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ForwardedHeadersConfigurationParser.TryParse(options, out _, out string[] errors)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class ConfigureForwardedHeadersOptions(IOptions<ForwardedHeadersConfiguration> configuration)
    : IConfigureOptions<ForwardedHeadersOptions>
{
    public void Configure(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ParsedForwardedHeadersConfiguration parsedConfiguration = ForwardedHeadersConfigurationParser.ParseOrThrow(configuration.Value);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

        foreach (IPAddress knownProxy in parsedConfiguration.KnownProxies)
        {
            if (!options.KnownProxies.Any(existingProxy => existingProxy.Equals(knownProxy)))
            {
                options.KnownProxies.Add(knownProxy);
            }
        }

        foreach (SystemNetIPNetwork knownNetwork in parsedConfiguration.KnownNetworks)
        {
            if (!options.KnownIPNetworks.Any(existingNetwork =>
                    existingNetwork.BaseAddress.Equals(knownNetwork.BaseAddress)
                    && existingNetwork.PrefixLength == knownNetwork.PrefixLength))
            {
                options.KnownIPNetworks.Add(knownNetwork);
            }
        }
    }
}

public static class ForwardedHeadersServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ForwardedHeadersConfiguration>()
            .Bind(configuration.GetSection(ForwardedHeadersConfiguration.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ForwardedHeadersConfiguration>, ForwardedHeadersConfigurationValidator>();
        services.AddSingleton<IConfigureOptions<ForwardedHeadersOptions>, ConfigureForwardedHeadersOptions>();

        return services;
    }
}

file sealed class ParsedForwardedHeadersConfiguration(
    IReadOnlyList<IPAddress> knownProxies,
    IReadOnlyList<SystemNetIPNetwork> knownNetworks)
{
    public IReadOnlyList<IPAddress> KnownProxies { get; } = knownProxies;

    public IReadOnlyList<SystemNetIPNetwork> KnownNetworks { get; } = knownNetworks;
}

file static class ForwardedHeadersConfigurationParser
{
    public static bool TryParse(
        ForwardedHeadersConfiguration configuration,
        out ParsedForwardedHeadersConfiguration parsedConfiguration,
        out string[] errors)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<string> validationErrors = [];
        List<IPAddress> knownProxies = [];
        List<SystemNetIPNetwork> knownNetworks = [];

        ParseKnownProxies(configuration.KnownProxies, knownProxies, validationErrors);
        ParseKnownNetworks(configuration.KnownNetworks, knownNetworks, validationErrors);

        errors = validationErrors.ToArray();
        parsedConfiguration = new ParsedForwardedHeadersConfiguration(knownProxies, knownNetworks);

        return errors.Length == 0;
    }

    public static ParsedForwardedHeadersConfiguration ParseOrThrow(ForwardedHeadersConfiguration configuration)
    {
        if (TryParse(configuration, out ParsedForwardedHeadersConfiguration parsedConfiguration, out string[] errors))
        {
            return parsedConfiguration;
        }

        throw new OptionsValidationException(
            ForwardedHeadersConfiguration.SectionName,
            typeof(ForwardedHeadersConfiguration),
            errors);
    }

    private static void ParseKnownProxies(
        IEnumerable<string?> configuredProxies,
        List<IPAddress> parsedProxies,
        List<string> validationErrors)
    {
        int index = 0;
        foreach (string? configuredProxy in configuredProxies)
        {
            string candidate = configuredProxy?.Trim() ?? string.Empty;
            if (!IPAddress.TryParse(candidate, out IPAddress? knownProxy))
            {
                validationErrors.Add($"ForwardedHeaders:KnownProxies[{index}] value '{configuredProxy}' is not a valid IP address.");
                index++;
                continue;
            }

            parsedProxies.Add(knownProxy);
            index++;
        }
    }

    private static void ParseKnownNetworks(
        IEnumerable<string?> configuredNetworks,
        List<SystemNetIPNetwork> parsedNetworks,
        List<string> validationErrors)
    {
        int index = 0;
        foreach (string? configuredNetwork in configuredNetworks)
        {
            string candidate = configuredNetwork?.Trim() ?? string.Empty;
            if (!TryParseNetwork(candidate, out SystemNetIPNetwork knownNetwork))
            {
                validationErrors.Add($"ForwardedHeaders:KnownNetworks[{index}] value '{configuredNetwork}' is not a valid CIDR network.");
                index++;
                continue;
            }

            parsedNetworks.Add(knownNetwork);
            index++;
        }
    }

    private static bool TryParseNetwork(string value, out SystemNetIPNetwork network)
    {
        network = default;

        string[] parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out IPAddress? prefix)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prefixLength))
        {
            return false;
        }

        int maximumPrefixLength = prefix.AddressFamily switch
        {
            AddressFamily.InterNetwork => 32,
            AddressFamily.InterNetworkV6 => 128,
            _ => -1
        };

        if (prefixLength < 0 || prefixLength > maximumPrefixLength)
        {
            return false;
        }

        network = new SystemNetIPNetwork(prefix, prefixLength);
        return true;
    }
}
