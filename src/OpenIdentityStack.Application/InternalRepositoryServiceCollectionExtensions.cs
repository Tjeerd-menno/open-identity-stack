using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace OpenIdentityStack.Application;

/// <summary>
/// Registers services whose constructors are internal because a parameter is an internal repository port.
/// A public constructor cannot name that parameter (CS0051), and the container only selects public constructors.
/// </summary>
internal static class InternalRepositoryServiceCollectionExtensions
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ConstructorInfo> constructors = new();

    public static IServiceCollection AddScopedInternal<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddScoped<TService>(static serviceProvider => Create<TImplementation>(serviceProvider));
        return services;
    }

    public static IServiceCollection AddScopedInternal<TImplementation>(this IServiceCollection services)
        where TImplementation : class
    {
        services.AddScoped<TImplementation>(static serviceProvider => Create<TImplementation>(serviceProvider));
        return services;
    }

    private static TImplementation Create<TImplementation>(IServiceProvider serviceProvider)
        where TImplementation : class
    {
        ConstructorInfo constructor = constructors.GetOrAdd(typeof(TImplementation), static type => SelectConstructor(type));
        ParameterInfo[] parameters = constructor.GetParameters();
        object?[] arguments = new object?[parameters.Length];
        for (int index = 0; index < parameters.Length; index++)
        {
            arguments[index] = Resolve(serviceProvider, parameters[index]);
        }

        return (TImplementation)constructor.Invoke(arguments);
    }

    private static ConstructorInfo SelectConstructor(Type type)
    {
        ConstructorInfo[] candidates = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(candidate => !candidate.IsPrivate)
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new InvalidOperationException($"No accessible constructor for {type.FullName}.");
        }

        int longest = candidates.Max(candidate => candidate.GetParameters().Length);
        ConstructorInfo[] greediest = candidates.Where(candidate => candidate.GetParameters().Length == longest).ToArray();
        if (greediest.Length != 1)
        {
            throw new InvalidOperationException($"Multiple constructors of length {longest} for {type.FullName}.");
        }

        return greediest[0];
    }

    private static object? Resolve(IServiceProvider serviceProvider, ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue)
        {
            object? service = serviceProvider.GetService(parameter.ParameterType);
            if (service is not null)
            {
                return service;
            }

            return parameter.DefaultValue is DBNull ? null : parameter.DefaultValue;
        }

        return serviceProvider.GetRequiredService(parameter.ParameterType);
    }
}
