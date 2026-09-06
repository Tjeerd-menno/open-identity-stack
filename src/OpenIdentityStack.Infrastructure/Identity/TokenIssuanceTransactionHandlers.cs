using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenIdentityStack.Infrastructure.Identity;

public sealed class BeginTokenIssuanceTransaction(TokenIssuanceTransaction transaction) :
    IOpenIddictServerHandler<ProcessSignInContext>
{
    public const int HandlerOrder = int.MinValue + 50_000;

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<BeginTokenIssuanceTransaction>()
            .SetOrder(HandlerOrder)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessSignInContext context) =>
        await transaction.BeginAsync(context.CancellationToken);
}

public sealed class CommitTokenIssuanceTransaction(TokenIssuanceTransaction transaction) :
    IOpenIddictServerHandler<ProcessSignInContext>
{
    // Apply*Response handlers at 500_000 dispatch and handle the response, which stops
    // later ProcessSignIn handlers. Commit after every Generate* handler (<= 112_000)
    // but before response dispatch so the client never receives uncommitted credentials.
    public const int HandlerOrder = 200_000;

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<CommitTokenIssuanceTransaction>()
            .SetOrder(HandlerOrder)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessSignInContext context) =>
        await transaction.CommitAsync(context.CancellationToken);
}

public static class TokenIssuanceTransactionExtensions
{
    public static OpenIddictServerBuilder AddTokenIssuanceTransaction(this OpenIddictServerBuilder builder)
    {
        builder.Services.AddScoped<TokenIssuanceTransaction>();
        builder.Services.AddScoped<BeginTokenIssuanceTransaction>();
        builder.Services.AddScoped<CommitTokenIssuanceTransaction>();
        builder.AddEventHandler(BeginTokenIssuanceTransaction.Descriptor);
        builder.AddEventHandler(CommitTokenIssuanceTransaction.Descriptor);
        return builder;
    }
}
