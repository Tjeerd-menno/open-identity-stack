using System.Reflection;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Api.Settings;
using OpenIdentityStack.Application.Federation.Queries;
using OpenIdentityStack.Application.Settings.Commands;
using OpenIdentityStack.Application.Settings.Queries;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;

namespace OpenIdentityStack.Api.UnitTests.Settings;

public sealed class AuthenticationSettingsApiTests
{
    private static readonly Assembly ApiAssembly = typeof(PermissionRequirement).Assembly;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetSettings_WithSuccessfulQuery_ReturnsMappedResponse()
    {
        DateTimeOffset updatedAt = DateTimeOffset.UtcNow;
        StubGetAuthenticationSettingsQueryHandler queryHandler = new((_, _) =>
            Task.FromResult<Result<AuthenticationSettingsDto>>(new AuthenticationSettingsDto("provider-1", false, true, updatedAt)));

        IResult result = await InvokeHandlerAsync("GetSettings", queryHandler, CancellationToken.None);
        (int statusCode, AuthenticationSettingsResponse? response, _) = await ExecuteAsync<AuthenticationSettingsResponse>(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        response.ShouldNotBeNull();
        response.DefaultProviderId.ShouldBe("provider-1");
        response.IsLocalDefault.ShouldBeFalse();
        response.LocalFallbackEnabled.ShouldBeTrue();
        response.UpdatedAt.ShouldBe(updatedAt);
        queryHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListProviders_WhenProviderQuerySucceeds_IncludesLocalAndExternalProviders()
    {
        var providerId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        StubListProvidersQueryHandler queryHandler = new((_, _) =>
        {
            IReadOnlyList<ProviderDto> providers =
            [
                new(providerId, "entra", "Microsoft Entra ID", "https://issuer", "client-id", ["openid"], true, "Active", createdAt),
                new(Guid.NewGuid(), "legacy", "Legacy", "https://legacy", "legacy-client", ["openid"], false, "Disabled", createdAt)
            ];
            Result<IReadOnlyList<ProviderDto>> result = AsResult(providers);
            return Task.FromResult(result);
        });

        IResult result = await InvokeHandlerAsync("ListProviders", queryHandler, CancellationToken.None);
        (int statusCode, List<AuthenticationProviderResponse>? response, _) = await ExecuteAsync<List<AuthenticationProviderResponse>>(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        response.ShouldNotBeNull();
        response.Count.ShouldBe(3);
        response[0].Id.ShouldBe(AuthenticationSettings.LocalProviderId);
        response[0].Type.ShouldBe("local");
        response[0].IsActive.ShouldBeTrue();
        response[1].Id.ShouldBe(providerId.ToString());
        response[1].Type.ShouldBe("external");
        response[1].IsActive.ShouldBeTrue();
        response[2].IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task ListProviders_WhenProviderQueryFails_ReturnsLocalProviderOnly()
    {
        StubListProvidersQueryHandler queryHandler = new((_, _) =>
            Task.FromResult((Result<IReadOnlyList<ProviderDto>>)DomainError.Failure("Providers.Unavailable", "Provider lookup failed.")));

        IResult result = await InvokeHandlerAsync("ListProviders", queryHandler, CancellationToken.None);
        (int statusCode, List<AuthenticationProviderResponse>? response, _) = await ExecuteAsync<List<AuthenticationProviderResponse>>(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response[0].Id.ShouldBe(AuthenticationSettings.LocalProviderId);
        response[0].Name.ShouldBe("local");
    }

    [Fact]
    public async Task SetDefaultProvider_WhenProviderDoesNotExist_ReturnsNotFound()
    {
        StubSetDefaultProviderUseCase useCase = new((command, _) =>
            Task.FromResult((Result<SetDefaultProviderResult>)DomainError.NotFound("Provider.Missing", "Provider not found.")));
        StubGetAuthenticationSettingsQueryHandler queryHandler = new((_, _) =>
            Task.FromResult((Result<AuthenticationSettingsDto>)new AuthenticationSettingsDto("local", true, true, DateTimeOffset.UtcNow)));

        IResult result = await InvokeHandlerAsync(
            "SetDefaultProvider",
            useCase,
            queryHandler,
            new SetDefaultProviderRequest { ProviderId = "missing" },
            CancellationToken.None);

        (int statusCode, JsonDocument? _, string body) = await ExecuteAsync<JsonDocument>(result);

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
        body.ShouldContain("Provider not found.");
        useCase.LastCommand?.ProviderId.ShouldBe("missing");
        queryHandler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SetDefaultProvider_WithSuccessfulUpdate_ReturnsLatestSettings()
    {
        DateTimeOffset updatedAt = DateTimeOffset.UtcNow;
        StubSetDefaultProviderUseCase useCase = new((command, _) =>
            Task.FromResult<Result<SetDefaultProviderResult>>(new SetDefaultProviderResult(command.ProviderId, false, updatedAt)));
        StubGetAuthenticationSettingsQueryHandler queryHandler = new((_, _) =>
            Task.FromResult<Result<AuthenticationSettingsDto>>(new AuthenticationSettingsDto("provider-2", false, true, updatedAt)));

        IResult result = await InvokeHandlerAsync(
            "SetDefaultProvider",
            useCase,
            queryHandler,
            new SetDefaultProviderRequest { ProviderId = "provider-2" },
            CancellationToken.None);

        (int statusCode, AuthenticationSettingsResponse? response, _) = await ExecuteAsync<AuthenticationSettingsResponse>(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        response.ShouldNotBeNull();
        response.DefaultProviderId.ShouldBe("provider-2");
        response.LocalFallbackEnabled.ShouldBeTrue();
        useCase.LastCommand?.ProviderId.ShouldBe("provider-2");
        queryHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SetLocalFallback_WhenSettingsReloadFails_ReturnsBadRequest()
    {
        StubSetLocalFallbackUseCase useCase = new((command, _) =>
            Task.FromResult<Result<SetLocalFallbackResult>>(new SetLocalFallbackResult(command.Enabled, DateTimeOffset.UtcNow)));
        StubGetAuthenticationSettingsQueryHandler queryHandler = new((_, _) =>
            Task.FromResult((Result<AuthenticationSettingsDto>)DomainError.Failure("Settings.ReloadFailed", "Could not reload settings.")));

        IResult result = await InvokeHandlerAsync(
            "SetLocalFallback",
            useCase,
            queryHandler,
            new SetLocalFallbackRequest { Enabled = true },
            CancellationToken.None);

        (int statusCode, JsonDocument? _, string body) = await ExecuteAsync<JsonDocument>(result);

        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
        body.ShouldContain("Could not reload settings.");
        useCase.LastCommand?.Enabled.ShouldBeTrue();
        queryHandler.CallCount.ShouldBe(1);
    }


    private static Result<T> AsResult<T>(T value) => value;

    private static async Task<IResult> InvokeHandlerAsync(string methodName, params object?[] args)
    {
        Type type = ApiAssembly.GetType("OpenIdentityStack.Api.Settings.AuthenticationSettingsApi")
            ?? throw new InvalidOperationException("Could not find AuthenticationSettingsApi type.");
        MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Could not find handler '{methodName}'.");

        return await (Task<IResult>)method.Invoke(null, args)!;
    }

    private static async Task<(int StatusCode, T? Value, string Body)> ExecuteAsync<T>(IResult result)
    {
        DefaultHttpContext context = new()
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .AddOptions()
                .BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body);
        string body = await reader.ReadToEndAsync();
        T? value = string.IsNullOrWhiteSpace(body)
            ? default
            : JsonSerializer.Deserialize<T>(body, JsonOptions);

        return (context.Response.StatusCode, value, body);
    }

    private sealed class StubGetAuthenticationSettingsQueryHandler(
        Func<GetAuthenticationSettingsQuery, CancellationToken, Task<Result<AuthenticationSettingsDto>>> handler)
        : IGetAuthenticationSettingsQueryHandler
    {
        public int CallCount { get; private set; }

        public Task<Result<AuthenticationSettingsDto>> HandleAsync(GetAuthenticationSettingsQuery query, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return handler(query, cancellationToken);
        }
    }

    private sealed class StubListProvidersQueryHandler(
        Func<ListProvidersQuery, CancellationToken, Task<Result<IReadOnlyList<ProviderDto>>>> handler)
        : IListProvidersQueryHandler
    {
        public Task<Result<IReadOnlyList<ProviderDto>>> HandleAsync(ListProvidersQuery query, CancellationToken cancellationToken = default)
        {
            return handler(query, cancellationToken);
        }
    }

    private sealed class StubSetDefaultProviderUseCase(
        Func<SetDefaultProviderCommand, CancellationToken, Task<Result<SetDefaultProviderResult>>> handler)
        : ISetDefaultProviderUseCase
    {
        public SetDefaultProviderCommand? LastCommand { get; private set; }

        public Task<Result<SetDefaultProviderResult>> ExecuteAsync(SetDefaultProviderCommand command, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return handler(command, cancellationToken);
        }
    }

    private sealed class StubSetLocalFallbackUseCase(
        Func<SetLocalFallbackCommand, CancellationToken, Task<Result<SetLocalFallbackResult>>> handler)
        : ISetLocalFallbackUseCase
    {
        public SetLocalFallbackCommand? LastCommand { get; private set; }

        public Task<Result<SetLocalFallbackResult>> ExecuteAsync(SetLocalFallbackCommand command, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return handler(command, cancellationToken);
        }
    }
}
