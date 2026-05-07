using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Controller for handling external authentication callbacks.
/// </summary>
[ApiController]
[Route("signin")]
public sealed partial class ExternalCallbackController : ControllerBase
{
    private readonly IJitProvisionUserUseCase jitProvisionUseCase;
    private readonly ILogger<ExternalCallbackController> logger;

    public ExternalCallbackController(
        IJitProvisionUserUseCase jitProvisionUseCase,
        ILogger<ExternalCallbackController> logger)
    {
        this.jitProvisionUseCase = jitProvisionUseCase;
        this.logger = logger;
    }

    /// <summary>
    /// Handles the callback from an external authentication provider.
    /// </summary>
    [HttpGet("{provider}")]
    public async Task<IActionResult> ExternalCallback(
        string provider,
        [FromQuery] string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        // Authenticate using the external scheme
        AuthenticateResult authenticateResult = await this.HttpContext.AuthenticateAsync(provider);

        if (!authenticateResult.Succeeded)
        {
            this.LogExternalAuthFailed(provider);
            return this.Redirect($"/login?error=external_auth_failed&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        ClaimsPrincipal? externalUser = authenticateResult.Principal;
        if (externalUser is null)
        {
            this.LogNoPrincipalReturned(provider);
            return this.Redirect($"/login?error=no_principal&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        // Extract claims from the external identity
        string? subjectId = externalUser.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? externalUser.FindFirstValue("sub");
        string? email = externalUser.FindFirstValue(ClaimTypes.Email)
            ?? externalUser.FindFirstValue("email");
        string? name = externalUser.FindFirstValue(ClaimTypes.Name)
            ?? externalUser.FindFirstValue("name")
            ?? externalUser.FindFirstValue("preferred_username");

        if (string.IsNullOrEmpty(subjectId))
        {
            this.LogNoSubjectIdFound(provider);
            return this.Redirect($"/login?error=no_subject&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        // JIT provision or find the user
        // Note: In a real implementation, you would look up the UpstreamProviderId from the database
        // based on the provider scheme name. This is simplified for demonstration.
        var providerId = UpstreamProviderId.Create(); // This would come from the database

        var command = new JitProvisionUserCommand(
            providerId,
            subjectId,
            email,
            name);

        Result<JitProvisionUserResult> result = await this.jitProvisionUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            this.LogJitProvisioningFailed(provider, result.Error.Description);
            return this.Redirect($"/login?error=provisioning_failed&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        string userId = result.Value.UserId.ToString();
        this.LogUserAuthenticated(userId, provider, result.Value.IsNewUser);

        // Sign in the user locally
        // In a real implementation, you would:
        // 1. Create a local authentication cookie
        // 2. Or redirect back to the authorization flow with the local user identity

        // For now, redirect to the return URL
        return this.Redirect(returnUrl ?? "/");
    }

    /// <summary>
    /// Initiates external authentication.
    /// </summary>
    [HttpGet("{provider}/challenge")]
    public IActionResult Challenge(
        string provider,
        [FromQuery] string? returnUrl = null)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/signin/{provider}?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}",
            Items =
            {
                ["LoginProvider"] = provider,
            },
        };

        return this.Challenge(properties, provider);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "External authentication failed for provider {Provider}")]
    private partial void LogExternalAuthFailed(string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No principal returned from external authentication for provider {Provider}")]
    private partial void LogNoPrincipalReturned(string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No subject ID found in external claims for provider {Provider}")]
    private partial void LogNoSubjectIdFound(string provider);

    [LoggerMessage(Level = LogLevel.Warning, Message = "JIT provisioning failed for provider {Provider}: {Error}")]
    private partial void LogJitProvisioningFailed(string provider, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} authenticated via {Provider} (new user: {IsNew})")]
    private partial void LogUserAuthenticated(string userId, string provider, bool isNew);
}
