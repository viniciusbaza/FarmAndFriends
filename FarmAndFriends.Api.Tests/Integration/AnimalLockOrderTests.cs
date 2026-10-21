using System.Data.Common;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class AnimalLockOrderTests
{
    [PostgresFact]
    public async Task CropCare_AndEnclosurePurchase_DoNotInvertFarmAndPlotLocks()
    {
        var s = await AnimalEnclosureIntegrationTests.Scenario.Create();
        Guid plotId; var opportunity = Guid.NewGuid();
        await using (var setup = s.Db())
        {
            var plot = await setup.Plots.FirstAsync(p => p.FarmId == s.Farm && p.Unlocked);
            plotId = plot.Id; plot.SeedId = "carrot"; plot.CareOpportunityId = opportunity;
            plot.PlantedAt = s.Clock.Now; plot.CurrentHarvestCycle = 1;
            plot.CurrentHarvestCycleStartedAt = s.Clock.Now; plot.ReadyAt = s.Clock.Now.AddHours(1);
            plot.RemainingYield = 3;
            await setup.SaveChangesAsync();
        }
        var observer = new CareLockObserver();
        await using var careDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!)
            .AddInterceptors(observer).Options);
        var care = new CropCareService(careDb, new ExperienceService(), Options.Create(new CropCareOptions()),
            NullLogger<CropCareService>.Instance, s.Clock);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var blocker = s.Db();
        await using var transaction = await blocker.Database.BeginTransactionAsync(timeout.Token);
        await blocker.LockFarmAsync(s.Farm, timeout.Token);
        var careTask = care.CareAsync(s.Visitors[0], s.Farm, plotId, opportunity, Guid.NewGuid(), timeout.Token);
        await observer.Reached.Task.WaitAsync(timeout.Token);
        var purchaseTask = s.Command(s.Owner, "buy-animal-enclosure");
        try
        {
            // If care takes Plot before Farm, this NOWAIT fails: care waits for our
            // Farm FK while the new purchaser would wait for care's Plot lock.
            await blocker.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT "Id" FROM "Plots" WHERE "Id" = {plotId} FOR UPDATE NOWAIT
                """, timeout.Token);
        }
        finally { await transaction.RollbackAsync(CancellationToken.None); }
        Assert.True((await careTask).Succeeded);
        Assert.False((await purchaseTask).Replayed);
        await using var check = s.Db();
        Assert.Equal(1, await check.CropCareCompletions.CountAsync(c => c.VisitorUserId == s.Visitors[0]));
        Assert.Equal(1, await check.AnimalEnclosures.CountAsync(e => e.FarmId == s.Farm));
        Assert.Equal(20002, (await check.Inventories.SingleAsync(i => i.UserId == s.Visitors[0])).Coins);
    }

    private sealed class CareLockObserver : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Farms\"") && command.CommandText.Contains("FOR UPDATE")) Reached.TrySetResult();
            return new(result);
        }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Plots\"") && command.CommandText.Contains("FOR UPDATE")) Reached.TrySetResult();
            return new(result);
        }
    }
}
