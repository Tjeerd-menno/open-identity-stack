using Microsoft.Extensions.DependencyInjection;

namespace OpenIdentityStack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application layer services are registered in Infrastructure.ServiceCollectionExtensions.AddCommonServices()
        // This method is kept for future application-layer-only registrations if needed
        return services;
    }
}
