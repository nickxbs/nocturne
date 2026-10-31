using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Infrastructure.Data.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service that periodically removes uncompleted enrollments and abandoned
/// registration subject records older than 24 hours to prevent database clutter.
/// </summary>
public class EnrolmentPruningService(
    IServiceScopeFactory scopeFactory,
    ILogger<EnrolmentPruningService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, timeProvider, stoppingToken);

            using var timer = new PeriodicTimer(Interval, timeProvider);
            do
            {
                try
                {
                    await PruneAbandonedEnrolmentsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error occurred while pruning abandoned enrolments");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    /// <summary>
    /// Identifies and removes unactivated subjects older than 24 hours with no credentials,
    /// OIDC identities, or tenant memberships.
    /// </summary>
    internal async Task<int> PruneAbandonedEnrolmentsAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<INocturneDbContextFactory>();
        var subjectService = scope.ServiceProvider.GetRequiredService<ISubjectService>();

        var cutoff = timeProvider.GetUtcNow().UtcDateTime - RetentionPeriod;
        List<Guid> candidateIds;

        await using (var context = await dbFactory.CreatePlatformAsync(ct))
        {
            candidateIds = await context.Subjects
                .Where(s => !s.IsActive
                    && !s.IsSystemSubject
                    && s.CreatedAt < cutoff
                    && !context.PasskeyCredentials.Any(c => c.SubjectId == s.Id)
                    && !context.SubjectOidcIdentities.Any(o => o.SubjectId == s.Id)
                    && !context.TotpCredentials.Any(t => t.SubjectId == s.Id)
                    && !context.RecoveryCodes.Any(r => r.SubjectId == s.Id)
                    && !context.TenantMembers.Any(tm => tm.SubjectId == s.Id))
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        var prunedCount = 0;
        foreach (var id in candidateIds)
        {
            try
            {
                if (await subjectService.DeleteSubjectAsync(id))
                {
                    prunedCount++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to delete abandoned subject {SubjectId}", id);
            }
        }

        if (prunedCount > 0)
        {
            logger.LogInformation(
                "Pruned {Count} abandoned enrolling subjects older than {Cutoff}",
                prunedCount, cutoff);
        }

        return prunedCount;
    }
}
