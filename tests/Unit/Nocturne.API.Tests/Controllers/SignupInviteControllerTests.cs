using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Controllers.Identity;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Communication;
using Nocturne.Core.Contracts.Identity;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Identity;
using Xunit;

namespace Nocturne.API.Tests.Controllers;

[Trait("Category", "Unit")]
public class SignupInviteControllerTests
{
    private readonly Mock<ITenantCreationInviteService> _tenantCreationInvites = new();
    private readonly Mock<ISubjectRepository> _subjects = new();
    private readonly Mock<ISubjectService> _subjectService = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    private SignupInviteController CreateController() =>
        new(
            _tenantCreationInvites.Object,
            _subjects.Object,
            _subjectService.Object,
            _emailSender.Object,
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
    public async Task CreateSignupInvite_WithValidEmail_CreatesTenantInviteAndReturnsStartUrl()
    {
        var controller = CreateController();
        var email = "user@example.com";
        var request = new SignupInviteRequest { Email = email };

        var publicSubjectId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var expectedInviteUrl = "https://nocturne.test/start?token=mocktoken123";

        _subjects
            .Setup(s => s.FindSystemSubjectAsync("Public", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectRecord(publicSubjectId, "Public", null, null, null, false, null, false, true, false, "Active", DateTime.UtcNow, null));

        var inviteDto = new TenantCreationInviteDto(
            Guid.NewGuid(), $"Signup for {email}", "Public", expiresAt, 1, 0, null, DateTime.UtcNow, TenantCreationInviteStatus.Active, []);

        _tenantCreationInvites
            .Setup(i => i.CreateAsync(
                publicSubjectId,
                It.Is<CreateTenantCreationInviteRequest>(r => r.Label == $"Signup for {email}" && r.MaxUses == 1 && r.ConfirmSubdomainChange),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantInviteOutcome<TenantCreationInviteCreated>.Ok(
                new TenantCreationInviteCreated(inviteDto, expectedInviteUrl)));

        _emailSender
            .Setup(e => e.SendInviteEmailAsync(
                email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                expectedInviteUrl,
                expiresAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await controller.CreateSignupInvite(request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = okResult.Value.Should().BeOfType<SignupInviteResponse>().Subject;

        response.Url.Should().Be(expectedInviteUrl);
        response.InviteUrl.Should().Be(expectedInviteUrl);
        response.ExpiresAt.Should().Be(expiresAt);
        response.EmailSent.Should().BeTrue();

        _emailSender.Verify(e => e.SendInviteEmailAsync(
            email,
            "Nocturne",
            "",
            expectedInviteUrl,
            expiresAt,
            It.IsAny<CancellationToken>()), Times.Once);

        _tenantCreationInvites.Verify(i => i.CreateAsync(
            publicSubjectId,
            It.Is<CreateTenantCreationInviteRequest>(r => r.Label == $"Signup for {email}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSignupInvite_WhenEmailSenderReturnsFalse_EmailSentIsFalse()
    {
        var controller = CreateController();
        var email = "user@example.com";
        var request = new SignupInviteRequest { Email = email };

        var publicSubjectId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var expectedInviteUrl = "https://nocturne.test/start?token=mocktoken123";

        _subjects
            .Setup(s => s.FindSystemSubjectAsync("Public", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectRecord(publicSubjectId, "Public", null, null, null, false, null, false, true, false, "Active", DateTime.UtcNow, null));

        var inviteDto = new TenantCreationInviteDto(
            Guid.NewGuid(), $"Signup for {email}", "Public", expiresAt, 1, 0, null, DateTime.UtcNow, TenantCreationInviteStatus.Active, []);

        _tenantCreationInvites
            .Setup(i => i.CreateAsync(
                publicSubjectId,
                It.IsAny<CreateTenantCreationInviteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantInviteOutcome<TenantCreationInviteCreated>.Ok(
                new TenantCreationInviteCreated(inviteDto, expectedInviteUrl)));

        _emailSender
            .Setup(e => e.SendInviteEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await controller.CreateSignupInvite(request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = okResult.Value.Should().BeOfType<SignupInviteResponse>().Subject;
        response.EmailSent.Should().BeFalse();
    }

    [Fact]
    public async Task CreateSignupInvite_WhenEmailSenderThrows_DoesNotFailAndSetsEmailSentFalse()
    {
        var controller = CreateController();
        var email = "user@example.com";
        var request = new SignupInviteRequest { Email = email };

        var publicSubjectId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var expectedInviteUrl = "https://nocturne.test/start?token=mocktoken123";

        _subjects
            .Setup(s => s.FindSystemSubjectAsync("Public", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectRecord(publicSubjectId, "Public", null, null, null, false, null, false, true, false, "Active", DateTime.UtcNow, null));

        var inviteDto = new TenantCreationInviteDto(
            Guid.NewGuid(), $"Signup for {email}", "Public", expiresAt, 1, 0, null, DateTime.UtcNow, TenantCreationInviteStatus.Active, []);

        _tenantCreationInvites
            .Setup(i => i.CreateAsync(
                publicSubjectId,
                It.IsAny<CreateTenantCreationInviteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantInviteOutcome<TenantCreationInviteCreated>.Ok(
                new TenantCreationInviteCreated(inviteDto, expectedInviteUrl)));

        _emailSender
            .Setup(e => e.SendInviteEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection error"));

        var result = await controller.CreateSignupInvite(request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = okResult.Value.Should().BeOfType<SignupInviteResponse>().Subject;
        response.EmailSent.Should().BeFalse();
    }

    [Fact]
    public async Task CreateSignupInvite_WhenInviteCreationRefused_ReturnsProblem()
    {
        var controller = CreateController();
        var email = "user@example.com";
        var request = new SignupInviteRequest { Email = email };

        var publicSubjectId = Guid.NewGuid();

        _subjects
            .Setup(s => s.FindSystemSubjectAsync("Public", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectRecord(publicSubjectId, "Public", null, null, null, false, null, false, true, false, "Active", DateTime.UtcNow, null));

        _tenantCreationInvites
            .Setup(i => i.CreateAsync(
                publicSubjectId,
                It.IsAny<CreateTenantCreationInviteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantInviteOutcome<TenantCreationInviteCreated>.Refused(
                TenantInviteRefusalCode.BaseDomainRequired, "Base domain is required."));

        var result = await controller.CreateSignupInvite(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}

