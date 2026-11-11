using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Authorization;
using Nocturne.API.Extensions;
using Nocturne.API.Services.Identity;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Controllers.V4.PlatformAdmin;

/// <summary>
/// Platform-admin endpoints for bootstrapping new ownerless tenants with a single-use owner invite.
/// </summary>
[ApiController]
[Tags("PlatformAdmin")]
[Route("api/v4/admin/tenants")]
[Produces("application/json")]
[Authorize(Roles = "platform_admin")]
[AllowDuringSetup]
public class TenantBootstrapInviteController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly TenantBootstrapInviteService _bootstrapInvites;

    public TenantBootstrapInviteController(
        ITenantService tenantService,
        TenantBootstrapInviteService bootstrapInvites)
    {
        _tenantService = tenantService;
        _bootstrapInvites = bootstrapInvites;
    }

    /// <summary>
    /// Creates a single-use owner invite for an existing tenant that does not yet have an owner.
    /// </summary>
    [HttpPost("{id:guid}/owner-invite")]
    [ProducesResponseType(typeof(MemberInviteResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOwnerInvite(
        Guid id, [FromBody] CreateOwnerInviteRequest? request, CancellationToken ct)
    {
        var req = request ?? new CreateOwnerInviteRequest();
        var fallbackOrigin = $"{Request.PublicScheme()}://{Request.Host}";

        try
        {
            var outcome = await _bootstrapInvites.CreateOwnerInviteAsync(
                id, req.Label, req.ExpiresInDays, Request.PublicScheme(), fallbackOrigin, ct);

            return outcome.Status switch
            {
                BootstrapInviteStatus.Created => StatusCode(StatusCodes.Status201Created, outcome.Invite),
                BootstrapInviteStatus.NotFound => NotFound(),
                BootstrapInviteStatus.Conflict => Problem(
                    detail: outcome.Error, statusCode: StatusCodes.Status409Conflict, title: "Conflict"),
                _ => throw new InvalidOperationException($"Unexpected outcome: {outcome.Status}"),
            };
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
        }
    }

    /// <summary>
    /// Creates a new tenant and issues a single-use owner invite in one call.
    /// </summary>
    [HttpPost("bootstrap")]
    [ProducesResponseType(typeof(BootstrapTenantWithInviteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Bootstrap(
        [FromBody] BootstrapTenantWithInviteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.DisplayName))
            return Problem(
                detail: "Slug and display name are required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");

        var validation = await _tenantService.ValidateSlugAsync(request.Slug, ct);
        if (!validation.IsValid)
            return Problem(
                detail: validation.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");

        var tenant = await _tenantService.CreateWithoutOwnerAsync(
            request.Slug, request.DisplayName, ct);

        try
        {
            var fallbackOrigin = $"{Request.PublicScheme()}://{Request.Host}";
            var outcome = await _bootstrapInvites.CreateOwnerInviteAsync(
                tenant.Id, request.Label, request.ExpiresInDays, Request.PublicScheme(), fallbackOrigin, ct);

            if (outcome.Status != BootstrapInviteStatus.Created || outcome.Invite is null)
            {
                await _tenantService.DeleteAsync(tenant.Id, ct);
                return Problem(
                    detail: outcome.Error ?? "Failed to create owner invite.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request");
            }

            return StatusCode(
                StatusCodes.Status201Created,
                new BootstrapTenantWithInviteResponse(tenant, outcome.Invite));
        }
        catch (ArgumentException ex)
        {
            await _tenantService.DeleteAsync(tenant.Id, ct);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
        }
        catch
        {
            await _tenantService.DeleteAsync(tenant.Id, ct);
            throw;
        }
    }
}

public record CreateOwnerInviteRequest(string? Label = null, int ExpiresInDays = 7);

public record BootstrapTenantWithInviteRequest(
    string Slug,
    string DisplayName,
    string? Label = null,
    int ExpiresInDays = 7);

public record BootstrapTenantWithInviteResponse(
    TenantCreatedDto Tenant,
    MemberInviteResult Invite);
