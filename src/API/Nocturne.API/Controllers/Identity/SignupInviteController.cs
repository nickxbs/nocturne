using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.API.Authorization;
using Nocturne.API.Multitenancy;
using Nocturne.API.Services;
using Nocturne.API.Services.Auth;
using Nocturne.API.Services.Identity;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Identity;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data.Extensions;

namespace Nocturne.API.Controllers.Identity;

/// <summary>
/// Tenantless signup invitation endpoint.
/// Accepts an email, creates a new tenant with a random English adjective-noun slug without numbers,
/// generates an owner invite, and returns the invitation URL in the response.
/// </summary>
[ApiController]
[Tags("Signup")]
[AllowAnonymous]
[DenyDemoSubject]
public class SignupInviteController : ControllerBase
{
    private const int MaxSlugGenerationAttempts = 25;

    private readonly ITenantService _tenantService;
    private readonly ITenantMembershipRepository _members;
    private readonly ISubjectRepository _subjects;
    private readonly ISubjectService _subjectService;
    private readonly IMemberInviteService _memberInviteService;
    private readonly IOptions<BaseDomainOptions> _baseDomainOptions;
    private readonly ILogger<SignupInviteController> _logger;

    public SignupInviteController(
        ITenantService tenantService,
        ITenantMembershipRepository members,
        ISubjectRepository subjects,
        ISubjectService subjectService,
        IMemberInviteService memberInviteService,
        IOptions<BaseDomainOptions> baseDomainOptions,
        ILogger<SignupInviteController> logger)
    {
        _tenantService = tenantService;
        _members = members;
        _subjects = subjects;
        _subjectService = subjectService;
        _memberInviteService = memberInviteService;
        _baseDomainOptions = baseDomainOptions;
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
            || email.Length > ContributionValidation.MaxEmailLength
            || !ContributionValidation.EmailPattern().IsMatch(email))
        {
            return Problem(
                detail: "A valid email address is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        // Resolve or generate slug and display name
        string? slug = null;
        string? displayName = null;

        if (!string.IsNullOrWhiteSpace(request?.Slug))
        {
            var requestedSlug = request.Slug.Trim().ToLowerInvariant();
            var check = await _tenantService.ValidateSlugAsync(requestedSlug, ct);
            if (!check.IsValid)
            {
                return Problem(
                    detail: check.ErrorMessage ?? "The requested slug is not available.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Slug");
            }

            slug = requestedSlug;
            displayName = string.Join(" ", requestedSlug.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..] : "")));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = requestedSlug;
            }
        }
        else
        {
            // Generate a unique random English adjective-noun slug without numbers
            for (var attempt = 0; attempt < MaxSlugGenerationAttempts; attempt++)
            {
                var (candidateSlug, candidateDisplayName) = RandomSlugGenerator.Generate();
                var check = await _tenantService.ValidateSlugAsync(candidateSlug, ct);
                if (check.IsValid)
                {
                    slug = candidateSlug;
                    displayName = candidateDisplayName;
                    break;
                }
            }

            if (slug == null || displayName == null)
            {
                _logger.LogError("Failed to generate a unique slug after {Attempts} attempts", MaxSlugGenerationAttempts);
                return Problem(
                    detail: "Unable to generate an available tenant name at this time. Please try again.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Server Error");
            }
        }

        // 1. Create tenant without owner (seeds roles including Owner and public membership)
        var tenantCreated = await _tenantService.CreateWithoutOwnerAsync(slug, displayName, ct);

        // 2. Retrieve Owner role id
        var ownerRoleId = await _members.FindRoleIdBySlugAsync(tenantCreated.Id, RoleSeeds.Owner, ct)
            ?? throw new InvalidOperationException($"Tenant {tenantCreated.Id} has no owner role after seeding.");

        // 3. Resolve public/system subject as the creator of the invite
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

        // 4. Derive tenant base URL for the invite
        var tenantOrigin = _baseDomainOptions.Value.TenantOrigin(slug);
        if (string.IsNullOrEmpty(tenantOrigin))
        {
            var host = Request?.Host.Value;
            var scheme = Request?.Scheme ?? "https";
            tenantOrigin = !string.IsNullOrEmpty(host) ? $"{scheme}://{slug}.{host}" : null;
        }

        // 5. Create the member invite
        var inviteResult = await _memberInviteService.CreateInviteAsync(
            tenantId: tenantCreated.Id,
            createdBySubjectId: creatorSubjectId,
            granterPermissions: [Scope.FullAccess],
            roleIds: [ownerRoleId],
            directPermissions: null,
            label: $"Owner invite for {email}",
            expiresInDays: 7,
            maxUses: 1,
            limitTo24Hours: false,
            baseUrl: tenantOrigin);

        _logger.LogInformation(
            "SignupInvite created for {Email}: Tenant {Slug} ({TenantId}), InviteUrl: {InviteUrl}",
            email, slug, tenantCreated.Id, inviteResult.InviteUrl);

        return Ok(new SignupInviteResponse
        {
            Url = inviteResult.InviteUrl,
            InviteUrl = inviteResult.InviteUrl,
            TenantSlug = slug,
            TenantDisplayName = displayName,
            TenantId = tenantCreated.Id,
            ExpiresAt = inviteResult.ExpiresAt
        });
    }
}

public class SignupInviteRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Slug { get; set; }
}

public class SignupInviteResponse
{
    public string Url { get; set; } = string.Empty;
    public string InviteUrl { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantDisplayName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

