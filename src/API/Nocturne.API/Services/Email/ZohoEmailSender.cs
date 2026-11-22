using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Nocturne.Core.Contracts.Communication;

namespace Nocturne.API.Services.Email;

/// <summary>
/// Email sender implementation that uses Zoho Mail SMTP (with implicit SSL on port 465 or STARTTLS on 587).
/// </summary>
public class ZohoEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly PlatformSettingsService _platformSettings;
    private readonly ILogger<ZohoEmailSender> _logger;

    public ZohoEmailSender(
        IConfiguration configuration,
        PlatformSettingsService platformSettings,
        ILogger<ZohoEmailSender> logger)
    {
        _configuration = configuration;
        _platformSettings = platformSettings;
        _logger = logger;
    }

    internal Func<MimeMessage, ZohoSmtpSettings, CancellationToken, Task>? SendMailHandler { get; set; }

    public async Task<bool> SendInviteEmailAsync(
        string toEmail,
        string tenantDisplayName,
        string tenantSlug,
        string inviteUrl,
        DateTime expiresAt,
        CancellationToken ct = default)
    {
        var settings = await ResolveSettingsAsync(ct);
        if (settings == null)
        {
            _logger.LogInformation(
                "Zoho SMTP email provider is not configured. Skipping invitation email to {Email}",
                toEmail);
            return false;
        }

        var fromAddress = string.IsNullOrWhiteSpace(settings.FromAddress)
            ? settings.Username
            : settings.FromAddress;

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName, fromAddress));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = $"Il tuo invito a Nocturne{(!string.IsNullOrWhiteSpace(tenantDisplayName) && tenantDisplayName != "Nocturne" ? $" - {tenantDisplayName}" : "")}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = BuildHtmlBody(tenantDisplayName, tenantSlug, inviteUrl, expiresAt),
                TextBody = BuildTextBody(tenantDisplayName, tenantSlug, inviteUrl, expiresAt),
            };
            message.Body = bodyBuilder.ToMessageBody();

            if (SendMailHandler != null)
            {
                await SendMailHandler(message, settings, ct);
            }
            else
            {
                using var client = new SmtpClient();
                // Port 465 uses implicit SSL (SslOnConnect, matching nodemailer secure: true);
                // other ports (587, 25) negotiate STARTTLS.
                var secureSocketOptions = settings.Port == 465
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                client.Timeout = 15000;
                await client.ConnectAsync(settings.Host, settings.Port, secureSocketOptions, ct);
                await client.AuthenticateAsync(settings.Username, settings.Password, ct);
                await client.SendAsync(message, ct);
                await client.DisconnectAsync(true, ct);
            }

            _logger.LogInformation(
                "Invitation email successfully sent via Zoho SMTP ({Host}:{Port}) to {Email}",
                settings.Host, settings.Port, toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send invitation email via Zoho SMTP ({Host}:{Port}, User: {User}, From: {From}) to {Email}: {ErrorMessage}",
                settings.Host, settings.Port, settings.Username, fromAddress, toEmail, ex.Message);
            return false;
        }
    }

    private async Task<ZohoSmtpSettings?> ResolveSettingsAsync(CancellationToken ct)
    {
        // 1. Check PlatformSettings (stored in DB, category "zoho" or "smtp")
        try
        {
            var credentials = await _platformSettings.GetCredentialsAsync("zoho", ct)
                ?? await _platformSettings.GetCredentialsAsync("smtp", ct);

            if (credentials != null && credentials.Enabled)
            {
                var user = credentials.Fields.GetValueOrDefault("user")
                    ?? credentials.Fields.GetValueOrDefault("username");
                var pass = credentials.Fields.GetValueOrDefault("password");

                if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
                {
                    var host = credentials.Fields.GetValueOrDefault("host");
                    if (string.IsNullOrWhiteSpace(host)) host = "smtp.zoho.com";

                    var portStr = credentials.Fields.GetValueOrDefault("port");
                    var port = int.TryParse(portStr, out var p) ? p : 465;
                    var fromAddress = credentials.Fields.GetValueOrDefault("fromAddress") ?? user;
                    var fromName = credentials.Fields.GetValueOrDefault("fromName") ?? "Nocturne";

                    return new ZohoSmtpSettings(host, port, user, pass, fromAddress, fromName, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not load Zoho credentials from PlatformSettings; checking configuration");
        }

        // 2. Check IConfiguration / Environment
        var envUser = _configuration["ZOHO_SMTP_USER"]
            ?? _configuration["NODEMAILER_ZOHO_USER"]
            ?? _configuration["SMTP_USER"]
            ?? _configuration["SMTP_USERNAME"];

        var envPass = _configuration["ZOHO_SMTP_PASSWORD"]
            ?? _configuration["NODEMAILER_ZOHO_PWD"]
            ?? _configuration["SMTP_PASSWORD"];

        if (!string.IsNullOrWhiteSpace(envUser) && !string.IsNullOrWhiteSpace(envPass))
        {
            var envHost = _configuration["ZOHO_SMTP_HOST"]
                ?? _configuration["SMTP_HOST"]
                ?? "smtp.zoho.com";

            var envPortStr = _configuration["ZOHO_SMTP_PORT"]
                ?? _configuration["SMTP_PORT"];
            var envPort = int.TryParse(envPortStr, out var p) ? p : 465;

            var envFromAddress = _configuration["ZOHO_FROM_ADDRESS"]
                ?? _configuration["SMTP_FROM_ADDRESS"]
                ?? envUser;

            var envFromName = _configuration["ZOHO_FROM_NAME"]
                ?? _configuration["SMTP_FROM_NAME"]
                ?? "Nocturne";

            return new ZohoSmtpSettings(envHost, envPort, envUser, envPass, envFromAddress, envFromName, true);
        }

        return null;
    }

    private static string BuildHtmlBody(string tenantDisplayName, string tenantSlug, string inviteUrl, DateTime expiresAt)
    {
        var expiresFormatted = expiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var spaceInfo = !string.IsNullOrWhiteSpace(tenantSlug)
            ? $"per lo spazio <strong>{tenantDisplayName}</strong> (<code>{tenantSlug}</code>)"
            : "per il tuo nuovo spazio Nocturne";

        return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Attiva il tuo account Nocturne</title>
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f4f4f5; margin: 0; padding: 24px; color: #18181b;"">
  <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 580px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; border: 1px solid #e4e4e7; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05);"">
    <tr>
      <td style=""padding: 32px 32px 24px 32px; text-align: center; background-color: #09090b; color: #ffffff;"">
        <h1 style=""margin: 0; font-size: 24px; font-weight: 700; letter-spacing: -0.5px;"">Nocturne</h1>
      </td>
    </tr>
    <tr>
      <td style=""padding: 32px;"">
        <h2 style=""margin: 0 0 16px 0; font-size: 20px; font-weight: 600; color: #09090b;"">Attiva il tuo spazio!</h2>
        <p style=""margin: 0 0 16px 0; font-size: 15px; line-height: 1.5; color: #3f3f46;"">
          Hai richiesto la creazione di un account {spaceInfo}.
        </p>
        <p style=""margin: 0 0 24px 0; font-size: 15px; line-height: 1.5; color: #3f3f46;"">
          Clicca sul pulsante qui sotto per completare la configurazione, impostare la tua passkey e attivare il tuo spazio:
        </p>
        <div style=""text-align: center; margin: 32px 0;"">
          <a href=""{inviteUrl}"" style=""display: inline-block; background-color: #0284c7; color: #ffffff; text-decoration: none; padding: 14px 28px; border-radius: 8px; font-weight: 600; font-size: 15px;"">
            Attiva il tuo spazio &rarr;
          </a>
        </div>
        <div style=""background-color: #f4f4f5; border-radius: 8px; padding: 16px; margin: 24px 0; font-size: 13px; line-height: 1.4; color: #71717a;"">
          <p style=""margin: 0 0 8px 0; font-weight: 600; color: #3f3f46;"">Se il pulsante non funziona, copia questo link nel browser:</p>
          <p style=""margin: 0; word-break: break-all; font-family: monospace; color: #0284c7;"">{inviteUrl}</p>
        </div>
        <p style=""margin: 24px 0 0 0; font-size: 13px; line-height: 1.5; color: #71717a;"">
          Questo invito scade il <strong>{expiresFormatted} UTC</strong>. Se non hai richiesto tu questa registrazione, puoi ignorare questo messaggio.
        </p>
      </td>
    </tr>
    <tr>
      <td style=""padding: 20px 32px; background-color: #fafafa; border-top: 1px solid #e4e4e7; text-align: center; font-size: 12px; color: #a1a1aa;"">
        Nocturne &bull; Gestione del diabete moderna e open-source
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    private static string BuildTextBody(string tenantDisplayName, string tenantSlug, string inviteUrl, DateTime expiresAt)
    {
        var expiresFormatted = expiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var spaceInfo = !string.IsNullOrWhiteSpace(tenantSlug)
            ? $"per lo spazio {tenantDisplayName} ({tenantSlug})"
            : "per il tuo nuovo spazio Nocturne";

        return $@"Nocturne - Attiva il tuo spazio!

Hai richiesto la creazione di un account {spaceInfo}.

Per completare la registrazione e impostare le tue credenziali proprietario, visita il seguente link:
{inviteUrl}

Questo link scade il {expiresFormatted} UTC.

Se non hai richiesto tu questo invito, puoi ignorare questo messaggio.
";
    }
}

public record ZohoSmtpSettings(
    string Host,
    int Port,
    string Username,
    string Password,
    string FromAddress,
    string FromName,
    bool EnableSsl);

