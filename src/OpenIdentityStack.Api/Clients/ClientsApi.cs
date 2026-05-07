using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Application.Clients.Queries;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Api.Clients;

/// <summary>
/// Minimal API endpoints for managing OAuth2/OIDC clients via Admin API.
/// </summary>
internal static class ClientsApi
{
    public static IEndpointRouteBuilder MapClientsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/clients")
            .WithTags(nameof(ClientsApi));

        group.MapPost(string.Empty, CreateClient)
            .RequireAuthorization(Permissions.Clients.Write)
            .Produces<ClientCreatedResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateClient")
            .WithSummary("Creates a new OAuth2/OIDC client");

        group.MapGet("{id:guid}", GetClient)
            .RequireAuthorization(Permissions.Clients.Read)
            .Produces<ClientResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetClient")
            .WithSummary("Gets a client by ID");

        group.MapGet(string.Empty, ListClients)
            .RequireAuthorization(Permissions.Clients.Read)
            .Produces<ListClientsResponse>(StatusCodes.Status200OK)
            .WithName("ListClients")
            .WithSummary("Lists all clients with pagination");

        group.MapPatch("{id:guid}", UpdateClient)
            .RequireAuthorization(Permissions.Clients.Write)
            .Produces<ClientResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateClient")
            .WithSummary("Updates a client's basic information");

        group.MapDelete("{id:guid}", DeleteClient)
            .RequireAuthorization(Permissions.Clients.Delete)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteClient")
            .WithSummary("Deletes a client");

        return app;
    }

    private static async Task<IResult> CreateClient(
        [FromServices] ICreateClientUseCase createClientUseCase,
        [FromBody] CreateClientRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateClientCommand(
            request.ClientId,
            request.DisplayName,
            request.ClientType,
            request.Description,
            request.RedirectUris,
            request.PostLogoutRedirectUris,
            request.AllowedScopes,
            request.AllowedGrantTypes,
            request.RequirePkce,
            request.RequireConsent);

        Result<CreateClientResult> result = await createClientUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Client.ClientIdExists")
            {
                return TypedResults.Conflict(new { error = result.Error.Code, message = result.Error.Description });
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        var response = new ClientCreatedResponse(
            result.Value.ClientId.Value,
            result.Value.ClientIdValue,
            result.Value.DisplayName,
            result.Value.ClientSecret,
            DateTimeOffset.UtcNow);

        return TypedResults.Created($"/api/admin/clients/{result.Value.ClientId.Value}", response);
    }

    private static async Task<IResult> GetClient(
        [FromServices] IGetClientQueryHandler getClientQueryHandler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result<ClientDetails> result = await getClientQueryHandler.HandleAsync(new ClientId(id), cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        ClientDetails detail = result.Value;
        var response = new ClientResponse(
            detail.Id.Value,
            detail.ClientIdValue,
            detail.DisplayName,
            detail.ClientType,
            detail.Description,
            detail.RedirectUris,
            detail.PostLogoutRedirectUris,
            detail.AllowedScopes,
            detail.AllowedGrantTypes,
            detail.RequirePkce,
            detail.RequireConsent,
            detail.CreatedAt,
            detail.ModifiedAt);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ListClients(
        [FromServices] IListClientsQueryHandler listClientsQueryHandler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var query = new ListClientsQuery(page, pageSize);
        Result<ListClientsResult> result = await listClientsQueryHandler.HandleAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        var items = result.Value.Items.Select(i => new ClientListItemResponse(
            i.Id.Value,
            i.ClientIdValue,
            i.DisplayName,
            i.ClientType,
            i.AllowedGrantTypes,
            i.CreatedAt)).ToList();

        var response = new ListClientsResponse(
            items,
            result.Value.TotalCount,
            result.Value.Page,
            result.Value.PageSize);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> UpdateClient(
        [FromServices] IUpdateClientUseCase updateClientUseCase,
        [FromServices] IGetClientQueryHandler getClientQueryHandler,
        Guid id,
        [FromBody] UpdateClientRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateClientCommand(
            new ClientId(id),
            request.DisplayName,
            request.Description);

        Result<UpdateClientResult> result = await updateClientUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Client.NotFound")
            {
                return TypedResults.NotFound();
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        // Fetch the full client details to return
        Result<ClientDetails> detailsResult = await getClientQueryHandler.HandleAsync(new ClientId(id), cancellationToken);
        if (detailsResult.IsSuccess)
        {
            ClientDetails detail = detailsResult.Value;
            var response = new ClientResponse(
                detail.Id.Value,
                detail.ClientIdValue,
                detail.DisplayName,
                detail.ClientType,
                detail.Description,
                detail.RedirectUris,
                detail.PostLogoutRedirectUris,
                detail.AllowedScopes,
                detail.AllowedGrantTypes,
                detail.RequirePkce,
                detail.RequireConsent,
                detail.CreatedAt,
                detail.ModifiedAt);

            return TypedResults.Ok(response);
        }

        return TypedResults.NotFound();
    }

    private static async Task<IResult> DeleteClient(
        [FromServices] IDeleteClientUseCase deleteClientUseCase,
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteClientCommand(new ClientId(id));
        Result result = await deleteClientUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Client.NotFound")
            {
                return TypedResults.NotFound();
            }
            return TypedResults.BadRequest(new { error = result.Error.Code, message = result.Error.Description });
        }

        return TypedResults.NoContent();
    }
}
