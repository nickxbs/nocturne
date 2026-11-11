using Microsoft.Extensions.Options;
using Nocturne.API.Multitenancy;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Services.Identity;

/// <summary>
/// Issues single-use owner invites for newly provisioned tenants that do not yet have an owner.
/// </summary>
public sealed class TenantBootstrapInviteService
{
    public const string PlatformInviterSubjectName = "Platform";

    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly ISubjectRepository _subjects;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BaseDomainOptions _baseDomain;

    public TenantBootstrapInviteService(
        ITenantRepository tenants,
        ITenantMembershipRepository members,
        ISubjectRepository subjects,
        IServiceScopeFactory scopeFactory,
        IOptions<BaseDomainOptions> baseDomain)
    {
        _tenants = tenants;
        _members = members;
        _subjects = subjects;
        _scopeFactory = scopeFactory;
        _baseDomain = baseDomain.Value;
    }

    public async Task<BootstrapInviteOutcome> CreateOwnerInviteAsync(
        Guid tenantId,
        string? label,
        int expiresInDays,
        string requestScheme,
        string? fallbackOrigin,
        CancellationToken ct = default)
    {
        var tenant = await _tenants.GetAsync(tenantId, ct);
        if (tenant is null)
            return BootstrapInviteOutcome.NotFound();

        if (!tenant.IsActive)
            return BootstrapInviteOutcome.Conflict("Tenant is not active.");

        var owners = await _members.GetOwnerSubjectIdsAsync(tenantId, ct);
        if (owners.Count > 0)
            return BootstrapInviteOutcome.Conflict("Tenant already has an owner.");

        var ownerRoleId = await _members.FindRoleIdBySlugAsync(tenantId, RoleSeeds.Owner, ct);
        if (ownerRoleId is null)
            throw new InvalidOperationException($"Tenant {tenantId} has no owner role.");

        var inviterSubjectId = await EnsurePlatformInviterSubjectAsync(ct);

        var tenantOrigin = !string.IsNullOrWhiteSpace(_baseDomain.BaseDomain)
            ? $"{requestScheme}://{tenant.Slug}.{_baseDomain.BaseDomain.Trim()}"
            : fallbackOrigin;

        // MemberInviteRepository reads TenantId from the DI scope's TenantReadContext, so the
        // invite must be created inside a child scope pinned to the target tenant.
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessor>().SetTenant(
            new TenantContext(tenant.Id, tenant.Slug, tenant.DisplayName, tenant.IsActive, tenant.IsDemo));

        var inviteService = scope.ServiceProvider.GetRequiredService<IMemberInviteService>();
        var invite = await inviteService.CreateInviteAsync(
            tenant.Id,
            inviterSubjectId,
            [Scope.FullAccess],
            [ownerRoleId.Value],
            directPermissions: null,
            label: label,
            expiresInDays: expiresInDays,
            maxUses: 1,
            limitTo24Hours: false,
            baseUrl: tenantOrigin);

        return BootstrapInviteOutcome.Created(invite);
    }

    private async Task<Guid> EnsurePlatformInviterSubjectAsync(CancellationToken ct)
    {
        var existing = await _subjects.FindSystemSubjectAsync(PlatformInviterSubjectName, ct);
        if (existing is not null)
            return existing.Id;

        return await _subjects.CreateAsync(
            new NewSubject(PlatformInviterSubjectName, IsSystemSubject: true),
            ct);
    }
}

public enum BootstrapInviteStatus
{
    Created,
    NotFound,
    Conflict,
}

public sealed record BootstrapInviteOutcome(
    BootstrapInviteStatus Status,
    MemberInviteResult? Invite = null,
    string? Error = null)
{
    public static BootstrapInviteOutcome Created(MemberInviteResult invite) =>
        new(BootstrapInviteStatus.Created, Invite: invite);

    public static BootstrapInviteOutcome NotFound() =>
        new(BootstrapInviteStatus.NotFound);

    public static BootstrapInviteOutcome Conflict(string error) =>
        new(BootstrapInviteStatus.Conflict, Error: error);
}

