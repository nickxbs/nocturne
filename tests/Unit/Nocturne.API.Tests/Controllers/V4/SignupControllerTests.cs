using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Configuration;
using Nocturne.API.Controllers.V4;
using Nocturne.API.Extensions;
using Nocturne.API.Multitenancy;
using Nocturne.API.Services.Auth;
using Nocturne.API.Services.Identity;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.Notifications;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Tests.Shared;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4;

public class SignupControllerTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly NocturneDbContext _dbContext;
    private readonly Mock<ITenantService> _tenantService;
    private readonly Mock<IPasskeyService> _passkeyService;
    private readonly Mock<IRecoveryCodeService> _recoveryCodeService;
    private readonly Mock<ILoginCodeService> _loginCodeService;
    private readonly Mock<ISubjectService> _subjectService;
    private readonly Mock<IInAppNotificationService> _notificationService;
    private readonly Mock<IInstanceSetupState> _setupState;
    private readonly Mock<IUsernameValidator> _usernameValidator;
    private readonly Mock<ITenantMemberService> _tenantMemberService;
    private readonly BaseDomainOptions _baseDomainOptions;

    public SignupControllerTests()
    {
        _db = TestDbContextFactory.CreateSqlite();
        _dbContext = _db.CreateContext();

        _tenantService = new Mock<ITenantService>();
        _passkeyService = new Mock<IPasskeyService>();
        _recoveryCodeService = new Mock<IRecoveryCodeService>();
        _loginCodeService = new Mock<ILoginCodeService>();
        _subjectService = new Mock<ISubjectService>();
        _notificationService = new Mock<IInAppNotificationService>();
        _setupState = new Mock<IInstanceSetupState>();
        _usernameValidator = new Mock<IUsernameValidator>();
        _tenantMemberService = new Mock<ITenantMemberService>();

        _baseDomainOptions = new BaseDomainOptions
        {
            BaseDomain = "nocturne.test",
        };

        // Defaults: setup is complete, username and slug are valid, no existing memberships
        _setupState.Setup(s => s.IsSetupCompleteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _usernameValidator.Setup(v => v.ValidateForSignupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(true));

        _tenantService.Setup(t => t.ValidateSlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(true));

        _tenantMemberService.Setup(m => m.GetTenantIdsForSubjectAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _db.Dispose();
    }

    private SignupController BuildController(
        bool enabled = true,
        bool allowSelfService = true,
        bool requiresApproval = false,
        string? host = "nocturne.test")
    {
        var operatorConfig = new OperatorConfiguration
        {
            AllowSelfServiceCreation = allowSelfService,
            Signup = new OperatorSignupConfiguration
            {
                Enabled = enabled,
                RequiresApproval = requiresApproval,
            },
        };

        var httpContext = new DefaultHttpContext();
        if (host != null)
        {
            httpContext.Request.Host = new HostString(host);
            httpContext.Request.Scheme = "https";
        }

        return new SignupController(
            _tenantService.Object,
            _passkeyService.Object,
            _recoveryCodeService.Object,
            _loginCodeService.Object,
            _subjectService.Object,
            _notificationService.Object,
            _db.ContextFactory.AsNocturneFactory(),
            Options.Create(operatorConfig),
            Options.Create(_baseDomainOptions),
            _setupState.Object,
            _usernameValidator.Object,
            _tenantMemberService.Object,
            NullLogger<SignupController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    // ── ValidateUsername ──────────────────────────────────────────────────

    [Fact]
    public async Task ValidateUsername_WhenSignupDisabled_Returns404()
    {
        var controller = BuildController(enabled: false);

        var result = await controller.ValidateUsername("alice", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ValidateUsername_WhenSetupIncomplete_Returns409()
    {
        _setupState.Setup(s => s.IsSetupCompleteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = BuildController();

        var result = await controller.ValidateUsername("alice", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ValidateUsername_WhenAllowed_DelegatesToUsernameValidator()
    {
        _usernameValidator.Setup(v => v.ValidateForSignupAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(true));
        var controller = BuildController();

        var result = await controller.ValidateUsername("alice", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SlugValidationResult>()
            .Which.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSlug_WhenSignupDisabled_Returns404()
    {
        var controller = BuildController(enabled: false);

        var result = await controller.ValidateSlug("my-tenant", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ValidateSlug_WhenAllowed_DelegatesToTenantService()
    {
        _tenantService.Setup(t => t.ValidateSlugAsync("my-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(true));
        var controller = BuildController();

        var result = await controller.ValidateSlug("my-tenant", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SlugValidationResult>()
            .Which.IsValid.Should().BeTrue();
    }

    // ── Options ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Options_WhenSignupDisabled_Returns404()
    {
        var controller = BuildController(enabled: false);

        var result = await controller.Options(new SignupOptionsRequest
        {
            Username = "alice",
            DisplayName = "Alice",
            TenantSlug = "alice-tenant",
            TenantDisplayName = "Alice Clinic",
        }, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Options_WhenAllowSelfServiceFalse_Returns404()
    {
        var controller = BuildController(allowSelfService: false);

        var result = await controller.Options(new SignupOptionsRequest
        {
            Username = "alice",
            DisplayName = "Alice",
            TenantSlug = "alice-tenant",
            TenantDisplayName = "Alice Clinic",
        }, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Options_WhenSetupIncomplete_Returns409()
    {
        _setupState.Setup(s => s.IsSetupCompleteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = BuildController();

        var result = await controller.Options(new SignupOptionsRequest
        {
            Username = "alice",
            DisplayName = "Alice",
            TenantSlug = "alice-tenant",
            TenantDisplayName = "Alice Clinic",
        }, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Options_WhenUsernameValidationFails_Returns400()
    {
        _usernameValidator.Setup(v => v.ValidateForSignupAsync("reserved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(false, "Reserved name"));
        var controller = BuildController();

        var result = await controller.Options(new SignupOptionsRequest
        {
            Username = "reserved",
            DisplayName = "Reserved",
            TenantSlug = "fresh-tenant",
            TenantDisplayName = "Fresh Clinic",
        }, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Options_WhenTenantSlugValidationFails_Returns400()
    {
        _tenantService.Setup(t => t.ValidateSlugAsync("taken-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(false, "Already taken"));
        var controller = BuildController();

        var result = await controller.Options(new SignupOptionsRequest
        {
            Username = "alice",
            DisplayName = "Alice",
            TenantSlug = "taken-slug",
            TenantDisplayName = "Taken",
        }, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Options_WhenValid_CreatesEnrollingSubjectAndReturnsOptions()
    {
        _passkeyService.Setup(p => p.GenerateRegistrationOptionsAsync(It.IsAny<Guid>(), "alice"))
            .ReturnsAsync(new PasskeyRegistrationOptions("{\"challenge\":\"xyz\"}", "challenge-token-123"));
        var controller = BuildController();

        var result = await controller.Options(new SignupOptionsRequest
        {
            Username = "alice",
            DisplayName = "Alice Wonderland",
            TenantSlug = "alice-tenant",
            TenantDisplayName = "Alice Clinic",
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SignupOptionsResponse>().Subject;
        response.Options.Should().Be("{\"challenge\":\"xyz\"}");
        response.ChallengeToken.Should().Be("challenge-token-123");

        // Verify subject stored with IsActive = false
        using var verifyCtx = _db.CreateContext();
        var subject = verifyCtx.Subjects.FirstOrDefault(s => s.Username == "alice");
        subject.Should().NotBeNull();
        subject!.IsActive.Should().BeFalse();
        subject.Name.Should().Be("Alice Wonderland");
    }

    [Fact]
    public async Task Options_WhenRetried_ReusesEnrollingSubject()
    {
        _passkeyService.Setup(p => p.GenerateRegistrationOptionsAsync(It.IsAny<Guid>(), "alice"))
            .ReturnsAsync(new PasskeyRegistrationOptions("{}", "tok"));
        var controller = BuildController();

        await controller.Options(new SignupOptionsRequest
        {
            Username = "alice",
            DisplayName = "Alice First",
            TenantSlug = "alice-slug",
            TenantDisplayName = "Alice Clinic",
        }, CancellationToken.None);

        await controller.Options(new SignupOptionsRequest
        {
            Username = "alice",
            DisplayName = "Alice Updated",
            TenantSlug = "alice-slug",
            TenantDisplayName = "Alice Clinic",
        }, CancellationToken.None);

        using var verifyCtx = _db.CreateContext();
        var subjects = verifyCtx.Subjects.Where(s => s.Username == "alice").ToList();
        subjects.Should().HaveCount(1);
        subjects[0].Name.Should().Be("Alice Updated");
    }

    // ── Complete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Complete_WhenNoPendingSubject_Returns400()
    {
        var controller = BuildController();

        var result = await controller.Complete(new SignupCompleteRequest
        {
            Username = "unknown-user",
            TenantSlug = "fresh-slug",
            TenantDisplayName = "Fresh",
            AttestationResponseJson = "{}",
            ChallengeToken = "token",
        }, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Complete_WhenValidAndNoApprovalRequired_SucceedsWithHandoff()
    {
        var subjectId = Guid.CreateVersion7();
        using (var setupCtx = _db.CreateContext())
        {
            setupCtx.Subjects.Add(new SubjectEntity
            {
                Id = subjectId,
                Username = "alice",
                Name = "Alice",
                IsActive = false,
                IsSystemSubject = false,
            });
            setupCtx.SaveChanges();
        }

        var tenantId = Guid.CreateVersion7();
        _tenantService.Setup(t => t.CreateAsync("alice-slug", "Alice Clinic", subjectId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(tenantId, "alice-slug", "Alice Clinic", true, DateTime.UtcNow));

        _passkeyService.Setup(p => p.CompleteRegistrationAsync("{}", "tok", tenantId, subjectId, null))
            .ReturnsAsync(new PasskeyCredentialResult(Guid.CreateVersion7(), subjectId));

        _recoveryCodeService.Setup(r => r.GenerateCodesAsync(subjectId))
            .ReturnsAsync(["code-1", "code-2"]);

        _loginCodeService.Setup(l => l.IssueAsync(
                It.IsAny<NocturneDbContext>(), subjectId, It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<AuthAuditActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginCode("secret-login-code", DateTime.UtcNow.AddMinutes(5)));

        var controller = BuildController(requiresApproval: false);

        var result = await controller.Complete(new SignupCompleteRequest
        {
            Username = "alice",
            TenantSlug = "alice-slug",
            TenantDisplayName = "Alice Clinic",
            AttestationResponseJson = "{}",
            ChallengeToken = "tok",
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SignupCompleteResponse>().Subject;
        response.Success.Should().BeTrue();
        response.PendingApproval.Should().BeFalse();
        response.RecoveryCodes.Should().Contain(["code-1", "code-2"]);
        response.HandoffUrl.Should().Be("https://alice-slug.nocturne.test/auth/handoff?code=secret-login-code");

        // Verify subject was activated
        using var verifyCtx = _db.CreateContext();
        var activatedSubject = verifyCtx.Subjects.First(s => s.Id == subjectId);
        activatedSubject.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Complete_WhenRequiresApproval_SucceedsWithPendingApprovalAndNoHandoff()
    {
        var subjectId = Guid.CreateVersion7();
        using (var setupCtx = _db.CreateContext())
        {
            setupCtx.Subjects.Add(new SubjectEntity
            {
                Id = subjectId,
                Username = "bob",
                Name = "Bob",
                IsActive = false,
                IsSystemSubject = false,
            });
            setupCtx.SaveChanges();
        }

        var tenantId = Guid.CreateVersion7();
        _tenantService.Setup(t => t.CreateAsync("bob-slug", "Bob Clinic", subjectId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(tenantId, "bob-slug", "Bob Clinic", false, DateTime.UtcNow));

        _passkeyService.Setup(p => p.CompleteRegistrationAsync("{}", "tok", tenantId, subjectId, null))
            .ReturnsAsync(new PasskeyCredentialResult(Guid.CreateVersion7(), subjectId));

        _recoveryCodeService.Setup(r => r.GenerateCodesAsync(subjectId))
            .ReturnsAsync(["rec-1"]);

        var controller = BuildController(requiresApproval: true);

        var result = await controller.Complete(new SignupCompleteRequest
        {
            Username = "bob",
            TenantSlug = "bob-slug",
            TenantDisplayName = "Bob Clinic",
            AttestationResponseJson = "{}",
            ChallengeToken = "tok",
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SignupCompleteResponse>().Subject;
        response.Success.Should().BeTrue();
        response.PendingApproval.Should().BeTrue();
        response.HandoffUrl.Should().BeNull();
    }

    [Fact]
    public async Task Complete_WhenRegistrationThrows_RollsBackTenantAndSubject()
    {
        var subjectId = Guid.CreateVersion7();
        using (var setupCtx = _db.CreateContext())
        {
            setupCtx.Subjects.Add(new SubjectEntity
            {
                Id = subjectId,
                Username = "charlie",
                Name = "Charlie",
                IsActive = false,
                IsSystemSubject = false,
            });
            setupCtx.SaveChanges();
        }

        var tenantId = Guid.CreateVersion7();
        _tenantService.Setup(t => t.CreateAsync("charlie-slug", "Charlie Clinic", subjectId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(tenantId, "charlie-slug", "Charlie Clinic", true, DateTime.UtcNow));

        _passkeyService.Setup(p => p.CompleteRegistrationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), null))
            .ThrowsAsync(new InvalidOperationException("Attestation failed"));

        var controller = BuildController();

        var result = await controller.Complete(new SignupCompleteRequest
        {
            Username = "charlie",
            TenantSlug = "charlie-slug",
            TenantDisplayName = "Charlie Clinic",
            AttestationResponseJson = "{}",
            ChallengeToken = "bad-tok",
        }, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(400);

        _tenantService.Verify(t => t.DeleteAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once());
        _subjectService.Verify(s => s.DeleteSubjectAsync(subjectId), Times.Once());
    }

    [Fact]
    public async Task Complete_WhenRequiresApproval_DispatchesNotificationToPlatformAdmins()
    {
        var adminId = Guid.NewGuid();
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = adminId,
            Name = "Platform Admin",
            IsPlatformAdmin = true,
            IsActive = true,
            IsSystemSubject = false,
        });

        var subjectId = Guid.NewGuid();
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = subjectId,
            Name = "Enrolling User",
            Username = "enrollinguser",
            IsActive = false,
            IsSystemSubject = false,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        var tenantId = Guid.NewGuid();
        _tenantService
            .Setup(t => t.CreateAsync("mytenant", "My Tenant", subjectId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(tenantId, "mytenant", "My Tenant", false, DateTime.UtcNow));

        _passkeyService
            .Setup(p => p.CompleteRegistrationAsync(
                It.IsAny<string>(), It.IsAny<string>(), tenantId, subjectId, null))
            .ReturnsAsync(new PasskeyCredentialResult(Guid.NewGuid(), subjectId));

        _recoveryCodeService
            .Setup(r => r.GenerateCodesAsync(subjectId))
            .ReturnsAsync(["code-1", "code-2"]);

        var controller = BuildController(requiresApproval: true);

        var request = new SignupCompleteRequest
        {
            Username = "enrollinguser",
            TenantSlug = "mytenant",
            TenantDisplayName = "My Tenant",
            AttestationResponseJson = "{}",
            ChallengeToken = "token-123",
        };

        var result = await controller.Complete(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SignupCompleteResponse>().Subject;
        response.Success.Should().BeTrue();
        response.PendingApproval.Should().BeTrue();
        response.HandoffUrl.Should().BeNull();

        _notificationService.Verify(
            n => n.CreateNotificationAsync(
                adminId.ToString(),
                "tenant.approval_requested",
                "Tenant approval requested: My Tenant",
                It.IsAny<NotificationCategory?>(),
                It.IsAny<NotificationUrgency?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                "Slug: mytenant, Owner: enrollinguser",
                tenantId.ToString(),
                It.IsAny<List<NotificationActionDto>?>(),
                It.IsAny<ResolutionConditions?>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Onboarding ────────────────────────────────────────────────────────

    [Fact]
    public async Task Onboarding_WhenSignupDisabled_Returns404()
    {
        var controller = BuildController(enabled: false);

        var result = await controller.Onboarding(new OnboardingRequest
        {
            TenantSlug = "myslug",
            TenantDisplayName = "My Workspace",
        }, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Onboarding_WhenUnauthenticated_Returns401()
    {
        var controller = BuildController(enabled: true);

        var result = await controller.Onboarding(new OnboardingRequest
        {
            TenantSlug = "myslug",
            TenantDisplayName = "My Workspace",
        }, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Onboarding_WhenUserAlreadyHasTenant_Returns409()
    {
        var subjectId = Guid.NewGuid();
        var controller = BuildController(enabled: true);
        controller.HttpContext.SetAuthContext(new AuthContext
        {
            IsAuthenticated = true,
            SubjectId = subjectId,
            AuthType = AuthType.SessionCookie,
        });

        _tenantMemberService.Setup(m => m.GetTenantIdsForSubjectAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Guid.NewGuid()]);

        var result = await controller.Onboarding(new OnboardingRequest
        {
            TenantSlug = "myslug",
            TenantDisplayName = "My Workspace",
        }, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Onboarding_WhenValid_ProvisionsTenantAndReturnsHandoffUrl()
    {
        var subjectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = subjectId,
            Name = "Google User",
            Username = "googleuser",
            IsActive = true,
        });
        await _dbContext.SaveChangesAsync();

        _tenantService
            .Setup(t => t.CreateAsync("myworkspace", "My Workspace", subjectId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(tenantId, "myworkspace", "My Workspace", true, DateTime.UtcNow));

        _loginCodeService
            .Setup(l => l.IssueAsync(
                It.IsAny<NocturneDbContext>(),
                subjectId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuthAuditActor>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginCode("code-xyz", DateTime.UtcNow.AddMinutes(5)));

        var controller = BuildController(enabled: true);
        controller.HttpContext.SetAuthContext(new AuthContext
        {
            IsAuthenticated = true,
            SubjectId = subjectId,
            AuthType = AuthType.SessionCookie,
        });

        var result = await controller.Onboarding(new OnboardingRequest
        {
            TenantSlug = "myworkspace",
            TenantDisplayName = "My Workspace",
        }, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SignupCompleteResponse>().Subject;
        response.Success.Should().BeTrue();
        response.PendingApproval.Should().BeFalse();
        response.HandoffUrl.Should().Be("https://myworkspace.nocturne.test/auth/handoff?code=code-xyz");
    }

    [Fact]
    public async Task Onboarding_WhenRequiresApproval_ReturnsPendingApproval()
    {
        var subjectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = subjectId,
            Name = "Google User",
            Username = "googleuser",
            IsActive = true,
        });
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = adminId,
            Name = "Platform Admin",
            Username = "admin",
            IsActive = true,
            IsPlatformAdmin = true,
        });
        await _dbContext.SaveChangesAsync();

        _tenantService
            .Setup(t => t.CreateAsync("myworkspace", "My Workspace", subjectId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(tenantId, "myworkspace", "My Workspace", false, DateTime.UtcNow));

        var controller = BuildController(enabled: true, requiresApproval: true);
        controller.HttpContext.SetAuthContext(new AuthContext
        {
            IsAuthenticated = true,
            SubjectId = subjectId,
            AuthType = AuthType.SessionCookie,
        });

        var result = await controller.Onboarding(new OnboardingRequest
        {
            TenantSlug = "myworkspace",
            TenantDisplayName = "My Workspace",
        }, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SignupCompleteResponse>().Subject;
        response.Success.Should().BeTrue();
        response.PendingApproval.Should().BeTrue();
        response.HandoffUrl.Should().BeNull();

        _notificationService.Verify(
            n => n.CreateNotificationAsync(
                adminId.ToString(),
                "tenant.approval_requested",
                "Tenant approval requested: My Workspace",
                It.IsAny<NotificationCategory?>(),
                It.IsAny<NotificationUrgency?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                "Slug: myworkspace, Owner: Google User",
                tenantId.ToString(),
                It.IsAny<List<NotificationActionDto>?>(),
                It.IsAny<ResolutionConditions?>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
