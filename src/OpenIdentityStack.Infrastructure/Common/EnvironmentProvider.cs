using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Infrastructure.Common;

/// <summary>
/// Implementation of IEnvironmentProvider using ASP.NET Core IHostEnvironment.
/// </summary>
public sealed class EnvironmentProvider : IEnvironmentProvider
{
    private readonly IHostEnvironment hostEnvironment;

    public EnvironmentProvider(IHostEnvironment hostEnvironment)
    {
        this.hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    public bool IsDevelopment => this.hostEnvironment.IsDevelopment();
}
