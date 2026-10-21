using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Nocturne.API.Services.BackgroundServices;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Services;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Services.BackgroundServices;

[Trait("Category", "Unit")]
public class EnrolmentPruningServiceTests
{
    private readonly DateTimeOffset _origin = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private (EnrolmentPruningService Sut, Mock<ISubjectService> SubjectService, string DbName) CreateSut(FakeTimeProvider timeProvider)
    {
        var dbName = $"enrolment_pruning_{Guid.NewGuid()}";
        var subjectServiceMock = new Mock<ISubjectService>();
        subjectServiceMock
            .Setup(s => s.DeleteSubjectAsync(It.IsAny<Guid>()))
            .ReturnsAsync(true);

        var factoryMock = new Mock<INocturneDbContextFactory>();
        factoryMock
            .Setup(f => f.CreatePlatformAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(TestDbContextFactory.CreateInMemoryContext(dbName)));

        var services = new ServiceCollection();
        services.AddSingleton(factoryMock.Object);
        services.AddSingleton(subjectServiceMock.Object);
        var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = new EnrolmentPruningService(scopeFactory, NullLogger<EnrolmentPruningService>.Instance, timeProvider);

        return (sut, subjectServiceMock, dbName);
    }

    private static async Task SeedSubjectAsync(
        string dbName,
        Guid id,
        string name,
        string username,
        bool isActive,
        bool isSystemSubject,
        DateTime createdAt,
        string approvalStatus = "Approved",
        string? accessRequestMessage = null)
    {
        await using var context = TestDbContextFactory.CreateInMemoryContext(dbName);
        var subject = new SubjectEntity
        {
            Id = id,
            Name = name,
            Username = username,
            IsActive = isActive,
            IsSystemSubject = isSystemSubject,
            ApprovalStatus = approvalStatus,
            AccessRequestMessage = accessRequestMessage,
        };
        context.Subjects.Add(subject);
        await context.SaveChangesAsync();

        subject.CreatedAt = createdAt;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenSubjectOlderThan24HoursAndNoCredentials_DeletesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "Abandoned User",
            "abandoned",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-25));

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(1);
        subjectService.Verify(s => s.DeleteSubjectAsync(subjectId), Times.Once);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenSubjectRecent_PreservesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "Recent In-Flight User",
            "inflight",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-2));

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(0);
        subjectService.Verify(s => s.DeleteSubjectAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenSubjectIsActive_PreservesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "Active User",
            "active",
            isActive: true,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-48));

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(0);
        subjectService.Verify(s => s.DeleteSubjectAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenSubjectIsSystemSubject_PreservesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "System Subject",
            "system",
            isActive: false,
            isSystemSubject: true,
            createdAt: _origin.UtcDateTime.AddHours(-48));

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(0);
        subjectService.Verify(s => s.DeleteSubjectAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenSubjectHasPasskey_PreservesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "Passkey User",
            "passkeyuser",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-30));

        await using (var context = TestDbContextFactory.CreateInMemoryContext(dbName))
        {
            context.PasskeyCredentials.Add(new PasskeyCredentialEntity
            {
                Id = Guid.CreateVersion7(),
                SubjectId = subjectId,
                CredentialId = [1, 2, 3],
                PublicKey = [4, 5, 6],
            });
            await context.SaveChangesAsync();
        }

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(0);
        subjectService.Verify(s => s.DeleteSubjectAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenSubjectHasTotp_PreservesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "Totp User",
            "totpuser",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-30));

        await using (var context = TestDbContextFactory.CreateInMemoryContext(dbName))
        {
            context.TotpCredentials.Add(new TotpCredentialEntity
            {
                Id = Guid.CreateVersion7(),
                SubjectId = subjectId,
                SecretKey = [1, 2, 3],
            });
            await context.SaveChangesAsync();
        }

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(0);
        subjectService.Verify(s => s.DeleteSubjectAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenSubjectHasOidcIdentity_PreservesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "OIDC User",
            "oidcuser",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-30));

        await using (var context = TestDbContextFactory.CreateInMemoryContext(dbName))
        {
            context.SubjectOidcIdentities.Add(new SubjectOidcIdentityEntity
            {
                Id = Guid.CreateVersion7(),
                SubjectId = subjectId,
                ProviderId = Guid.CreateVersion7(),
                OidcSubjectId = "sub-123",
                Issuer = "https://issuer.example.com",
            });
            await context.SaveChangesAsync();
        }

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(0);
        subjectService.Verify(s => s.DeleteSubjectAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenSubjectHasTenantMembership_PreservesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "Member User",
            "memberuser",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-30));

        await using (var context = TestDbContextFactory.CreateInMemoryContext(dbName))
        {
            context.TenantMembers.Add(new TenantMemberEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = Guid.CreateVersion7(),
                SubjectId = subjectId,
            });
            await context.SaveChangesAsync();
        }

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(0);
        subjectService.Verify(s => s.DeleteSubjectAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenAbandonedAnonymousAccessRequest_DeletesSubject()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);
        var subjectId = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            subjectId,
            "Anonymous Requester",
            "anon-requester",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-30),
            approvalStatus: "Pending",
            accessRequestMessage: "Please let me in");

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(1);
        subjectService.Verify(s => s.DeleteSubjectAsync(subjectId), Times.Once);
    }

    [Fact]
    public async Task PruneAbandonedEnrolments_WhenMixedSubjects_DeletesOnlyAbandonedCandidates()
    {
        var time = new FakeTimeProvider(_origin);
        var (sut, subjectService, dbName) = CreateSut(time);

        var abandoned1 = Guid.CreateVersion7();
        var abandoned2 = Guid.CreateVersion7();
        var activeUser = Guid.CreateVersion7();
        var recentUser = Guid.CreateVersion7();
        var memberUser = Guid.CreateVersion7();

        await SeedSubjectAsync(
            dbName,
            abandoned1,
            "Abandoned 1",
            "abandoned1",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-30));

        await SeedSubjectAsync(
            dbName,
            abandoned2,
            "Abandoned 2",
            "abandoned2",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-25));

        await SeedSubjectAsync(
            dbName,
            activeUser,
            "Active User",
            "active",
            isActive: true,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-40));

        await SeedSubjectAsync(
            dbName,
            recentUser,
            "Recent In-Flight",
            "recent",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-1));

        await SeedSubjectAsync(
            dbName,
            memberUser,
            "Tenant Member",
            "member",
            isActive: false,
            isSystemSubject: false,
            createdAt: _origin.UtcDateTime.AddHours(-50));

        await using (var context = TestDbContextFactory.CreateInMemoryContext(dbName))
        {
            context.TenantMembers.Add(new TenantMemberEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = Guid.CreateVersion7(),
                SubjectId = memberUser,
            });
            await context.SaveChangesAsync();
        }

        var pruned = await sut.PruneAbandonedEnrolmentsAsync(CancellationToken.None);

        pruned.Should().Be(2);
        subjectService.Verify(s => s.DeleteSubjectAsync(abandoned1), Times.Once);
        subjectService.Verify(s => s.DeleteSubjectAsync(abandoned2), Times.Once);
        subjectService.Verify(s => s.DeleteSubjectAsync(activeUser), Times.Never);
        subjectService.Verify(s => s.DeleteSubjectAsync(recentUser), Times.Never);
        subjectService.Verify(s => s.DeleteSubjectAsync(memberUser), Times.Never);
    }
}
