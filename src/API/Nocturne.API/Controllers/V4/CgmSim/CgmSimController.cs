using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenApi.Remote.Attributes;
using Nocturne.API.Attributes;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Health;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.CgmSim;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Services;

namespace Nocturne.API.Controllers.V4.CgmSim;

[ApiController]
[Authorize]
[Route("api/v4/cgmsim")]
[Tags("CgmSim")]
public class CgmSimController : ControllerBase, IWriteScopedController
{
    public string WriteScope => Scope.TherapyReadWrite;

    private readonly INocturneDbContextFactory _contextFactory;
    private readonly IEntryService _entryService;
    private readonly IIobCalculator _iobCalculator;
    private readonly ICobCalculator _cobCalculator;
    private readonly IBolusRepository _bolusRepository;
    private readonly ITempBasalRepository _tempBasalRepository;
    private readonly ICarbIntakeRepository _carbIntakeRepository;
    private readonly ITargetRangeResolver _targetRangeResolver;
    private readonly ISensitivityResolver _sensitivityResolver;
    private readonly ICarbRatioResolver _carbRatioResolver;
    private readonly ITherapySettingsResolver _therapySettingsResolver;
    private readonly IPatientRecordRepository _patientRecordRepository;
    private readonly IBodyWeightService _bodyWeightService;

    public CgmSimController(
        INocturneDbContextFactory contextFactory,
        IEntryService entryService,
        IIobCalculator iobCalculator,
        ICobCalculator cobCalculator,
        IBolusRepository bolusRepository,
        ITempBasalRepository tempBasalRepository,
        ICarbIntakeRepository carbIntakeRepository,
        ITargetRangeResolver targetRangeResolver,
        ISensitivityResolver sensitivityResolver,
        ICarbRatioResolver carbRatioResolver,
        ITherapySettingsResolver therapySettingsResolver,
        IPatientRecordRepository patientRecordRepository,
        IBodyWeightService bodyWeightService)
    {
        _contextFactory = contextFactory;
        _entryService = entryService;
        _iobCalculator = iobCalculator;
        _cobCalculator = cobCalculator;
        _bolusRepository = bolusRepository;
        _tempBasalRepository = tempBasalRepository;
        _carbIntakeRepository = carbIntakeRepository;
        _targetRangeResolver = targetRangeResolver;
        _sensitivityResolver = sensitivityResolver;
        _carbRatioResolver = carbRatioResolver;
        _therapySettingsResolver = therapySettingsResolver;
        _patientRecordRepository = patientRecordRepository;
        _bodyWeightService = bodyWeightService;
    }

    [HttpGet("subject")]
    [RequireScope(Scope.TherapyRead)]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimSubject), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimSubject>> GetSubject(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);
        var subject = await ResolveEffectiveSubjectAsync(entity, ct);

        return Ok(subject);
    }

    [HttpPut("subject")]
    [RequireDeclaredWriteScope]
    [RemoteCommand(Invalidates = ["GetSubject", "GetSettings"])]
    [ProducesResponseType(typeof(CgmSimSubject), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimSubject>> UpdateSubject(
        [FromBody] CgmSimSubject model,
        CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        // Update PatientRecord if age or sex specified
        var patient = await _patientRecordRepository.GetOrCreateAsync(ct);
        if (model.Age > 0)
        {
            var birthYear = DateTime.UtcNow.Year - model.Age;
            patient.DateOfBirth = new DateOnly(birthYear, 1, 1);
        }

        if (string.Equals(model.Sex, "female", StringComparison.OrdinalIgnoreCase))
        {
            patient.Sex = Nocturne.Core.Models.V4.BiologicalSex.Female;
        }
        else if (string.Equals(model.Sex, "male", StringComparison.OrdinalIgnoreCase))
        {
            patient.Sex = Nocturne.Core.Models.V4.BiologicalSex.Male;
        }

        await _patientRecordRepository.UpdateAsync(patient, Nocturne.Core.Contracts.V4.WriteOrigin.Live, ct);

        // Update BodyWeight if weight specified
        if (model.Weight > 0)
        {
            await _bodyWeightService.CreateBodyWeightsAsync(new[]
            {
                new Nocturne.Core.Models.BodyWeight
                {
                    WeightKg = (decimal)model.Weight,
                    Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    EnteredBy = "cgmsim",
                }
            }, ct);
        }

        // Entity fallback storage
        entity.Sex = model.Sex;
        entity.Weight = model.Weight;
        entity.Age = model.Age;
        entity.Height = model.Height;

        await db.SaveChangesAsync(ct);

        var subject = await ResolveEffectiveSubjectAsync(entity, ct);
        return Ok(subject);
    }

    [HttpGet("parameters")]
    [RequireScope(Scope.TherapyRead)]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimParameters), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimParameters>> GetParameters(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        var nowMills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var effectiveIsf = await _sensitivityResolver.GetSensitivityAsync(nowMills, ct: ct);
        var effectiveCr = await _carbRatioResolver.GetCarbRatioAsync(nowMills, ct: ct);
        var effectiveDia = await _therapySettingsResolver.GetDIAAsync(nowMills, ct: ct);

        return Ok(new CgmSimParameters
        {
            Isf = effectiveIsf > 0 ? effectiveIsf : (entity.Isf > 0 ? entity.Isf : 50.0),
            Dia = effectiveDia > 0 ? effectiveDia : (entity.Dia > 0 ? entity.Dia : 4.0),
            CarbsAbsTime = entity.CarbsAbsTime,
            InsulinPeak = entity.InsulinPeak,
            Cr = effectiveCr > 0 ? effectiveCr : (entity.Cr > 0 ? entity.Cr : 10.0),
        });
    }

    [HttpPut("parameters")]
    [RequireDeclaredWriteScope]
    [RemoteCommand(Invalidates = ["GetParameters", "GetSettings"])]
    [ProducesResponseType(typeof(CgmSimParameters), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimParameters>> UpdateParameters(
        [FromBody] CgmSimParameters model,
        CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        // Simulator-specific kinetics
        entity.CarbsAbsTime = model.CarbsAbsTime;
        entity.InsulinPeak = model.InsulinPeak;

        // Keep entity values updated as fallback
        if (model.Isf > 0) entity.Isf = model.Isf;
        if (model.Dia > 0) entity.Dia = model.Dia;
        if (model.Cr > 0) entity.Cr = model.Cr;

        await db.SaveChangesAsync(ct);

        var nowMills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var effectiveIsf = await _sensitivityResolver.GetSensitivityAsync(nowMills, ct: ct);
        var effectiveCr = await _carbRatioResolver.GetCarbRatioAsync(nowMills, ct: ct);
        var effectiveDia = await _therapySettingsResolver.GetDIAAsync(nowMills, ct: ct);

        return Ok(new CgmSimParameters
        {
            Isf = effectiveIsf > 0 ? effectiveIsf : entity.Isf,
            Dia = effectiveDia > 0 ? effectiveDia : entity.Dia,
            CarbsAbsTime = entity.CarbsAbsTime,
            InsulinPeak = entity.InsulinPeak,
            Cr = effectiveCr > 0 ? effectiveCr : entity.Cr,
        });
    }

    [HttpGet("settings")]
    [RequireScope(Scope.TherapyRead)]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimSettings>> GetSettings(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        var nowMills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var effectiveIsf = await _sensitivityResolver.GetSensitivityAsync(nowMills, ct: ct);
        var effectiveCr = await _carbRatioResolver.GetCarbRatioAsync(nowMills, ct: ct);
        var effectiveDia = await _therapySettingsResolver.GetDIAAsync(nowMills, ct: ct);

        var subject = await ResolveEffectiveSubjectAsync(entity, ct);

        return Ok(new CgmSimSettings
        {
            Subject = subject,
            Parameters = new CgmSimParameters
            {
                Isf = effectiveIsf > 0 ? effectiveIsf : (entity.Isf > 0 ? entity.Isf : 50.0),
                Dia = effectiveDia > 0 ? effectiveDia : (entity.Dia > 0 ? entity.Dia : 4.0),
                CarbsAbsTime = entity.CarbsAbsTime,
                InsulinPeak = entity.InsulinPeak,
                Cr = effectiveCr > 0 ? effectiveCr : (entity.Cr > 0 ? entity.Cr : 10.0),
            },
            IsInitialized = entity.IsInitialized,
            UpdatedAt = entity.UpdatedAt,
        });
    }

    [HttpPost("initialize")]
    [RequireDeclaredWriteScope]
    [RemoteCommand(Invalidates = ["GetSettings", "GetSubject", "GetParameters"])]
    [ProducesResponseType(typeof(CgmSimSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimSettings>> Initialize(
        [FromBody] CgmSimInitializeRequest request,
        CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        // Update PatientRecord
        var patient = await _patientRecordRepository.GetOrCreateAsync(ct);
        if (request.Subject.Age > 0)
        {
            var birthYear = DateTime.UtcNow.Year - request.Subject.Age;
            patient.DateOfBirth = new DateOnly(birthYear, 1, 1);
        }

        if (string.Equals(request.Subject.Sex, "female", StringComparison.OrdinalIgnoreCase))
        {
            patient.Sex = Nocturne.Core.Models.V4.BiologicalSex.Female;
        }
        else if (string.Equals(request.Subject.Sex, "male", StringComparison.OrdinalIgnoreCase))
        {
            patient.Sex = Nocturne.Core.Models.V4.BiologicalSex.Male;
        }

        await _patientRecordRepository.UpdateAsync(patient, Nocturne.Core.Contracts.V4.WriteOrigin.Live, ct);

        // Update BodyWeight
        if (request.Subject.Weight > 0)
        {
            await _bodyWeightService.CreateBodyWeightsAsync(new[]
            {
                new Nocturne.Core.Models.BodyWeight
                {
                    WeightKg = (decimal)request.Subject.Weight,
                    Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    EnteredBy = "cgmsim-init",
                }
            }, ct);
        }

        entity.Sex = request.Subject.Sex;
        entity.Weight = request.Subject.Weight;
        entity.Age = request.Subject.Age;
        entity.Height = request.Subject.Height;

        entity.Isf = request.Parameters.Isf;
        entity.Dia = request.Parameters.Dia;
        entity.CarbsAbsTime = request.Parameters.CarbsAbsTime;
        entity.InsulinPeak = request.Parameters.InsulinPeak;
        entity.Cr = request.Parameters.Cr;

        entity.IsInitialized = true;

        await db.SaveChangesAsync(ct);

        var nowMills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var effectiveIsf = await _sensitivityResolver.GetSensitivityAsync(nowMills, ct: ct);
        var effectiveCr = await _carbRatioResolver.GetCarbRatioAsync(nowMills, ct: ct);
        var effectiveDia = await _therapySettingsResolver.GetDIAAsync(nowMills, ct: ct);

        var subject = await ResolveEffectiveSubjectAsync(entity, ct);

        return Ok(new CgmSimSettings
        {
            Subject = subject,
            Parameters = new CgmSimParameters
            {
                Isf = effectiveIsf > 0 ? effectiveIsf : entity.Isf,
                Dia = effectiveDia > 0 ? effectiveDia : entity.Dia,
                CarbsAbsTime = entity.CarbsAbsTime,
                InsulinPeak = entity.InsulinPeak,
                Cr = effectiveCr > 0 ? effectiveCr : entity.Cr,
            },
            IsInitialized = entity.IsInitialized,
            UpdatedAt = entity.UpdatedAt,
        });
    }

    [HttpGet("wizard-preview")]
    [RequireScope(Scope.TherapyRead)]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimWizardPreview), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimWizardPreview>> GetWizardPreview(
        [FromQuery] double carbs = 0,
        [FromQuery] double adjustment = 0,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        var nowMills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var effectiveIsf = await _sensitivityResolver.GetSensitivityAsync(nowMills, ct: ct);
        var effectiveCr = await _carbRatioResolver.GetCarbRatioAsync(nowMills, ct: ct);

        var isf = effectiveIsf > 0 ? effectiveIsf : (entity.Isf > 0 ? entity.Isf : 50.0);
        var cr = effectiveCr > 0 ? effectiveCr : (entity.Cr > 0 ? entity.Cr : 10.0);
        var entries = (await _entryService.GetEntriesAsync("sgv", 1, 0, ct)).ToList();
        var currentEntry = entries.FirstOrDefault();
        double? currentBg = currentEntry is not null && (nowMills - currentEntry.Mills) <= 30 * 60 * 1000L
            ? (currentEntry.Sgv ?? currentEntry.Mgdl)
            : null;

        var targetRange = await _targetRangeResolver.GetBGTargetRangeAsync(nowMills, null, ct);
        var targetBg = (targetRange.Low + targetRange.High) / 2.0;
        if (targetBg <= 0)
        {
            targetBg = 100.0;
        }

        var iobFrom = DateTime.UtcNow.AddHours(-8);
        var cobFrom = DateTime.UtcNow.AddHours(-6);

        var boluses = (await _bolusRepository.GetAsync(
            from: iobFrom, to: null, device: null, source: null,
            limit: 1000, offset: 0, descending: false, ct: ct)).ToList();
        var tempBasals = (await _tempBasalRepository.GetAsync(
            from: iobFrom, to: null, device: null, source: null,
            limit: 1000, offset: 0, descending: false, ct: ct)).ToList();
        var carbIntakes = (await _carbIntakeRepository.GetAsync(
            from: cobFrom, to: null, device: null, source: null,
            limit: 1000, offset: 0, descending: false, ct: ct)).ToList();

        var iobResult = await _iobCalculator.CalculateTotalAsync(boluses, tempBasals, nowMills, ct: ct);
        var cobResult = await _cobCalculator.CalculateTotalAsync(carbIntakes, boluses, tempBasals, nowMills, ct: ct);

        var bgUnits = currentBg.HasValue
            ? Math.Round(((currentBg.Value - targetBg) / isf) * 10.0, MidpointRounding.AwayFromZero) / 10.0
            : 0.0;
        var choUnits = Math.Round((carbs / cr) * 10.0, MidpointRounding.AwayFromZero) / 10.0;
        var cobUnits = Math.Round((cobResult.Cob / cr) * 10.0, MidpointRounding.AwayFromZero) / 10.0;
        var iobUnits = Math.Round((-iobResult.Iob) * 10.0, MidpointRounding.AwayFromZero) / 10.0;
        var adjUnits = Math.Round(adjustment * 10.0, MidpointRounding.AwayFromZero) / 10.0;

        var recommended = Math.Max(
            0.0,
            Math.Round((bgUnits + choUnits + cobUnits + iobUnits + adjUnits) * 10.0, MidpointRounding.AwayFromZero) / 10.0);

        return Ok(new CgmSimWizardPreview
        {
            Carbs = carbs,
            BgUnits = bgUnits,
            ChoUnits = choUnits,
            CobUnits = cobUnits,
            IobUnits = iobUnits,
            AdjustmentUnits = adjUnits,
            RecommendedBolus = recommended,
            CurrentBg = currentBg,
            TargetBg = targetBg,
            Isf = isf,
            Cr = cr,
        });
    }

    private async Task<CgmSimSubject> ResolveEffectiveSubjectAsync(CgmSimSettingsEntity entity, CancellationToken ct)
    {
        var patient = await _patientRecordRepository.GetAsync(ct);
        var weights = await _bodyWeightService.GetBodyWeightsAsync(count: 1, skip: 0, cancellationToken: ct);
        var latestWeight = weights.FirstOrDefault();

        var sex = patient?.Sex switch
        {
            Nocturne.Core.Models.V4.BiologicalSex.Female => "female",
            Nocturne.Core.Models.V4.BiologicalSex.Male => "male",
            _ => !string.IsNullOrEmpty(entity.Sex) ? entity.Sex : "male",
        };

        var age = 30;
        if (patient?.DateOfBirth.HasValue == true)
        {
            var dob = patient.DateOfBirth.Value;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
        }
        else if (entity.Age > 0)
        {
            age = entity.Age;
        }

        var weight = latestWeight is not null && latestWeight.WeightKg > 0
            ? (double)latestWeight.WeightKg
            : (entity.Weight > 0 ? entity.Weight : 70.0);

        var height = entity.Height > 0 ? entity.Height : 170.0;

        return new CgmSimSubject
        {
            Sex = sex,
            Weight = weight,
            Age = Math.Max(1, Math.Min(150, age)),
            Height = height,
        };
    }

    private static async Task<CgmSimSettingsEntity> GetOrCreateSettingsAsync(
        NocturneDbContext db,
        CancellationToken ct)
    {
        var entity = await db.CgmSimSettings.FirstOrDefaultAsync(ct);
        if (entity is not null)
        {
            return entity;
        }

        entity = new CgmSimSettingsEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = db.TenantId,
        };

        db.CgmSimSettings.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var existing = await db.CgmSimSettings.FirstOrDefaultAsync(ct);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }

        return entity;
    }

    [HttpGet("noise")]
    [RequireScope(Scope.TherapyRead)]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimNoiseData), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimNoiseData>> GetNoise(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        var points = string.IsNullOrEmpty(entity.NoisePointsJson)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<CgmSimNoisePoint>>(entity.NoisePointsJson) ?? [];

        return Ok(new CgmSimNoiseData
        {
            Points = points,
            GeneratedAt = entity.NoiseGeneratedAt,
        });
    }

    [HttpPut("noise")]
    [RequireDeclaredWriteScope]
    [RemoteCommand(Invalidates = ["GetNoise"])]
    [ProducesResponseType(typeof(CgmSimNoiseData), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimNoiseData>> UpdateNoise(
        [FromBody] CgmSimNoiseData model,
        CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        entity.NoisePointsJson = System.Text.Json.JsonSerializer.Serialize(model.Points ?? []);
        entity.NoiseGeneratedAt = model.GeneratedAt ?? DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new CgmSimNoiseData
        {
            Points = model.Points ?? [],
            GeneratedAt = entity.NoiseGeneratedAt,
        });
    }
}
