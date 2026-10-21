using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nocturne.API.Controllers.V4.PlatformAdmin;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.Notifications;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Services;
using Nocturne.Tests.Shared;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Controllers.Admin;

public class TenantControllerActivateTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly NocturneDbContext _dbContext;
    private readonly INocturneDbContextFactory _dbFactory;
    private readonly Mock<ITenantService> _tenantService = new();
    private readonly Mock<ITenantRoleService> _roleService = new();
    private readonly Mock<IInAppNotificationService> _notificationService = new();
    private readonly TenantController _controller;

    public TenantControllerActivateTests()
    {
        _db = TestDbContextFactory.CreateSqlite();
        _dbContext = _db.CreateContext();
        _dbFactory = _db.ContextFactory.AsNocturneFactory();

        _controller = new TenantController(_tenantService.Object, _roleService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Activate_WhenTenantExists_UpdatesIsActiveAndArchivesNotifications()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = adminId,
            Name = "Platform Admin",
            IsPlatformAdmin = true,
            IsActive = true,
            IsSystemSubject = false,
        });
        await _dbContext.SaveChangesAsync();

        var existingTenant = new TenantDetailDto(
            tenantId,
            "test-slug",
            "Test Tenant",
            false,
            DateTime.UtcNow,
            []);

        var updatedTenant = new TenantDto(
            tenantId,
            "test-slug",
            "Test Tenant",
            true,
            DateTime.UtcNow);

        _tenantService
            .Setup(s => s.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTenant);

        _tenantService
            .Setup(s => s.UpdateAsync(tenantId, "Test Tenant", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedTenant);

        var result = await _controller.Activate(
            tenantId, _notificationService.Object, _dbFactory, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(updatedTenant);

        _tenantService.Verify(
            s => s.UpdateAsync(tenantId, "Test Tenant", true, null, It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationService.Verify(
            n => n.ArchiveBySourceAsync(
                adminId.ToString(),
                "tenant.approval_requested",
                tenantId.ToString(),
                NotificationArchiveReason.Completed,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Activate_WhenTenantNotFound_Returns404()
    {
        var tenantId = Guid.NewGuid();

        _tenantService
            .Setup(s => s.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantDetailDto?)null);

        var result = await _controller.Activate(
            tenantId, _notificationService.Object, _dbFactory, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Activate_WhenGetByIdThrowsKeyNotFound_Returns404()
    {
        var tenantId = Guid.NewGuid();

        _tenantService
            .Setup(s => s.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException($"Tenant {tenantId} not found"));

        var result = await _controller.Activate(
            tenantId, _notificationService.Object, _dbFactory, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Activate_DoesNotRequireCallerToBeTenantOwner()
    {
        var tenantId = Guid.NewGuid();

        var existingTenant = new TenantDetailDto(
            tenantId,
            "foreign-tenant",
            "Foreign Tenant",
            false,
            DateTime.UtcNow,
            []);

        var updatedTenant = new TenantDto(
            tenantId,
            "foreign-tenant",
            "Foreign Tenant",
            true,
            DateTime.UtcNow);

        _tenantService
            .Setup(s => s.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTenant);

        _tenantService
            .Setup(s => s.UpdateAsync(tenantId, "Foreign Tenant", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedTenant);

        // Caller has no tenant membership or claims
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();

        var result = await _controller.Activate(
            tenantId, _notificationService.Object, _dbFactory, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
