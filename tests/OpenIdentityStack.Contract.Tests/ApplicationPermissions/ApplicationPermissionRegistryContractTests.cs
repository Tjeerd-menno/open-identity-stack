namespace OpenIdentityStack.Contract.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionRegistryContractTests : ApplicationPermissionRegistryContractTestBase
{
    [Fact]
    public void ContractFilePath_IsDefined()
    {
        OpenApiContractPath.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Contract_DefinesSliceOneApplicationAndManifestRoutes()
    {
        string contract = ReadContract();

        contract.ShouldContain("/api/admin/application-permissions/applications:");
        contract.ShouldContain("/api/admin/application-permissions/applications/{applicationId}:");
        contract.ShouldContain("/api/admin/application-permissions/applications/{applicationId}/manifest/preview:");
        contract.ShouldContain("/api/admin/application-permissions/applications/{applicationId}/manifest:");
        contract.ShouldContain("operationId: createApplicationPermissionApplication");
        contract.ShouldContain("operationId: previewInlineApplicationPermissionManifest");
        contract.ShouldContain("operationId: applyInlineApplicationPermissionManifest");
    }

    [Fact]
    public void Contract_DefinesCompleteManifestRequestShape()
    {
        string contract = ReadContract();

        contract.ShouldContain("CreateRegisteredApplicationRequest:");
        contract.ShouldContain("PermissionManifest:");
        contract.ShouldContain("schemaVersion:");
        contract.ShouldContain("const: 1.0.0");
        contract.ShouldContain("displayName:");
        contract.ShouldContain("key: { type: string, pattern: '^[a-z][a-z0-9-]{1,62}:[a-z][a-z0-9-]{1,62}$' }");
        contract.ShouldNotContain("required: [id, name]");
        contract.ShouldNotContain("name: { type: string, pattern:");
    }

    [Fact]
    public void Contract_DefinesApplicationMetadataAndStateWithoutRetiredLifecycle()
    {
        string contract = ReadContract();

        contract.ShouldContain("ApplicationPermissionApplicationStatus:");
        contract.ShouldContain("enum: [active, disabled]");
        contract.ShouldContain("manifestVersion:");
        contract.ShouldContain("manifestBaseUrl:");
        contract.ShouldNotContain("retired");
        contract.ShouldNotContain("/api/admin/application-permissions/applications/{applicationId}/lifecycle:");
    }

    [Fact]
    public void Contract_DefinesSliceTwoRoleAcknowledgementFields()
    {
        string contract = ReadContract();

        contract.ShouldContain("CreateRoleRequest:");
        contract.ShouldContain("SetRolePermissionsRequest:");
        contract.ShouldContain("AddPermissionRequest:");
        contract.ShouldContain("acknowledgeWildcardGrant: { type: boolean, default: false }");
        contract.ShouldContain("RolePermissions.BroadGrantAcknowledgementRequired");
    }

    [Fact]
    public void Contract_DefinesSliceTwoCatalogs()
    {
        string contract = ReadContract();

        contract.ShouldContain("/api/admin/application-permissions/catalog:");
        contract.ShouldContain("ApplicationPermissionWildcardCatalogItem:");
        contract.ShouldContain("kind: { const: wildcard }");
        contract.ShouldContain("/api/admin/permissions/platform:");
        contract.ShouldContain("PlatformPermissionCatalogItem:");
    }

    [Fact]
    public void Contract_DefinesSliceThreeDestructiveRoutes()
    {
        string contract = ReadContract();

        contract.ShouldContain("/api/admin/application-permissions/applications/{applicationId}/deletion-impact:");
        contract.ShouldContain("/api/admin/application-permissions/permissions/{permissionId}:");
        contract.ShouldContain("/api/admin/application-permissions/permissions/{permissionId}/deletion-impact:");
        contract.ShouldContain("operationId: getApplicationPermissionApplicationDeletionImpact");
        contract.ShouldContain("operationId: deleteApplicationPermissionApplication");
        contract.ShouldContain("operationId: getApplicationPermissionDeletionImpact");
        contract.ShouldContain("operationId: deleteApplicationPermission");
    }

    [Fact]
    public void Contract_DefinesSliceThreeDestructivePreviewAndResultDtos()
    {
        string contract = ReadContract();

        contract.ShouldContain("ManifestPreviewResponse:");
        contract.ShouldContain("ManifestApplyResponse:");
        contract.ShouldContain("DeletionImpactResponse:");
        contract.ShouldContain("DestructiveOperationResult:");
        contract.ShouldContain("PermissionAssignmentImpact:");
        contract.ShouldContain("removedPermissions:");
        contract.ShouldContain("exactAssignmentsRemoved:");
        contract.ShouldContain("wildcardAssignmentsRemoved:");
        contract.ShouldContain("wildcardAssignmentsImpacted:");
        contract.ShouldContain("assignmentStore: { type: string, enum: [role] }");
        contract.ShouldContain("AssignmentImpactKind:");
        contract.ShouldContain("enum: [exactRemoved, wildcardRemoved, wildcardImpacted]");
        contract.ShouldContain("impactKind: { $ref: '#/components/schemas/AssignmentImpactKind' }");
    }

    [Fact]
    public void Contract_DefinesSliceFourRemoteImportRoutesAndRequest()
    {
        string contract = ReadContract();

        contract.ShouldContain("/api/admin/application-permissions/applications/{applicationId}/import/preview:");
        contract.ShouldContain("/api/admin/application-permissions/applications/{applicationId}/import:");
        contract.ShouldContain("operationId: previewRemoteApplicationPermissionManifest");
        contract.ShouldContain("operationId: applyRemoteApplicationPermissionManifest");
        contract.ShouldContain("RemoteImportRequest:");
        contract.ShouldContain("concurrencyToken:");
    }

    [Fact]
    public void Contract_DefinesSliceFiveHistoryReplacementAndDiagnosticRoutes()
    {
        string contract = ReadContract();

        contract.ShouldContain("/api/admin/application-permissions/permissions/{permissionId}/replacement:");
        contract.ShouldContain("/api/admin/application-permissions/history:");
        contract.ShouldContain("/api/admin/application-permissions/diagnostics:");
        contract.ShouldContain("operationId: updateRemovedApplicationPermissionReplacement");
        contract.ShouldContain("operationId: listApplicationPermissionHistory");
        contract.ShouldContain("operationId: listApplicationPermissionDiagnostics");
        contract.ShouldContain("ReplacementGuidanceRequest:");
        contract.ShouldContain("ApplicationPermissionHistoryResponse:");
        contract.ShouldContain("PermissionDiagnosticsResponse:");
        contract.ShouldContain("enum: [malformed, unknown, orphaned, deletedApplication, tombstonedPermission, collapsedWildcard]");
    }

    private static string ReadContract()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, OpenApiContractPath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            string? parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
            {
                break;
            }

            directory = parent ?? string.Empty;
        }

        throw new FileNotFoundException($"Could not locate contract file '{OpenApiContractPath}'.");
    }
}
