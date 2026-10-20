using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Services.Identity;

/// <summary>
/// Validates username syntax, reserved names, operator webhooks, and tenant availability.
/// Shared between first-run setup and self-service registration.
/// </summary>
public interface IUsernameValidator
{
    /// <summary>
    /// Validates username syntax and reserved words without querying database or webhooks.
    /// </summary>
    SlugValidationResult ValidateFormat(string? username);

    /// <summary>
    /// Validates username format, reserved words, webhook, and availability in the setup wizard
    /// against the sole non-demo tenant.
    /// </summary>
    Task<SlugValidationResult> ValidateForSetupAsync(string? username, CancellationToken ct = default);

    /// <summary>
    /// Validates username format, reserved words, and operator webhook for self-service registration.
    /// </summary>
    Task<SlugValidationResult> ValidateForSignupAsync(string? username, CancellationToken ct = default);

    /// <summary>
    /// Validates username format, reserved words, webhook, and availability within an existing tenant.
    /// </summary>
    Task<SlugValidationResult> ValidateForTenantAsync(string? username, Guid tenantId, CancellationToken ct = default);
}
