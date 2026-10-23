using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenApi.Remote.Attributes;
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

    public CgmSimController(INocturneDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
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
