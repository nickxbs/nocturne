using System.ComponentModel.DataAnnotations;

namespace Nocturne.Core.Models.CgmSim;

/// <summary>
/// Physiological subject parameters for CGM simulation. All fields are mandatory.
/// </summary>
public class CgmSimSubject
{
    [Required]
    [MaxLength(32)]
    public string Sex { get; set; } = "other";

    [Required]
    [Range(1.0, 500.0)]
    public double Weight { get; set; } = 70.0;

    [Required]
    [Range(1, 150)]
    public int Age { get; set; } = 30;

    [Required]
    [Range(20.0, 300.0)]
    public double Height { get; set; } = 170.0;
}

/// <summary>
/// Pharmacokinetic and metabolic simulation parameters. All fields are mandatory.
/// </summary>
public class CgmSimParameters
{
    [Required]
    [Range(1.0, 1000.0)]
    public double Isf { get; set; } = 50.0;

    [Required]
    [Range(0.5, 24.0)]
    public double Dia { get; set; } = 4.0;

    [Required]
    [Range(5, 1440)]
    public int CarbsAbsTime { get; set; } = 180;

    [Required]
    [Range(5, 360)]
    public int InsulinPeak { get; set; } = 75;

    [Required]
    [Range(0.1, 200.0)]
    public double Cr { get; set; } = 10.0;
}

/// <summary>
/// Complete CGM simulation configuration for a tenant.
/// </summary>
public class CgmSimSettings
{
    public CgmSimSubject Subject { get; set; } = new();
    public CgmSimParameters Parameters { get; set; } = new();
    public bool IsInitialized { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Atomic initialization request for setting up tenant CGM simulation.
/// </summary>
public class CgmSimInitializeRequest
{
    [Required]
    public CgmSimSubject Subject { get; set; } = new();

    [Required]
    public CgmSimParameters Parameters { get; set; } = new();
}

/// <summary>
/// Bolus wizard calculation breakdown computed from current glucose, IOB, COB, and tenant parameters.
/// </summary>
public class CgmSimWizardPreview
{
    public double Carbs { get; set; }
    public double BgUnits { get; set; }
    public double ChoUnits { get; set; }
    public double CobUnits { get; set; }
    public double IobUnits { get; set; }
    public double AdjustmentUnits { get; set; }
    public double RecommendedBolus { get; set; }
    public double? CurrentBg { get; set; }
    public double TargetBg { get; set; }
    public double Isf { get; set; }
    public double Cr { get; set; }
}
