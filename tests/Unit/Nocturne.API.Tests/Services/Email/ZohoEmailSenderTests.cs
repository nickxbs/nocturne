using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Moq;
using Nocturne.API.Services;
using Nocturne.API.Services.Email;
using Xunit;

namespace Nocturne.API.Tests.Services.Email;

[Trait("Category", "Unit")]
public class ZohoEmailSenderTests
{
    [Fact]
    public async Task SendInviteEmailAsync_WhenNotConfigured_ReturnsFalse()
    {
        var config = new ConfigurationBuilder().Build();
        var mockSettings = new Mock<PlatformSettingsService>(null!, null!, null!);
        mockSettings.Setup(s => s.GetCredentialsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlatformCredentials?)null);

        var sender = new ZohoEmailSender(config, mockSettings.Object, NullLogger<ZohoEmailSender>.Instance);

        var result = await sender.SendInviteEmailAsync(
            "user@example.com",
            "My Space",
            "my-space",
            "https://my-space.example.com/start?token=123",
            DateTime.UtcNow.AddDays(7));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendInviteEmailAsync_WhenConfiguredViaEnv_SendsMailAndReturnsTrue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ZOHO_SMTP_HOST"] = "smtp.zoho.eu",
                ["ZOHO_SMTP_PORT"] = "587",
                ["ZOHO_SMTP_USER"] = "noreply@mysite.com",
                ["ZOHO_SMTP_PASSWORD"] = "secret_zoho_password",
                ["ZOHO_FROM_NAME"] = "Nocturne Alert"
            })
            .Build();

        var mockSettings = new Mock<PlatformSettingsService>(null!, null!, null!);
        mockSettings.Setup(s => s.GetCredentialsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlatformCredentials?)null);

        MimeMessage? capturedMessage = null;
        ZohoSmtpSettings? capturedSettings = null;

        var sender = new ZohoEmailSender(config, mockSettings.Object, NullLogger<ZohoEmailSender>.Instance)
        {
            SendMailHandler = (msg, settings, _) =>
            {
                capturedMessage = msg;
                capturedSettings = settings;
                return Task.CompletedTask;
            }
        };

        var inviteUrl = "https://example.com/start?token=abc";
        var result = await sender.SendInviteEmailAsync(
            "user@example.com",
            "My Space",
            "my-space",
            inviteUrl,
            DateTime.UtcNow.AddDays(7));

        result.Should().BeTrue();

        capturedSettings.Should().NotBeNull();
        capturedSettings!.Host.Should().Be("smtp.zoho.eu");
        capturedSettings.Port.Should().Be(587);
        capturedSettings.Username.Should().Be("noreply@mysite.com");
        capturedSettings.Password.Should().Be("secret_zoho_password");
        capturedSettings.EnableSsl.Should().BeTrue();

        capturedMessage.Should().NotBeNull();
        var from = capturedMessage!.From.Mailboxes.First();
        from.Address.Should().Be("noreply@mysite.com");
        from.Name.Should().Be("Nocturne Alert");
        capturedMessage.To.Mailboxes.First().Address.Should().Be("user@example.com");
        capturedMessage.Subject.Should().Contain("My Space");
        capturedMessage.HtmlBody.Should().Contain(inviteUrl);
        capturedMessage.TextBody.Should().Contain(inviteUrl);
    }

    [Fact]
    public async Task SendInviteEmailAsync_WhenConfiguredViaNodemailerEnv_SendsMailAndReturnsTrue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NODEMAILER_ZOHO_USER"] = "user@zoho.com",
                ["NODEMAILER_ZOHO_PWD"] = "app_specific_password"
            })
            .Build();

        var mockSettings = new Mock<PlatformSettingsService>(null!, null!, null!);
        mockSettings.Setup(s => s.GetCredentialsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlatformCredentials?)null);

        MimeMessage? capturedMessage = null;
        ZohoSmtpSettings? capturedSettings = null;

        var sender = new ZohoEmailSender(config, mockSettings.Object, NullLogger<ZohoEmailSender>.Instance)
        {
            SendMailHandler = (msg, settings, _) =>
            {
                capturedMessage = msg;
                capturedSettings = settings;
                return Task.CompletedTask;
            }
        };

        var inviteUrl = "https://example.com/start?token=abc";
        var result = await sender.SendInviteEmailAsync(
            "test@example.com",
            "Nocturne",
            "",
            inviteUrl,
            DateTime.UtcNow.AddDays(7));

        result.Should().BeTrue();

        capturedSettings.Should().NotBeNull();
        capturedSettings!.Host.Should().Be("smtp.zoho.com");
        capturedSettings.Port.Should().Be(465);
        capturedSettings.Username.Should().Be("user@zoho.com");
        capturedSettings.Password.Should().Be("app_specific_password");

        capturedMessage.Should().NotBeNull();
        capturedMessage!.To.Mailboxes.First().Address.Should().Be("test@example.com");
        capturedMessage.HtmlBody.Should().Contain(inviteUrl);
    }

    [Fact]
    public async Task SendInviteEmailAsync_WhenSendThrows_ReturnsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ZOHO_SMTP_USER"] = "noreply@mysite.com",
                ["ZOHO_SMTP_PASSWORD"] = "secret_zoho_password"
            })
            .Build();

        var mockSettings = new Mock<PlatformSettingsService>(null!, null!, null!);
        mockSettings.Setup(s => s.GetCredentialsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlatformCredentials?)null);

        var sender = new ZohoEmailSender(config, mockSettings.Object, NullLogger<ZohoEmailSender>.Instance)
        {
            SendMailHandler = (_, _, _) => throw new Exception("Authentication failed")
        };

        var result = await sender.SendInviteEmailAsync(
            "user@example.com",
            "My Space",
            "my-space",
            "https://my-space.example.com/start?token=abc",
            DateTime.UtcNow.AddDays(7));

        result.Should().BeFalse();
    }
}

