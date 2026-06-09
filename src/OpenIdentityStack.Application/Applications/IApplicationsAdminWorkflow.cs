using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Application.Common;
using SharedKernel;

namespace OpenIdentityStack.Application.Applications;

public interface IApplicationsAdminWorkflow
{
    Task<Result<PagedResult<ApplicationSummary>>> ListAsync(
        ListApplicationsAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationDetails>> GetDetailsAsync(
        GetApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationCreateOperationResult>> CreateAsync(
        CreateApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationDetails>> UpdateMetadataAsync(
        UpdateApplicationMetadataAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationDetails>> ConfigureOAuthAsync(
        ConfigureApplicationOAuthAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationDetails>> EnableAsync(
        EnableApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationDetails>> DisableAsync(
        DisableApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        DeleteApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ApplicationCredentialDetails>>> ListCredentialsAsync(
        ListApplicationCredentialsAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationSecretOperationResult>> AddSecretAsync(
        AddApplicationSecretAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApplicationCredentialCommandResult>> AddCertificateAsync(
        AddApplicationCertificateAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeCredentialAsync(
        RevokeApplicationCredentialAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ApplicationProfilePolicyDetails>>> ListProfilePoliciesAsync(
        ListApplicationProfilePoliciesAdminWorkflowRequest request,
        CancellationToken cancellationToken = default);
}
