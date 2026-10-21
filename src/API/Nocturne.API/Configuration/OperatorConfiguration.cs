namespace Nocturne.API.Configuration;

public class OperatorConfiguration
{
    public const string SectionName = "Operator";

    /// <summary>
    /// Operator display name (e.g. "Nocturne.run"). When null, operator features are disabled.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether authenticated users can create their own tenants.
    /// SaaS operators set this to false to gate tenant creation behind billing.
    /// </summary>
    public bool AllowSelfServiceCreation { get; set; } = true;

    /// <summary>
    /// Optional webhook URL for custom slug validation.
    /// When configured, Nocturne POSTs { "slug": "xxx" } and expects { "isValid": bool, "message"?: string }.
    /// </summary>
    public string? SlugValidationWebhookUrl { get; set; }

    /// <summary>
    /// Optional webhook URL for custom username validation.
    /// When configured, Nocturne POSTs { "username": "xxx" } and expects { "isValid": bool, "message"?: string }.
    /// </summary>
    public string? UsernameValidationWebhookUrl { get; set; }

    /// <summary>
    /// Self-service passkey registration configuration.
    /// </summary>
    public OperatorSignupConfiguration Signup { get; set; } = new();

    public OperatorSupportConfiguration Support { get; set; } = new();
}

public class OperatorSignupConfiguration
{
    /// <summary>
    /// Whether public self-service passkey registration is enabled.
    /// Disabled by default; requires AllowSelfServiceCreation and setup completion.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Whether newly registered tenants start inactive and require platform admin approval.
    /// </summary>
    public bool RequiresApproval { get; set; } = false;
}

public class OperatorSupportConfiguration
{
    public OperatorSupportChannelConfiguration? AccountBilling { get; set; }

    public OperatorAccountPortalConfiguration? AccountPortal { get; set; }
}

/// <summary>
/// A page the operator hosts for managing the account. Only ever opened in a browser, so it has
/// no mode: an operator whose <see cref="OperatorSupportConfiguration.AccountBilling"/> is an API
/// endpoint still has somewhere to send a visitor.
/// </summary>
public class OperatorAccountPortalConfiguration
{
    public string Url { get; set; } = "";

    /// <summary>
    /// Optional button/link text. Falls back to "Contact {Operator.Name}".
    /// </summary>
    public string? Label { get; set; }
}

public class OperatorSupportChannelConfiguration
{
    /// <summary>
    /// "Redirect" opens the URL in a new tab. "Api" POSTs the issue payload to the URL.
    /// </summary>
    public OperatorSupportMode Mode { get; set; }

    /// <summary>
    /// The operator's support URL (redirect target or API endpoint).
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// Optional button/link text. Falls back to "Contact {Operator.Name}".
    /// </summary>
    public string? Label { get; set; }
}

public enum OperatorSupportMode
{
    Redirect,
    Api,
}
