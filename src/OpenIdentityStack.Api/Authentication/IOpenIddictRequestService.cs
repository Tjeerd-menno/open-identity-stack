using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace OpenIdentityStack.Api.Authentication;

public interface IOpenIddictRequestService
{
    OpenIddictRequest? GetRequest(HttpContext context);
}

public class OpenIddictRequestService : IOpenIddictRequestService
{
    public OpenIddictRequest? GetRequest(HttpContext context)
    {
        OpenIddictServerTransaction? transaction = context.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction;
        return transaction?.Request;
    }
}
