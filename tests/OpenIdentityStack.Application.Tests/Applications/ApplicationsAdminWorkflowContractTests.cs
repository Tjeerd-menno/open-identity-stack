using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class ApplicationsAdminWorkflowContractTests
{
    [Fact]
    public async Task Workflow_exposes_operator_visible_application_administration_actions()
    {
        IApplicationsAdminWorkflow workflow = Substitute.For<IApplicationsAdminWorkflow>();
        DateTimeOffset now = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var applicationId = DomainApplicationId.NewId();
        var credentialId = Guid.NewGuid();

        ApplicationSummary summary = new(
            applicationId,
            ClientId: "portal",
            DisplayName: "Portal",
            Profile: ApplicationProfile.Web,
            ClientType: OAuthClientType.Confidential,
            Status: ApplicationStatus.Active,
            RequiresMigrationReview: false,
            CreatedAt: now,
            ModifiedAt: null);
        ApplicationDetails details = new(
            applicationId,
            ClientId: "portal",
            DisplayName: "Portal",
            Description: "Operator portal",
            Profile: ApplicationProfile.Web,
            ClientType: OAuthClientType.Confidential,
            Status: ApplicationStatus.Active,
            AllowedGrantTypes: ["authorization_code"],
            AllowedScopes: ["openid", "profile"],
            RedirectUris: ["https://portal.example.com/callback"],
            PostLogoutRedirectUris: ["https://portal.example.com/signout-callback"],
            RequirePkce: true,
            RequireConsent: false,
            RequiresMigrationReview: false,
            MigrationSource: null,
            CreatedAt: now,
            ModifiedAt: null);
        ApplicationSecretOperationResult credentialCommandResult = new(
            credentialId,
            OneTimeSecret: "generated-secret");
        ApplicationCredentialCommandResult certificateCredentialCommandResult = new(
            Guid.NewGuid(),
            applicationId,
            ApplicationCredentialType.X509Certificate,
            OneTimeSecret: null);
        ApplicationCredentialDetails credentialDetails = new(
            credentialId,
            applicationId,
            ApplicationCredentialType.ClientSecret,
            Thumbprint: null,
            Subject: null,
            Description: "Current secret",
            ExpiresAt: now.AddYears(1),
            CreatedAt: now,
            LastUsedAt: null,
            RevokedAt: null);
        ApplicationProfilePolicyDetails profilePolicy = new(
            ApplicationProfile.Web,
            IsSelectable: true,
            UnavailabilityReason: null,
            DefaultClientProfile: ClientProfile.Public,
            AllowedClientProfiles: [ClientProfile.Public],
            AllowedGrantTypes: ["authorization_code"],
            DefaultGrantTypes: ["authorization_code"],
            Options: new Dictionary<string, ApplicationOptionAvailability>(),
            RequirePkce: true,
            DefaultRequirePkce: true,
            DefaultRequireConsent: false,
            RequiresRedirectUris: true);

        workflow.CreateAsync(Arg.Any<CreateApplicationAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Success(new ApplicationCreateOperationResult(details, InitialSecret: "initial-secret")));
        workflow.UpdateMetadataAsync(Arg.Any<UpdateApplicationMetadataAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Success(details));
        workflow.ConfigureOAuthAsync(Arg.Any<ConfigureApplicationOAuthAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Success(details));
        workflow.EnableAsync(Arg.Any<EnableApplicationAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Success(details));
        workflow.DisableAsync(Arg.Any<DisableApplicationAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Success(details));
        workflow.DeleteAsync(Arg.Any<DeleteApplicationAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        workflow.AddSecretAsync(Arg.Any<AddApplicationSecretAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Success(credentialCommandResult));
        workflow.AddCertificateAsync(Arg.Any<AddApplicationCertificateAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Success(certificateCredentialCommandResult));
        workflow.RevokeCredentialAsync(Arg.Any<RevokeApplicationCredentialAdminWorkflowRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        CreateApplicationAdminWorkflowRequest createRequest = new(
            ClientId: "portal",
            DisplayName: "Portal",
            Description: "Operator portal",
            Profile: ApplicationProfile.Web,
            ClientType: OAuthClientType.Confidential,
            AllowedGrantTypes: ["authorization_code"],
            AllowedScopes: ["openid", "profile"],
            RedirectUris: ["https://portal.example.com/callback"],
            PostLogoutRedirectUris: ["https://portal.example.com/signout-callback"],
            RequirePkce: true,
            RequireConsent: false,
            InitialCredential: new CreateInitialCredentialAdminWorkflowRequest(
                Type: "ClientSecret",
                Description: "Initial secret",
                ExpiresAt: now.AddYears(1)));
        UpdateApplicationMetadataAdminWorkflowRequest updateMetadataRequest = new(
            applicationId,
            DisplayName: "Portal",
            Description: "Operator portal");
        ConfigureApplicationOAuthAdminWorkflowRequest configureOAuthRequest = new(
            applicationId,
            Profile: ApplicationProfile.Web,
            ClientType: OAuthClientType.Confidential,
            AllowedGrantTypes: ["authorization_code"],
            AllowedScopes: ["openid", "profile"],
            RedirectUris: ["https://portal.example.com/callback"],
            PostLogoutRedirectUris: ["https://portal.example.com/signout-callback"],
            RequirePkce: true,
            RequireConsent: false);
        EnableApplicationAdminWorkflowRequest enableRequest = new(applicationId);
        DisableApplicationAdminWorkflowRequest disableRequest = new(applicationId);
        DeleteApplicationAdminWorkflowRequest deleteRequest = new(applicationId);
        AddApplicationSecretAdminWorkflowRequest addSecretRequest = new(
            applicationId,
            Description: "Current secret",
            ExpiresAt: now.AddYears(1),
            RevokeExisting: true);
        AddApplicationCertificateAdminWorkflowRequest addCertificateRequest = new(
            applicationId,
            Thumbprint: "0123456789ABCDEF",
            Subject: "CN=portal",
            Description: "Portal certificate",
            ExpiresAt: now.AddYears(1));
        RevokeApplicationCredentialAdminWorkflowRequest revokeCredentialRequest = new(applicationId, credentialId);
        Result<ApplicationCreateOperationResult> createResult = await workflow.CreateAsync(createRequest);
        Result<ApplicationDetails> updateMetadataResult = await workflow.UpdateMetadataAsync(updateMetadataRequest);
        Result<ApplicationDetails> configureOAuthResult = await workflow.ConfigureOAuthAsync(configureOAuthRequest);
        Result<ApplicationDetails> enableResult = await workflow.EnableAsync(enableRequest);
        Result<ApplicationDetails> disableResult = await workflow.DisableAsync(disableRequest);
        Result deleteResult = await workflow.DeleteAsync(deleteRequest);
        Result<ApplicationSecretOperationResult> addSecretResult = await workflow.AddSecretAsync(addSecretRequest);
        Result<ApplicationCredentialCommandResult> addCertificateResult = await workflow.AddCertificateAsync(addCertificateRequest);
        Result revokeCredentialResult = await workflow.RevokeCredentialAsync(revokeCredentialRequest);
        createResult.Value.Details.ShouldBe(details);
        createResult.Value.InitialSecret.ShouldBe("initial-secret");
        updateMetadataResult.Value.ShouldBe(details);
        configureOAuthResult.Value.ShouldBe(details);
        enableResult.Value.ShouldBe(details);
        disableResult.Value.ShouldBe(details);
        deleteResult.IsSuccess.ShouldBeTrue();
        addSecretResult.Value.ShouldBe(credentialCommandResult);
        addCertificateResult.Value.ShouldBe(certificateCredentialCommandResult);
        revokeCredentialResult.IsSuccess.ShouldBeTrue();
    }

    private static Result<T> Success<T>(T value) => value;
}
