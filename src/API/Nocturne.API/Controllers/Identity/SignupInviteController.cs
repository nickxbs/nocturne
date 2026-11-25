using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nocturne.API.Authorization;
using Nocturne.API.Services.Auth;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Communication;
using Nocturne.Core.Contracts.Identity;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data.Extensions;

namespace Nocturne.API.Controllers.Identity;

/// <summary>
/// Public self-service signup invitation endpoint.
/// Accepts an email, creates a native tenant creation invite, and emails the /start link to the recipient.
/// </summary>
[ApiController]
[Tags("Signup")]
[AllowAnonymous]
[AllowDuringSetup]
[DenyDemoSubject]
public partial class SignupInviteController : ControllerBase
{
    private const int MaxEmailLength = 254;

    [GeneratedRegex(@"^[^\s<>@]+@[^\s<>@]+\.[^\s<>@]+\z")]
    private static partial Regex EmailPattern();

    private readonly ITenantCreationInviteService _tenantCreationInvites;
    private readonly ISubjectRepository _subjects;
    private readonly ISubjectService _subjectService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SignupInviteController> _logger;

    public SignupInviteController(
        ITenantCreationInviteService tenantCreationInvites,
        ISubjectRepository subjects,
        ISubjectService subjectService,
        IEmailSender emailSender,
        ILogger<SignupInviteController> logger)
    {
        _tenantCreationInvites = tenantCreationInvites;
        _subjects = subjects;
        _subjectService = subjectService;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpPost("signup-invite")]
    [HttpPost("api/v4/signup-invite")]
    [ProducesResponseType(typeof(SignupInviteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SignupInviteResponse>> CreateSignupInvite(
        [FromBody] SignupInviteRequest request,
        CancellationToken ct)
    {
        var email = request?.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email)
            || email.Length > MaxEmailLength
            || !EmailPattern().IsMatch(email))
        {
            return Problem(
                detail: "A valid email address is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        // 1. Resolve public/system subject as the creator of the invite
        var publicSubject = await _subjects.FindSystemSubjectAsync(PublicSubjectFilter.PublicSubjectName, ct);
        Guid creatorSubjectId;
        if (publicSubject != null)
        {
            creatorSubjectId = publicSubject.Id;
        }
        else
        {
            var initialized = await _subjectService.InitializePublicSubjectAsync();
            creatorSubjectId = initialized?.Id ?? Guid.Empty;
        }

        // 2. Create the native tenant creation invite
        var inviteResult = await _tenantCreationInvites.CreateAsync(
            creatorSubjectId,
            new CreateTenantCreationInviteRequest(
                Label: $"Signup for {email}",
                MaxUses: 1,
                ExpiresInDays: 7,
                ConfirmSubdomainChange: true),
            ct);

        if (inviteResult.Refusal != null || inviteResult.Value == null)
        {
            _logger.LogError("Failed to create tenant creation invite: {Refusal}", inviteResult.Refusal?.Code);
            return Problem(
                detail: inviteResult.Refusal?.Message ?? "Unable to generate invitation at this time.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Server Error");
        }

        var inviteUrl = inviteResult.Value.Url;

        _logger.LogInformation(
            "SignupInvite created for {Email}: InviteUrl: {InviteUrl}",
            email, inviteUrl);

        var emailSent = false;
        try
        {
            emailSent = await _emailSender.SendInviteEmailAsync(
                email, "Nocturne", "", inviteUrl, inviteResult.Value.Invite.ExpiresAt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send signup invite email to {Email}", email);
        }

        return Ok(new SignupInviteResponse
        {
            Url = inviteUrl,
            InviteUrl = inviteUrl,
            ExpiresAt = inviteResult.Value.Invite.ExpiresAt,
            EmailSent = emailSent,
        });
    }
}

public class SignupInviteRequest
{
    public string Email { get; set; } = string.Empty;
}

public class SignupInviteResponse
{
    public string Url { get; set; } = string.Empty;
    public string InviteUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool EmailSent { get; set; }
}

