using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenApi.Remote.Attributes;
using Nocturne.API.Authorization;
using Nocturne.API.Configuration;
using Nocturne.API.Extensions;
using Nocturne.API.Multitenancy;
using Nocturne.API.Services.Auth;
using Nocturne.API.Services.Identity;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.Notifications;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Services;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Endpoints for public self-service registration and personal tenant provisioning.
/// Operates on the apex domain without an existing tenant.
/// </summary>
[ApiController]
[Tags("Identity")]
[Route("api/v4/signup")]
[Produces("application/json")]
[AllowAnonymous]
[DenyDemoSubject]
public class SignupController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly IPasskeyService _passkeyService;
    private readonly IRecoveryCodeService _recoveryCodeService;
    private readonly ILoginCodeService _loginCodeService;
    private readonly ISubjectService _subjectService;
    private readonly IInAppNotificationService _notificationService;
    private readonly INocturneDbContextFactory _dbFactory;
    private readonly OperatorConfiguration _operatorConfig;
    private readonly BaseDomainOptions _baseDomainOptions;
    private readonly IInstanceSetupState _setupState;
    private readonly IUsernameValidator _usernameValidator;
    private readonly ITenantMemberService _tenantMemberService;
    private readonly ILogger<SignupController> _logger;

    public SignupController(
        ITenantService tenantService,
        IPasskeyService passkeyService,
        IRecoveryCodeService recoveryCodeService,
        ILoginCodeService loginCodeService,
        ISubjectService subjectService,
        IInAppNotificationService notificationService,
        INocturneDbContextFactory dbFactory,
        IOptions<OperatorConfiguration> operatorConfig,
        IOptions<BaseDomainOptions> baseDomainOptions,
        IInstanceSetupState setupState,
        IUsernameValidator usernameValidator,
        ITenantMemberService tenantMemberService,
        ILogger<SignupController> logger)
    {
        _tenantService = tenantService;
        _passkeyService = passkeyService;
        _recoveryCodeService = recoveryCodeService;
        _loginCodeService = loginCodeService;
        _subjectService = subjectService;
        _notificationService = notificationService;
        _dbFactory = dbFactory;
        _operatorConfig = operatorConfig.Value;
        _baseDomainOptions = baseDomainOptions.Value;
        _setupState = setupState;
        _usernameValidator = usernameValidator;
        _tenantMemberService = tenantMemberService;
        _logger = logger;
    }

    /// <summary>
    /// Check whether a username is available for self-service registration.
    /// </summary>
    [HttpGet("validate-username")]
    [EnableRateLimiting("name-availability")]
    [RemoteQuery]
    [ProducesResponseType(typeof(SlugValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ValidateUsername(
        [FromQuery] string username, CancellationToken ct)
    {
        if (await CheckSignupDisabledOrSetupIncompleteAsync(ct) is { } guardResult)
            return guardResult;

        var result = await _usernameValidator.ValidateForSignupAsync(username, ct);
        return Ok(result);
    }

    /// <summary>
    /// Check whether a tenant slug is available for self-service registration.
    /// </summary>
    [HttpGet("validate-slug")]
    [EnableRateLimiting("name-availability")]
    [RemoteQuery]
    [ProducesResponseType(typeof(SlugValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ValidateSlug(
        [FromQuery] string slug, CancellationToken ct)
    {
        if (await CheckSignupDisabledOrSetupIncompleteAsync(ct) is { } guardResult)
            return guardResult;

        if (string.IsNullOrWhiteSpace(slug))
            return Ok(new SlugValidationResult(false, "Slug is required"));

        var result = await _tenantService.ValidateSlugAsync(slug, ct);
        return Ok(result);
    }

    /// <summary>
    /// Initiate passkey registration ceremony for public signup.
    /// Creates or updates an inactive enrolling subject and issues WebAuthn registration options.
    /// </summary>
    [HttpPost("options")]
    [EnableRateLimiting("signup")]
    [RemoteCommand]
    [ProducesResponseType(typeof(SignupOptionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Options(
        [FromBody] SignupOptionsRequest request, CancellationToken ct)
    {
        if (this.PasskeyHostRefusal(_passkeyService) is { } refusal)
            return refusal;

        if (await CheckSignupDisabledOrSetupIncompleteAsync(ct) is { } guardResult)
            return guardResult;

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.DisplayName))
            return Problem(detail: "Username and display name are required", statusCode: 400, title: "Bad Request");

        if (string.IsNullOrWhiteSpace(request.TenantSlug) || string.IsNullOrWhiteSpace(request.TenantDisplayName))
            return Problem(detail: "Tenant slug and display name are required", statusCode: 400, title: "Bad Request");

        var usernameValidation = await _usernameValidator.ValidateForSignupAsync(request.Username, ct);
        if (!usernameValidation.IsValid)
            return Problem(detail: usernameValidation.Message, statusCode: 400, title: "Bad Request");

        var slugValidation = await _tenantService.ValidateSlugAsync(request.TenantSlug, ct);
        if (!slugValidation.IsValid)
            return Problem(detail: slugValidation.Message, statusCode: 400, title: "Bad Request");

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        await using var context = await _dbFactory.CreatePlatformAsync(ct);

        var existingSubject = await context.Subjects
            .Where(s => s.Username == normalizedUsername && !s.IsActive && !s.IsSystemSubject)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        Guid subjectId;
        if (existingSubject != null)
        {
            subjectId = existingSubject.Id;
            existingSubject.Name = request.DisplayName.Trim();
            await context.SaveChangesAsync(ct);
        }
        else
        {
            subjectId = Guid.CreateVersion7();
            context.Subjects.Add(new SubjectEntity
            {
                Id = subjectId,
                Name = request.DisplayName.Trim(),
                Username = normalizedUsername,
                IsActive = false,
                IsSystemSubject = false,
            });
            await context.SaveChangesAsync(ct);
        }

        var result = await _passkeyService.GenerateRegistrationOptionsAsync(
            subjectId, normalizedUsername);

        return Ok(new SignupOptionsResponse
        {
            Options = result.OptionsJson,
            ChallengeToken = result.ChallengeToken,
        });
    }

    /// <summary>
    /// Complete passkey registration, activate the subject, create the personal tenant,
    /// generate recovery codes, and issue a login handoff code.
    /// </summary>
    [HttpPost("complete")]
    [EnableRateLimiting("signup")]
    [RemoteCommand]
    [ProducesResponseType(typeof(SignupCompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete(
        [FromBody] SignupCompleteRequest request, CancellationToken ct)
    {
        if (await CheckSignupDisabledOrSetupIncompleteAsync(ct) is { } guardResult)
            return guardResult;

        if (string.IsNullOrWhiteSpace(request.Username))
            return Problem(detail: "Username is required", statusCode: 400, title: "Bad Request");

        if (string.IsNullOrWhiteSpace(request.TenantSlug) || string.IsNullOrWhiteSpace(request.TenantDisplayName))
            return Problem(detail: "Tenant slug and display name are required", statusCode: 400, title: "Bad Request");

        if (string.IsNullOrWhiteSpace(request.AttestationResponseJson) || string.IsNullOrWhiteSpace(request.ChallengeToken))
            return Problem(detail: "Attestation response and challenge token are required", statusCode: 400, title: "Bad Request");

        var slugValidation = await _tenantService.ValidateSlugAsync(request.TenantSlug, ct);
        if (!slugValidation.IsValid)
            return Problem(detail: slugValidation.Message, statusCode: 400, title: "Bad Request");

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        await using var platformCtx = await _dbFactory.CreatePlatformAsync(ct);

        var enrollingSubject = await platformCtx.Subjects
            .Where(s => s.Username == normalizedUsername && !s.IsActive && !s.IsSystemSubject)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (enrollingSubject == null)
        {
            return Problem(
                detail: "No pending signup found for this username. Please restart the signup process.",
                statusCode: 400,
                title: "Bad Request");
        }

        var subjectId = enrollingSubject.Id;
        Guid? createdTenantId = null;

        try
        {
            var requiresApproval = _operatorConfig.Signup.RequiresApproval;

            var tenant = await _tenantService.CreateAsync(
                request.TenantSlug.Trim(),
                request.TenantDisplayName.Trim(),
                subjectId,
                isActive: !requiresApproval,
                ct: ct);
            createdTenantId = tenant.Id;

            await using (var memberCtx = await _dbFactory.CreateForTenantAsync(tenant.Id, ct))
            {
                var membership = await memberCtx.TenantMembers
                    .FirstOrDefaultAsync(m => m.TenantId == tenant.Id && m.SubjectId == subjectId, ct);
                if (membership != null)
                {
                    membership.Username = normalizedUsername;
                    await memberCtx.SaveChangesAsync(ct);
                }
            }

            var credResult = await _passkeyService.CompleteRegistrationAsync(
                request.AttestationResponseJson,
                request.ChallengeToken,
                tenant.Id,
                expectedSubjectId: subjectId);

            enrollingSubject.IsActive = true;
            await platformCtx.SaveChangesAsync(ct);

            var recoveryCodes = await _recoveryCodeService.GenerateCodesAsync(subjectId);

            string? handoffUrl = null;
            if (!requiresApproval)
            {
                await using var tenantCtx = await _dbFactory.CreateForTenantAsync(tenant.Id, ct);
                var issued = await _loginCodeService.IssueAsync(
                    tenantCtx,
                    subjectId,
                    LoginCodePurpose.Login,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.ToString(),
                    new AuthAuditActor(subjectId, null),
                    ct);

                var (host, port) = _baseDomainOptions.SplitHostPort();
                var hostWithSubdomain = $"{tenant.Slug}.{host}" + (port.HasValue ? $":{port}" : "");
                var scheme = Request.Scheme;
                handoffUrl = $"{scheme}://{hostWithSubdomain}/auth/handoff?code={Uri.EscapeDataString(issued.Code)}";
            }
            else
            {
                var adminSubjectIds = await platformCtx.Subjects
                    .Where(s => s.IsPlatformAdmin && s.IsActive && !s.IsSystemSubject)
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                foreach (var adminId in adminSubjectIds)
                {
                    await _notificationService.CreateNotificationAsync(
                        adminId.ToString(),
                        "tenant.approval_requested",
                        $"Tenant approval requested: {tenant.DisplayName}",
                        subtitle: $"Slug: {tenant.Slug}, Owner: {normalizedUsername}",
                        sourceId: tenant.Id.ToString(),
                        cancellationToken: ct);
                }
            }

            _logger.LogInformation(
                "Signup completed: subject {SubjectId} ({Username}) provisioned tenant {TenantId} ({Slug}), approval={Approval}",
                subjectId, normalizedUsername, tenant.Id, tenant.Slug, requiresApproval);

            return Ok(new SignupCompleteResponse
            {
                Success = true,
                PendingApproval = requiresApproval,
                HandoffUrl = handoffUrl,
                RecoveryCodes = recoveryCodes,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signup completion failed for username {Username}, rolling back", normalizedUsername);

            if (createdTenantId.HasValue)
            {
                try
                {
                    await _tenantService.DeleteAsync(createdTenantId.Value, CancellationToken.None);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback tenant {TenantId}", createdTenantId.Value);
                }
            }

            try
            {
                await _subjectService.DeleteSubjectAsync(subjectId);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Failed to rollback subject {SubjectId}", subjectId);
            }

            return Problem(detail: "Passkey registration or tenant provisioning failed", statusCode: 400, title: "Signup Failed");
        }
    }

    /// <summary>
    /// Provision a personal tenant for an already-authenticated subject (e.g., following OIDC sign-in).
    /// </summary>
    [HttpPost("onboarding")]
    [EnableRateLimiting("signup")]
    [RemoteCommand]
    [ProducesResponseType(typeof(SignupCompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Onboarding(
        [FromBody] OnboardingRequest request, CancellationToken ct)
    {
        if (await CheckSignupDisabledOrSetupIncompleteAsync(ct) is { } guardResult)
            return guardResult;

        var authContext = HttpContext.GetAuthContext();
        if (authContext == null || !authContext.IsAuthenticated || authContext.SubjectId == null)
            return Unauthorized();

        var subjectId = authContext.SubjectId.Value;

        if (string.IsNullOrWhiteSpace(request.TenantSlug) || string.IsNullOrWhiteSpace(request.TenantDisplayName))
            return Problem(detail: "Tenant slug and display name are required", statusCode: 400, title: "Bad Request");

        var slugValidation = await _tenantService.ValidateSlugAsync(request.TenantSlug, ct);
        if (!slugValidation.IsValid)
            return Problem(detail: slugValidation.Message, statusCode: 400, title: "Bad Request");

        var existingTenants = await _tenantMemberService.GetTenantIdsForSubjectAsync(subjectId, ct);
        if (existingTenants.Count > 0)
            return Conflict(new { error = "already_has_tenant", message = "Subject already has an active workspace." });

        await using var platformCtx = await _dbFactory.CreatePlatformAsync(ct);
        var subject = await platformCtx.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId, ct);
        if (subject == null)
            return Unauthorized();

        Guid? createdTenantId = null;

        try
        {
            var requiresApproval = _operatorConfig.Signup.RequiresApproval;

            var tenant = await _tenantService.CreateAsync(
                request.TenantSlug.Trim(),
                request.TenantDisplayName.Trim(),
                subjectId,
                isActive: !requiresApproval,
                ct: ct);
            createdTenantId = tenant.Id;

            if (!subject.IsActive)
            {
                subject.IsActive = true;
                await platformCtx.SaveChangesAsync(ct);
            }

            string? handoffUrl = null;

            if (!requiresApproval)
            {
                await using var tenantCtx = await _dbFactory.CreateForTenantAsync(tenant.Id, ct);
                var issued = await _loginCodeService.IssueAsync(
                    tenantCtx,
                    subjectId,
                    LoginCodePurpose.Login,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.ToString(),
                    new AuthAuditActor(subjectId, null),
                    ct);

                var (host, port) = _baseDomainOptions.SplitHostPort();
                var hostWithSubdomain = $"{tenant.Slug}.{host}" + (port.HasValue ? $":{port}" : "");
                var scheme = Request.Scheme;
                handoffUrl = $"{scheme}://{hostWithSubdomain}/auth/handoff?code={Uri.EscapeDataString(issued.Code)}";
            }
            else
            {
                var adminSubjectIds = await platformCtx.Subjects
                    .Where(s => s.IsPlatformAdmin && s.IsActive && !s.IsSystemSubject)
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                foreach (var adminId in adminSubjectIds)
                {
                    await _notificationService.CreateNotificationAsync(
                        adminId.ToString(),
                        "tenant.approval_requested",
                        $"Tenant approval requested: {tenant.DisplayName}",
                        subtitle: $"Slug: {tenant.Slug}, Owner: {subject.Name}",
                        sourceId: tenant.Id.ToString(),
                        cancellationToken: ct);
                }
            }

            _logger.LogInformation(
                "Onboarding completed: subject {SubjectId} ({Name}) provisioned tenant {TenantId} ({Slug}), approval={Approval}",
                subjectId, subject.Name, tenant.Id, tenant.Slug, requiresApproval);

            return Ok(new SignupCompleteResponse
            {
                Success = true,
                PendingApproval = requiresApproval,
                HandoffUrl = handoffUrl,
                RecoveryCodes = [],
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Onboarding failed for subject {SubjectId}", subjectId);

            if (createdTenantId.HasValue)
            {
                try
                {
                    await _tenantService.DeleteAsync(createdTenantId.Value, CancellationToken.None);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback tenant {TenantId}", createdTenantId.Value);
                }
            }

            return Problem(detail: "Tenant provisioning failed", statusCode: 400, title: "Onboarding Failed");
        }
    }

    private async Task<IActionResult?> CheckSignupDisabledOrSetupIncompleteAsync(CancellationToken ct)
    {
        if (!_operatorConfig.Signup.Enabled || !_operatorConfig.AllowSelfServiceCreation)
        {
            return NotFound(new { error = "signup_disabled" });
        }

        if (!await _setupState.IsSetupCompleteAsync(ct))
        {
            return Conflict(new { error = "setup_not_complete" });
        }

        return null;
    }
}

#region Request/Response DTOs

public class OnboardingRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantDisplayName { get; set; } = string.Empty;
}

public class SignupOptionsRequest
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantDisplayName { get; set; } = string.Empty;
}

public class SignupOptionsResponse
{
    public string Options { get; set; } = string.Empty;
    public string ChallengeToken { get; set; } = string.Empty;
}

public class SignupCompleteRequest
{
    public string Username { get; set; } = string.Empty;
    public string AttestationResponseJson { get; set; } = string.Empty;
    public string ChallengeToken { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantDisplayName { get; set; } = string.Empty;
}

public class SignupCompleteResponse
{
    public bool Success { get; set; }
    public bool PendingApproval { get; set; }
    public string? HandoffUrl { get; set; }
    public List<string> RecoveryCodes { get; set; } = [];
}

#endregion
