using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Tenant-level configuration for the CGM simulator. Exactly one row per tenant.
/// </summary>
[Table("cgmsim_settings")]
public class CgmSimSettingsEntity : ITenantScoped, IEntityTimestamped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("sex")]
    public string Sex { get; set; } = "other";

    [Column("weight")]
    public double Weight { get; set; } = 70.0;

    [Column("age")]
    public int Age { get; set; } = 30;

    [Column("height")]
    public double Height { get; set; } = 170.0;

    [Column("isf")]
    public double Isf { get; set; } = 50.0;

    [Column("dia")]
    public double Dia { get; set; } = 4.0;

    [Column("carbs_abs_time")]
    public int CarbsAbsTime { get; set; } = 180;

    [Column("insulin_peak")]
    public int InsulinPeak { get; set; } = 75;

    [Column("cr")]
    public double Cr { get; set; } = 10.0;

    [Column("is_initialized")]
    public bool IsInitialized { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
