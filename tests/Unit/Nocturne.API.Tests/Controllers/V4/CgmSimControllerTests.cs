using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nocturne.API.Controllers.V4.CgmSim;
using Nocturne.Core.Models.CgmSim;
using Nocturne.Infrastructure.Data;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4;

public class CgmSimControllerTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();

    [Fact]
    public async Task GetSubject_CreatesDefaultSettingsOnFirstAccess()
    {
        var (controller, options) = NewController();

        var result = await controller.GetSubject(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var subject = ok.Value.Should().BeOfType<CgmSimSubject>().Subject;
        subject.Sex.Should().Be("other");
        subject.Weight.Should().Be(70.0);
        subject.Age.Should().Be(30);
        subject.Height.Should().Be(170.0);

        await using var db = new NocturneDbContext(options) { TenantId = Tenant };
        (await db.CgmSimSettings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateSubject_PersistsChangesAndReturnsUpdatedModel()
    {
        var (controller, options) = NewController();

        var updateModel = new CgmSimSubject
        {
            Sex = "female",
            Weight = 62.5,
            Age = 28,
            Height = 168.0,
        };

        var result = await controller.UpdateSubject(updateModel, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var subject = ok.Value.Should().BeOfType<CgmSimSubject>().Subject;
        subject.Sex.Should().Be("female");
        subject.Weight.Should().Be(62.5);
        subject.Age.Should().Be(28);
        subject.Height.Should().Be(168.0);

        await using var db = new NocturneDbContext(options) { TenantId = Tenant };
        var stored = await db.CgmSimSettings.FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Sex.Should().Be("female");
        stored.Weight.Should().Be(62.5);
        stored.Age.Should().Be(28);
        stored.Height.Should().Be(168.0);
    }

    [Fact]
    public async Task GetParameters_CreatesDefaultSettingsOnFirstAccess()
    {
        var (controller, options) = NewController();

        var result = await controller.GetParameters(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var parameters = ok.Value.Should().BeOfType<CgmSimParameters>().Subject;
        parameters.Isf.Should().Be(50.0);
        parameters.Dia.Should().Be(4.0);
        parameters.CarbsAbsTime.Should().Be(180);
        parameters.InsulinPeak.Should().Be(75);
        parameters.Cr.Should().Be(10.0);

        await using var db = new NocturneDbContext(options) { TenantId = Tenant };
        (await db.CgmSimSettings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateParameters_PersistsChangesAndReturnsUpdatedModel()
    {
        var (controller, options) = NewController();

        var updateModel = new CgmSimParameters
        {
            Isf = 45.0,
            Dia = 4.5,
            CarbsAbsTime = 180,
            InsulinPeak = 75,
            Cr = 10.5,
        };

        var result = await controller.UpdateParameters(updateModel, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var parameters = ok.Value.Should().BeOfType<CgmSimParameters>().Subject;
        parameters.Isf.Should().Be(45.0);
        parameters.Dia.Should().Be(4.5);
        parameters.CarbsAbsTime.Should().Be(180);
        parameters.InsulinPeak.Should().Be(75);
        parameters.Cr.Should().Be(10.5);

        await using var db = new NocturneDbContext(options) { TenantId = Tenant };
        var stored = await db.CgmSimSettings.FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Isf.Should().Be(45.0);
        stored.Dia.Should().Be(4.5);
        stored.CarbsAbsTime.Should().Be(180);
        stored.InsulinPeak.Should().Be(75);
        stored.Cr.Should().Be(10.5);
    }

    [Fact]
    public async Task GetSettings_ReturnsBothSubjectAndParameters()
    {
        var (controller, _) = NewController();

        await controller.UpdateSubject(new CgmSimSubject { Sex = "male", Weight = 80.0 }, CancellationToken.None);
        await controller.UpdateParameters(new CgmSimParameters { Isf = 50.0, Cr = 12.0 }, CancellationToken.None);

        var result = await controller.GetSettings(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var settings = ok.Value.Should().BeOfType<CgmSimSettings>().Subject;
        settings.Subject.Sex.Should().Be("male");
        settings.Subject.Weight.Should().Be(80.0);
        settings.Parameters.Isf.Should().Be(50.0);
        settings.Parameters.Cr.Should().Be(12.0);
        settings.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_PersistsBothSubjectAndParametersAndSetsIsInitializedTrue()
    {
        var (controller, options) = NewController();

        var request = new CgmSimInitializeRequest
        {
            Subject = new CgmSimSubject
            {
                Sex = "female",
                Weight = 65.0,
                Age = 25,
                Height = 165.0,
            },
            Parameters = new CgmSimParameters
            {
                Isf = 40.0,
                Dia = 3.5,
                CarbsAbsTime = 150,
                InsulinPeak = 60,
                Cr = 9.0,
            },
        };

        var result = await controller.Initialize(request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var settings = ok.Value.Should().BeOfType<CgmSimSettings>().Subject;
        settings.IsInitialized.Should().BeTrue();
        settings.Subject.Sex.Should().Be("female");
        settings.Subject.Weight.Should().Be(65.0);
        settings.Parameters.Isf.Should().Be(40.0);
        settings.Parameters.Dia.Should().Be(3.5);

        await using var db = new NocturneDbContext(options) { TenantId = Tenant };
        var stored = await db.CgmSimSettings.FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.IsInitialized.Should().BeTrue();
        stored.Sex.Should().Be("female");
        stored.Weight.Should().Be(65.0);
        stored.Isf.Should().Be(40.0);
        stored.Dia.Should().Be(3.5);
    }

    private static (CgmSimController Controller, DbContextOptions<NocturneDbContext> Options) NewController()
    {
        var options = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var seed = new NocturneDbContext(options) { TenantId = Tenant };
        var factory = new TestNocturneDbContextFactory(seed);
        return (new CgmSimController(factory), options);
    }
}
