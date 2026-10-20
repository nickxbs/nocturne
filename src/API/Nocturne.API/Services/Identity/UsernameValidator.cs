using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nocturne.API.Configuration;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Extensions;
using Nocturne.Infrastructure.Data.Services;

namespace Nocturne.API.Services.Identity;

/// <summary>
/// Implements username syntax, reserved word, webhook, and database availability validation.
/// </summary>
public partial class UsernameValidator : IUsernameValidator
{
    private readonly INocturneDbContextFactory _dbFactory;
    private readonly OperatorConfiguration _operatorConfig;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private int _usernameWebhookOutageReported;

    private static readonly HashSet<string> ReservedUsernames = ["admin", "system"];

    [GeneratedRegex(@"^[a-z0-9][a-z0-9._\-]{1,30}[a-z0-9]$")]
    private static partial Regex UsernamePattern();

    public UsernameValidator(
        INocturneDbContextFactory dbFactory,
        IOptions<OperatorConfiguration> operatorConfig,
        IHttpClientFactory httpClientFactory,
        ILogger<UsernameValidator> logger)
        : this(dbFactory, operatorConfig.Value, httpClientFactory, (ILogger)logger)
    {
    }

    public UsernameValidator(
        INocturneDbContextFactory dbFactory,
        OperatorConfiguration operatorConfig,
        IHttpClientFactory httpClientFactory,
        ILogger? logger = null)
    {
        _dbFactory = dbFactory;
        _operatorConfig = operatorConfig;
        _httpClientFactory = httpClientFactory;
        _logger = logger ?? NullLogger<UsernameValidator>.Instance;
    }

    /// <inheritdoc />
    public SlugValidationResult ValidateFormat(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new SlugValidationResult(false, "Username is required");

        var normalized = username.Trim().ToLowerInvariant();

        if (!UsernamePattern().IsMatch(normalized))
            return new SlugValidationResult(false,
                "Username must be 3-32 characters: letters, numbers, dots, underscores, and hyphens");

        if (ReservedUsernames.Contains(normalized))
            return new SlugValidationResult(false, "This username is reserved");

        return new SlugValidationResult(true);
    }

    /// <inheritdoc />
    public async Task<SlugValidationResult> ValidateForSetupAsync(string? username, CancellationToken ct = default)
    {
        var format = ValidateFormat(username);
        if (!format.IsValid)
            return format;

        var normalized = username!.Trim().ToLowerInvariant();

        await using var context = await _dbFactory.CreatePlatformAsync(ct);

        var tenant = await context.Tenants.AsNoTracking().ExcludeDemo().FirstOrDefaultAsync(ct);
        if (tenant == null)
            return new SlugValidationResult(false, "No tenant exists");

        await context.PinTenantAsync(tenant.Id, ct);

        var exists = await context.TenantMembers.AsNoTracking()
            .AnyAsync(m => m.TenantId == tenant.Id && m.Username == normalized, ct);

        if (exists)
            return new SlugValidationResult(false, "This username is already taken");

        if (await AskUsernameWebhookAsync(normalized, ct) is { IsValid: false } refusal)
            return refusal;

        return new SlugValidationResult(true);
    }

    /// <inheritdoc />
    public async Task<SlugValidationResult> ValidateForSignupAsync(string? username, CancellationToken ct = default)
    {
        var format = ValidateFormat(username);
        if (!format.IsValid)
            return format;

        var normalized = username!.Trim().ToLowerInvariant();

        if (await AskUsernameWebhookAsync(normalized, ct) is { IsValid: false } refusal)
            return refusal;

        return new SlugValidationResult(true);
    }

    /// <inheritdoc />
    public async Task<SlugValidationResult> ValidateForTenantAsync(string? username, Guid tenantId, CancellationToken ct = default)
    {
        var format = ValidateFormat(username);
        if (!format.IsValid)
            return format;

        var normalized = username!.Trim().ToLowerInvariant();

        await using var context = await _dbFactory.CreatePlatformAsync(ct);
        await context.PinTenantAsync(tenantId, ct);

        var exists = await context.TenantMembers.AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantId && m.Username == normalized, ct);

        if (exists)
            return new SlugValidationResult(false, "This username is already taken");

        if (await AskUsernameWebhookAsync(normalized, ct) is { IsValid: false } refusal)
            return refusal;

        return new SlugValidationResult(true);
    }

    private async Task<SlugValidationResult?> AskUsernameWebhookAsync(string username, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_operatorConfig.UsernameValidationWebhookUrl))
            return null;

        Exception? failure = null;
        int? statusCode = null;

        try
        {
            var client = _httpClientFactory.CreateClient("username-validation");
            var response = await client.PostAsJsonAsync(
                _operatorConfig.UsernameValidationWebhookUrl,
                new { username },
                ct);

            if (response.IsSuccessStatusCode)
            {
                Interlocked.Exchange(ref _usernameWebhookOutageReported, 0);
                return await response.Content.ReadFromJsonAsync<SlugValidationResult>(ct);
            }

            statusCode = (int)response.StatusCode;
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested)
                return null;

            failure = ex;
        }

        if (Interlocked.Exchange(ref _usernameWebhookOutageReported, 1) == 0)
        {
            _logger.LogWarning(
                failure,
                "Username validation webhook did not answer ({Outcome}); usernames are being accepted without it",
                statusCode?.ToString() ?? "unreachable");
        }

        return null;
    }
}
