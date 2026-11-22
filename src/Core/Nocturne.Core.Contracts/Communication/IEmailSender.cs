using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nocturne.Core.Contracts.Communication;

/// <summary>
/// Service contract for sending transactional emails (such as invitation emails).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an invitation email to the specified recipient.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="tenantDisplayName">Human-readable tenant/site display name.</param>
    /// <param name="tenantSlug">Unique tenant subdomain slug.</param>
    /// <param name="inviteUrl">Direct activation or invitation link.</param>
    /// <param name="expiresAt">Expiration date/time of the invitation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the email was successfully sent, false otherwise.</returns>
    Task<bool> SendInviteEmailAsync(
        string toEmail,
        string tenantDisplayName,
        string tenantSlug,
        string inviteUrl,
        DateTime expiresAt,
        CancellationToken ct = default);
}

