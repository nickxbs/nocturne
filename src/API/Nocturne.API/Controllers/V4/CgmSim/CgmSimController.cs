using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.CgmSim;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Services;

namespace Nocturne.API.Controllers.V4.CgmSim;

[ApiController]
[Authorize]
[Route("api/v4/cgmsim")]
[Tags("CgmSim")]
public class CgmSimController : ControllerBase
{
    private readonly INocturneDbContextFactory _contextFactory;
    private readonly IEntryService _entryService;
    private readonly IIobCalculator _iobCalculator;
    private readonly ICobCalculator _cobCalculator;
    private readonly IBolusRepository _bolusRepository;
    private readonly ITempBasalRepository _tempBasalRepository;
    private readonly ICarbIntakeRepository _carbIntakeRepository;
    private readonly ITargetRangeResolver _targetRangeResolver;

    public CgmSimController(
        INocturneDbContextFactory contextFactory,
        IEntryService entryService,
        IIobCalculator iobCalculator,
        ICobCalculator cobCalculator,
        IBolusRepository bolusRepository,
        ITempBasalRepository tempBasalRepository,
        ICarbIntakeRepository carbIntakeRepository,
        ITargetRangeResolver targetRangeResolver)
    {
        _contextFactory = contextFactory;
        _entryService = entryService;
        _iobCalculator = iobCalculator;
        _cobCalculator = cobCalculator;
        _bolusRepository = bolusRepository;
        _tempBasalRepository = tempBasalRepository;
        _carbIntakeRepository = carbIntakeRepository;
        _targetRangeResolver = targetRangeResolver;
    }

    [HttpGet("subject")]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimSubject), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimSubject>> GetSubject(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        return Ok(new CgmSimSubject
        {
            Sex = entity.Sex,
            Weight = entity.Weight,
            Age = entity.Age,
            Height = entity.Height,
        });
    }

    [HttpPut("subject")]
    [RemoteCommand(Invalidates = ["GetSubject", "GetSettings"])]
    [ProducesResponseType(typeof(CgmSimSubject), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimSubject>> UpdateSubject(
        [FromBody] CgmSimSubject model,
        CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        entity.Sex = model.Sex;
        entity.Weight = model.Weight;
        entity.Age = model.Age;
        entity.Height = model.Height;

        await db.SaveChangesAsync(ct);

        return Ok(new CgmSimSubject
        {
            Sex = entity.Sex,
            Weight = entity.Weight,
            Age = entity.Age,
            Height = entity.Height,
        });
    }

    [HttpGet("parameters")]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimParameters), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimParameters>> GetParameters(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        return Ok(new CgmSimParameters
        {
            Isf = entity.Isf,
            Dia = entity.Dia,
            CarbsAbsTime = entity.CarbsAbsTime,
            InsulinPeak = entity.InsulinPeak,
            Cr = entity.Cr,
        });
    }

    [HttpPut("parameters")]
    [RemoteCommand(Invalidates = ["GetParameters", "GetSettings"])]
    [ProducesResponseType(typeof(CgmSimParameters), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimParameters>> UpdateParameters(
        [FromBody] CgmSimParameters model,
        CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        entity.Isf = model.Isf;
        entity.Dia = model.Dia;
        entity.CarbsAbsTime = model.CarbsAbsTime;
        entity.InsulinPeak = model.InsulinPeak;
        entity.Cr = model.Cr;

        await db.SaveChangesAsync(ct);

        return Ok(new CgmSimParameters
        {
            Isf = entity.Isf,
            Dia = entity.Dia,
            CarbsAbsTime = entity.CarbsAbsTime,
            InsulinPeak = entity.InsulinPeak,
            Cr = entity.Cr,
        });
    }

    [HttpGet("settings")]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimSettings>> GetSettings(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        return Ok(new CgmSimSettings
        {
            Subject = new CgmSimSubject
            {
                Sex = entity.Sex,
                Weight = entity.Weight,
                Age = entity.Age,
                Height = entity.Height,
            },
            Parameters = new CgmSimParameters
            {
                Isf = entity.Isf,
                Dia = entity.Dia,
                CarbsAbsTime = entity.CarbsAbsTime,
                InsulinPeak = entity.InsulinPeak,
                Cr = entity.Cr,
            },
            IsInitialized = entity.IsInitialized,
            UpdatedAt = entity.UpdatedAt,
        });
    }

    [HttpPost("initialize")]
    [RemoteCommand(Invalidates = ["GetSettings", "GetSubject", "GetParameters"])]
    [ProducesResponseType(typeof(CgmSimSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimSettings>> Initialize(
        [FromBody] CgmSimInitializeRequest request,
        CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

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

        return Ok(new CgmSimSettings
        {
            Subject = new CgmSimSubject
            {
                Sex = entity.Sex,
                Weight = entity.Weight,
                Age = entity.Age,
                Height = entity.Height,
            },
            Parameters = new CgmSimParameters
            {
                Isf = entity.Isf,
                Dia = entity.Dia,
                CarbsAbsTime = entity.CarbsAbsTime,
                InsulinPeak = entity.InsulinPeak,
                Cr = entity.Cr,
            },
            IsInitialized = entity.IsInitialized,
            UpdatedAt = entity.UpdatedAt,
        });
    }

    [HttpGet("wizard-preview")]
    [RemoteQuery]
    [ProducesResponseType(typeof(CgmSimWizardPreview), StatusCodes.Status200OK)]
    public async Task<ActionResult<CgmSimWizardPreview>> GetWizardPreview(
        [FromQuery] double carbs = 0,
        [FromQuery] double adjustment = 0,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateAsync(ct);
        var entity = await GetOrCreateSettingsAsync(db, ct);

        var isf = entity.Isf > 0 ? entity.Isf : 50.0;
        var cr = entity.Cr > 0 ? entity.Cr : 10.0;

        var nowMills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
}
