using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Controllers.Identity;
using Nocturne.API.Multitenancy;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Identity;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.Identity;
using Xunit;

namespace Nocturne.API.Tests.Controllers;

[Trait("Category", "Unit")]
public class SignupInviteControllerTests
{
    private readonly Mock<ITenantService> _tenantService = new();
    private readonly Mock<ITenantMembershipRepository> _members = new();
    private readonly Mock<ISubjectRepository> _subjects = new();
    private readonly Mock<ISubjectService> _subjectService = new();
    private readonly Mock<IMemberInviteService> _memberInviteService = new();
    private readonly IOptions<BaseDomainOptions> _baseDomainOptions =
        Microsoft.Extensions.Options.Options.Create(new BaseDomainOptions { BaseDomain = "test.nocturne.local" });

    private SignupInviteController CreateController() =>
        new(
            _tenantService.Object,
            _members.Object,
            _subjects.Object,
            _subjectService.Object,
            _memberInviteService.Object,
            _baseDomainOptions,
            NullLogger<SignupInviteController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@nodomain.com")]
    [InlineData("spaces in@email.com")]
    public async Task CreateSignupInvite_WithInvalidEmail_ReturnsBadRequest(string? invalidEmail)
    {
        var controller = CreateController();
        var request = new SignupInviteRequest { Email = invalidEmail! };

        var result = await controller.CreateSignupInvite(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateSignupInvite_WithValidEmail_CreatesTenantAndReturnsInviteUrl()
    {
        var controller = CreateController();
        var email = "user@example.com";
        var request = new SignupInviteRequest { Email = email };

        var tenantId = Guid.NewGuid();
        var ownerRoleId = Guid.NewGuid();
        var publicSubjectId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var expectedInviteUrl = "https://swift-fox.test.nocturne.local/join?token=mocktoken123";

        _tenantService
            .Setup(s => s.ValidateSlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(true));

        _tenantService
            .Setup(s => s.CreateWithoutOwnerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string slug, string displayName, CancellationToken _) =>
                new TenantCreatedDto(tenantId, slug, displayName, true, DateTime.UtcNow));

        _members
            .Setup(m => m.FindRoleIdBySlugAsync(tenantId, RoleSeeds.Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerRoleId);

        _subjects
            .Setup(s => s.FindSystemSubjectAsync("Public", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectRecord(publicSubjectId, "Public", null, null, null, false, null, false, true, false, "Active", DateTime.UtcNow, null));

        _memberInviteService
            .Setup(i => i.CreateInviteAsync(
                tenantId,
                publicSubjectId,
                It.Is<IEnumerable<string>>(p => p.Contains(Scope.FullAccess)),
                It.Is<List<Guid>>(r => r.Contains(ownerRoleId)),
                null,
                It.Is<string>(l => l.Contains(email)),
                7,
                1,
                false,
                It.IsAny<string>()))
            .ReturnsAsync(new MemberInviteResult(Guid.NewGuid(), "mocktoken123", expectedInviteUrl, expiresAt, "Public"));

        var result = await controller.CreateSignupInvite(request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = okResult.Value.Should().BeOfType<SignupInviteResponse>().Subject;

        response.Url.Should().Be(expectedInviteUrl);
        response.InviteUrl.Should().Be(expectedInviteUrl);
        response.TenantId.Should().Be(tenantId);
        response.TenantSlug.Should().NotBeNullOrWhiteSpace();
        response.TenantDisplayName.Should().NotBeNullOrWhiteSpace();
        response.ExpiresAt.Should().Be(expiresAt);

        _tenantService.Verify(s => s.CreateWithoutOwnerAsync(
            It.Is<string>(slug => !string.IsNullOrEmpty(slug) && slug.Contains('-')),
            It.Is<string>(dn => !string.IsNullOrEmpty(dn) && dn.Contains(' ')),
            It.IsAny<CancellationToken>()), Times.Once);

        _memberInviteService.Verify(i => i.CreateInviteAsync(
            tenantId,
            publicSubjectId,
            It.IsAny<IEnumerable<string>>(),
            It.Is<List<Guid>>(r => r.Contains(ownerRoleId)),
            null,
            It.Is<string>(l => l.Contains(email)),
            7,
            1,
            false,
            It.Is<string>(u => u != null && u.Contains("test.nocturne.local"))), Times.Once);
    }
}

