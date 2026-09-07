using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OpenIdentityStack.Api.Authorization;

namespace OpenIdentityStack.Api.Configuration;

public sealed class AdministrativeApprovalOpenApiTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        foreach (IOpenApiParameter parameter in operation.Parameters.Where(static parameter =>
                     parameter.In == ParameterLocation.Header &&
                     string.Equals(parameter.Name, AdministrativeActorContext.ApprovalHeader, StringComparison.Ordinal)))
        {
            if (parameter is OpenApiParameter mutableParameter)
            {
                mutableParameter.Schema = CreateAcknowledgementSchema();
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema CreateAcknowledgementSchema() =>
        new()
        {
            Type = JsonSchemaType.String,
            Enum = [JsonValue.Create(AdministrativeActorContext.ApprovalAcknowledgement)]
        };
}
