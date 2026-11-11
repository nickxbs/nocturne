using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nocturne.API.Authorization;
using Nocturne.API.Controllers.V4.Identity;
using Nocturne.API.Controllers.V4.PlatformAdmin;
using Nocturne.API.Multitenancy;
using Nocturne.API.Services.Identity;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Identity;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Repositories;
using Nocturne.Infrastructure.Data.Services;
using Nocturne.Tests.Shared.Infrastructure;

namespace Nocturne.API.Tests.Controllers.Admin;

[Trait("Category", "Unit")]
public class TenantBootstrapInviteControllerTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly NocturneDbContext _rootContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ITenantService> _tenantService = new();
    private readonly TenantBootstrapInviteController _controller;

    private readonly Guid _tenantId = Guid.CreateVersion7();
    private readonly Guid _ownerRoleId = Guid.CreateVersion7();

    public TenantBootstrapInviteControllerTests()
    {
        _db = TestDbContextFactory.CreateSqlite();
        _rootContext = _db.CreateContext();

        var services = new ServiceCollection();
        services.AddScoped<ITenantAccessor, HttpContextTenantAccessor>();
        services.AddScoped<TenantReadContext>(sp =>
        {
            var accessor = sp.GetRequiredService<ITenantAccessor>();
            return new TenantReadContext(accessor, null, null);
        });
        services.AddSingleton<INocturneDbContextFactory>(_ => _rootContext.AsFactory());
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IMemberInviteRepository, MemberInviteRepository>();
        services.AddScoped<ITenantRoleService, TenantRoleService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOptions<BaseDomainOptions>>(
            Options.Create(new BaseDomainOptions { BaseDomain = "nocturne.example" }));

        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString("N"));
        services.AddSingleton(jwt.Object);

        services.AddScoped<IMemberInviteService>(sp => new MemberInviteService(
            sp.GetRequiredService<ITenantMembershipRepository>(),
            sp.GetRequiredService<IMemberInviteRepository>(),
            sp.GetRequiredService<ISubjectRepository>(),
            sp.GetRequiredService<INocturneDbContextFactory>(),
            sp.GetRequiredService<IJwtService>(),
            sp.GetRequiredService<ITenantRoleService>(),
            sp.GetRequiredService<IOptions<BaseDomainOptions>>(),
            TimeProvider.System,
            NullLogger<MemberInviteService>.Instance));

        _serviceProvider = services.BuildServiceProvider();

        var bootstrapService = new TenantBootstrapInviteService(
            _serviceProvider.GetRequiredService<ITenantRepository>(),
            _serviceProvider.GetRequiredService<ITenantMembershipRepository>(),
            _serviceProvider.GetRequiredService<ISubjectRepository>(),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BaseDomainOptions { BaseDomain = "nocturne.example" }));

        _controller = new TenantBootstrapInviteController(_tenantService.Object, bootstrapService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
        _controller.HttpContext.Request.Scheme = "https";
        _controller.HttpContext.Request.Host = new HostString("nocturne.example");
    }

    private void SeedTenant(Guid tenantId, string slug, bool isActive = true, Guid? existingOwnerId = null)
    {
        using var ctx = _db.CreateContext(tenantId);
        ctx.Tenants.Add(new TenantEntity
        {
            Id = tenantId,
            Slug = slug,
            DisplayName = slug.ToUpperInvariant(),
            IsActive = isActive,
        });
        ctx.TenantRoles.Add(new TenantRoleEntity
        {
            Id = _ownerRoleId,
            TenantId = tenantId,
            Name = "Owner",
            Slug = RoleSeeds.Owner,
            Permissions = [Scope.FullAccess],
            IsSystem = true,
        });
        if (existingOwnerId is { } ownerId)
        {
            ctx.Subjects.Add(new SubjectEntity
            {
                Id = ownerId,
                Name = "Existing Owner",
                IsActive = true,
            });
            ctx.TenantMembers.Add(new TenantMemberEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                SubjectId = ownerId,
                Roles =
                [
                    new TenantMemberRoleEntity
                    {
                        Id = Guid.CreateVersion7(),
                        TenantId = tenantId,
                        TenantRoleId = _ownerRoleId,
                    },
                ],
            });
        }
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Bootstrap_CreatesTenantAndSingleUseOwnerInvite_OnTenantSubdomain()
    {
        SeedTenant(_tenantId, "alice");
        _tenantService
            .Setup(s => s.ValidateSlugAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(true));
        _tenantService
            .Setup(s => s.CreateWithoutOwnerAsync("alice", "Alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(_tenantId, "alice", "Alice", true, DateTime.UtcNow));

        var result = await _controller.Bootstrap(
            new BootstrapTenantWithInviteRequest("alice", "Alice", Label: "alice@example.com", ExpiresInDays: 7),
            CancellationToken.None);

        var created = result.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var payload = created.Value.Should().BeOfType<BootstrapTenantWithInviteResponse>().Subject;
        payload.Tenant.Slug.Should().Be("alice");
        payload.Invite.Token.Should().NotBeNullOrWhiteSpace();
        payload.Invite.InviteUrl.Should().StartWith("https://alice.nocturne.example/join?token=");
        payload.Invite.CreatedByName.Should().Be(TenantBootstrapInviteService.PlatformInviterSubjectName);

        var invites = await _serviceProvider.GetRequiredService<IMemberInviteService>()
            .GetInvitesForTenantAsync(_tenantId);
        invites.Should().ContainSingle();
        invites[0].MaxUses.Should().Be(1);
        invites[0].Label.Should().Be("alice@example.com");
        invites[0].RoleIds.Should().Equal(_ownerRoleId);
    }

    [Fact]
    public async Task Bootstrap_WhenInviteExpiryIsInvalid_DeletesCreatedTenantAndReturnsBadRequest()
    {
        SeedTenant(_tenantId, "bob");
        _tenantService
            .Setup(s => s.ValidateSlugAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(true));
        _tenantService
            .Setup(s => s.CreateWithoutOwnerAsync("bob", "Bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(_tenantId, "bob", "Bob", true, DateTime.UtcNow));

        var result = await _controller.Bootstrap(
            new BootstrapTenantWithInviteRequest("bob", "Bob", ExpiresInDays: 999),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<ObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _tenantService.Verify(s => s.DeleteAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOwnerInvite_ReturnsConflict_WhenTenantAlreadyHasOwner()
    {
        SeedTenant(_tenantId, "carol", existingOwnerId: Guid.CreateVersion7());

        var result = await _controller.CreateOwnerInvite(
            _tenantId,
            new CreateOwnerInviteRequest("carol@example.com"),
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CreateOwnerInvite_ReturnsNotFound_WhenTenantDoesNotExist()
    {
        var result = await _controller.CreateOwnerInvite(
            Guid.CreateVersion7(),
            new CreateOwnerInviteRequest(),
            CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void MemberInviteConsumeActions_AllowDuringSetup()
    {
        var getInfo = typeof(MemberInviteController).GetMethod(nameof(MemberInviteController.GetInviteInfo))!;
        var accept = typeof(MemberInviteController).GetMethod(nameof(MemberInviteController.AcceptInvite))!;

        getInfo.GetCustomAttributes(typeof(AllowDuringSetupAttribute), inherit: false).Should().ContainSingle();
        accept.GetCustomAttributes(typeof(AllowDuringSetupAttribute), inherit: false).Should().ContainSingle();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _rootContext.Dispose();
        _db.Dispose();
    }
}

