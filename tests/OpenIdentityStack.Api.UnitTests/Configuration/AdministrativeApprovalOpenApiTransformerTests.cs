using Microsoft.OpenApi;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Api.Configuration;

namespace OpenIdentityStack.Api.UnitTests.Configuration;

public sealed class AdministrativeApprovalOpenApiTransformerTests
{
    [Fact]
    public async Task TransformAsync_ConstrainsApprovalHeaderToAcceptedLiteral()
    {
        var approvalParameter = new OpenApiParameter
        {
            Name = AdministrativeActorContext.ApprovalHeader,
            In = ParameterLocation.Header,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var operation = new OpenApiOperation { Parameters = [approvalParameter] };
        var transformer = new AdministrativeApprovalOpenApiTransformer();

        await transformer.TransformAsync(operation, context: null!, CancellationToken.None);

        OpenApiSchema schema = approvalParameter.Schema.ShouldBeOfType<OpenApiSchema>();
        schema.Enum.ShouldNotBeNull();
        schema.Enum.Count.ShouldBe(1);
        schema.Enum[0]!.GetValue<string>().ShouldBe(AdministrativeActorContext.ApprovalAcknowledgement);
    }
}
