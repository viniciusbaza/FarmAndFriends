using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Contracts.Fertilizer;
using FarmAndFriends.Api.Contracts.Plots;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Mappers;
using FarmAndFriends.Api.Dtos.Theft;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class FertilizerIntegrationTests
{
    [Fact]
    public void DefaultPolicyUsesFixedOneHourReductionWithoutMinimumWait()
    {
        var policy = new FertilizerOptions();
        Assert.True(policy.IsValid());
        Assert.Equal(3600, policy.MaximumReductionSeconds);
        Assert.Equal(0, policy.MinimumRemainingSeconds);
        policy.MinimumRemainingSeconds = -1;
        Assert.False(policy.IsValid());
    }

    [PostgresFact]
    public async Task OfferAdvertisesFixedDurationWithoutObsoletePercentage()
    {
        var s = await Scenario.Create();
        await using var db = s.Db();
        var offer = s.Service(db).GetOffer();
        Assert.Equal("fertilizer", offer.ItemId);
        Assert.Equal(2, offer.PricePremiumCoins);
        Assert.Equal(3600, offer.MaxReductionSeconds);
        Assert.Equal(0, offer.MinRemainingSeconds);
        Assert.DoesNotContain("effectPercent", JsonSerializer.Serialize(offer,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [PostgresFact]
    public async Task BuyingUsesPremiumLedgerAndIsIdempotent()
    {
        var s = await Scenario.Create();
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(s.Buy(s.Owner, key, 2), s.Buy(s.Owner, key, 2));
        Assert.Equal(results[0].OperationId, results[1].OperationId);
        Assert.Single(results, result => result.Replayed);
        await using var db = s.Db();
        Assert.Equal(6, (await db.Inventories.SingleAsync(i => i.UserId == s.Owner)).PremiumCoins);
        Assert.Equal(2, (await db.InventoryItems.SingleAsync(i => i.Inventory.UserId == s.Owner
            && i.ItemId == FertilizerOptions.ItemId)).Quantity);
        Assert.Single(await db.PremiumCurrencyTransactions.Where(t => t.UserId == s.Owner).ToListAsync());
        var conflict = await Assert.ThrowsAsync<AnimalActionException>(() => s.Buy(s.Owner, key, 3));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", conflict.Code);
    }

    [PostgresFact]
    public async Task ApplyingOnFriendsFarmConsumesOnceAndAuditsOriginalReadyTime()
    {
        var s = await Scenario.Create();
        var key = Guid.NewGuid();
        var request = new FertilizePlotRequest(1, s.Now);
        var results = await Task.WhenAll(s.Apply(s.Visitor, key, request), s.Apply(s.Visitor, key, request));
        Assert.Equal(results[0].OperationId, results[1].OperationId);
        Assert.Single(results, result => result.Replayed);
        Assert.Equal(s.Now.AddHours(2), results[0].PreviousReadyAt);
        Assert.Equal(s.Now.AddHours(1), results[0].ReadyAt);
        await using var db = s.Db();
        Assert.Equal(0, (await db.InventoryItems.SingleAsync(i => i.Inventory.UserId == s.Visitor
            && i.ItemId == FertilizerOptions.ItemId)).Quantity);
        Assert.Equal(results[0].ReadyAt, (await db.Plots.SingleAsync(p => p.Id == s.Plot)).ReadyAt);
        Assert.Single(await db.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
        Assert.Single(await db.Notifications.Where(n => n.Type == NotificationType.PlotFertilized
            && n.RecipientUserId == s.Owner).ToListAsync());
        Assert.Equal(0, (await db.Users.SingleAsync(u => u.Id == s.Visitor)).CurrentXp);
        var repeat = await Assert.ThrowsAsync<AnimalActionException>(() => s.Apply(s.Owner, Guid.NewGuid(), request));
        Assert.Equal("ALREADY_FERTILIZED", repeat.Code);
    }

    [PostgresFact]
    public async Task OwnerCanApplyFertilizerWithoutChangingYieldOrCreatingSocialNotification()
    {
        var s = await Scenario.Create();
        var request = await s.SetCycle(TimeSpan.FromHours(4), TimeSpan.FromMinutes(58));
        await using (var db = s.Db())
        {
            var inventory = await db.Inventories.SingleAsync(i => i.UserId == s.Owner);
            db.InventoryItems.Add(new InventoryItem { InventoryId = inventory.Id,
                ItemType = ItemType.AnimalProduct, ItemId = FertilizerOptions.ItemId, Quantity = 1 });
            await db.SaveChangesAsync();
        }

        var result = await s.Apply(s.Owner, Guid.NewGuid(), request);
        Assert.Equal(s.Now.AddMinutes(58), result.PreviousReadyAt);
        Assert.Equal(s.Now, result.ReadyAt);

        await using var check = s.Db();
        var plot = await check.Plots.SingleAsync(p => p.Id == s.Plot);
        Assert.Equal(result.ReadyAt, plot.ReadyAt);
        Assert.Equal(2, plot.RemainingYield);
        Assert.Equal(0, (await check.InventoryItems.SingleAsync(i => i.Inventory.UserId == s.Owner
            && i.ItemId == FertilizerOptions.ItemId)).Quantity);
        Assert.Equal(0, (await check.Users.SingleAsync(u => u.Id == s.Owner)).CurrentXp);
        Assert.Empty(await check.Notifications.Where(n => n.Type == NotificationType.PlotFertilized
            && n.ActorUserId == s.Owner && n.RecipientUserId == s.Owner).ToListAsync());
    }

    [PostgresFact]
    public async Task EmptyReadyChangedAndRevokedFriendshipDoNotConsumeStock()
    {
        var s = await Scenario.Create();
        var wrong = await Assert.ThrowsAsync<AnimalActionException>(() =>
            s.Apply(s.Visitor, Guid.NewGuid(), new FertilizePlotRequest(2, s.Now)));
        Assert.Equal("CYCLE_CHANGED", wrong.Code);
        await using (var db = s.Db())
        {
            var (a, b) = FriendshipService.GetCanonicalPair(s.Owner, s.Visitor);
            (await db.Friendships.SingleAsync(f => f.UserAId == a && f.UserBId == b)).Status = FriendshipStatus.Declined;
            await db.SaveChangesAsync();
        }
        var denied = await Assert.ThrowsAsync<AnimalActionException>(() =>
            s.Apply(s.Visitor, Guid.NewGuid(), new FertilizePlotRequest(1, s.Now)));
        Assert.Equal("NOT_FRIENDS", denied.Code);
        await using (var db = s.Db())
        {
            var plot = await db.Plots.SingleAsync(p => p.Id == s.Plot);
            plot.SeedId = null;
            plot.PlantedAt = null;
            plot.CurrentHarvestCycle = null;
            plot.CurrentHarvestCycleStartedAt = null;
            plot.ReadyAt = null;
            plot.RemainingYield = null;
            await db.SaveChangesAsync();
        }
        var empty = await Assert.ThrowsAsync<AnimalActionException>(() =>
            s.Apply(s.Owner, Guid.NewGuid(), new FertilizePlotRequest(1, s.Now)));
        Assert.Equal("PLOT_EMPTY", empty.Code);
        await using var check = s.Db();
        Assert.Equal(1, (await check.InventoryItems.SingleAsync(i => i.Inventory.UserId == s.Visitor
            && i.ItemId == FertilizerOptions.ItemId)).Quantity);
        Assert.Empty(await check.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
    }

    [PostgresFact]
    public async Task MissingStockAndIncompatibleReplayDoNotChangePlotOrDebitAgain()
    {
        var s = await Scenario.Create();
        var request = await s.SetCycle(TimeSpan.FromHours(4), TimeSpan.FromMinutes(58));
        var noStock = await Assert.ThrowsAsync<AnimalActionException>(() =>
            s.Apply(s.Owner, Guid.NewGuid(), request));
        Assert.Equal("INSUFFICIENT_STOCK", noStock.Code);
        await using (var before = s.Db())
        {
            Assert.Equal(s.Now.AddMinutes(58), (await before.Plots.SingleAsync(x => x.Id == s.Plot)).ReadyAt);
            Assert.Empty(await before.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
        }
        var key = Guid.NewGuid();
        var applied = await s.Apply(s.Visitor, key, request);
        var mismatch = await Assert.ThrowsAsync<AnimalActionException>(() =>
            s.Apply(s.Visitor, key, request with { ExpectedHarvestCycle = 2 }));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", mismatch.Code);
        var staleCycle = await Assert.ThrowsAsync<AnimalActionException>(() =>
            s.Apply(s.Visitor, Guid.NewGuid(), request with { ExpectedCycleStartedAt = request.ExpectedCycleStartedAt.AddSeconds(-1) }));
        Assert.Equal("CYCLE_CHANGED", staleCycle.Code);
        await using var check = s.Db();
        Assert.Equal(applied.ReadyAt, (await check.Plots.SingleAsync(x => x.Id == s.Plot)).ReadyAt);
        Assert.Equal(0, (await check.InventoryItems.SingleAsync(x => x.Inventory.UserId == s.Visitor
            && x.ItemId == FertilizerOptions.ItemId)).Quantity);
        Assert.Single(await check.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
        Assert.Single(await check.Notifications.Where(x => x.RecipientUserId == s.Owner
            && x.Type == NotificationType.PlotFertilized).ToListAsync());
    }

    [PostgresFact]
    public async Task PigProducesFertilizerButCannotBeStolenOrSold()
    {
        var s = await AnimalEnclosureIntegrationTests.Scenario.Create();
        await using (var db = s.Db())
        {
            (await db.Users.SingleAsync(u => u.Id == s.Owner)).Level = 7;
            await db.SaveChangesAsync();
        }
        await s.Command(s.Owner, "buy-animal-enclosure");
        await s.Command(s.Owner, "buy-animal", item: "pig");
        var piglet = Assert.Single((await s.State(s.Owner)).Enclosure!.Animals);
        Assert.Equal("piglet", piglet.Stage);
        s.Clock.Now = piglet.MaturesAt;
        var adult = Assert.Single((await s.State(s.Owner)).Enclosure!.Animals);
        Assert.Equal("adult", adult.Stage);
        Assert.Equal("fertilizer", adult.Cycle!.ProductId);
        Assert.Equal(4, adult.Cycle.BaseYield);
        var helper = s.Visitors[0];
        await s.Command(helper, "help", farm: s.Farm, animal: adult.Id, cycle: adult.Cycle.Id);
        await using (var noticeDb = s.Db())
        {
            var notice = await noticeDb.Notifications.SingleAsync(n => n.AnimalCycleId == adult.Cycle.Id
                && n.Type == NotificationType.AnimalHelped);
            var helperName = (await noticeDb.Users.SingleAsync(u => u.Id == helper)).Username;
            Assert.Equal($"Você receberá +1 adubo na próxima coleta com a ajuda de {helperName}.", notice.Message);
        }
        s.Clock.Now = adult.Cycle.ReadyAt;
        Assert.Equal("NOT_STEALABLE", (await s.Attempt(helper, "steal", s.Farm, adult.Id, adult.Cycle.Id)).Error);
        var collect = await s.Command(s.Owner, "collect", farm: s.Farm, animal: adult.Id, cycle: adult.Cycle.Id);
        Assert.Equal(5, collect.Effect.Quantity);
        Assert.Equal(25, collect.Effect.XpGained);
        Assert.Equal("INVALID_REQUEST", (await s.Attempt(s.Owner, "sell-animal-product", item: "fertilizer", quantity: 1)).Error);
        await using var check = s.Db();
        Assert.Equal(5, (await check.InventoryItems.SingleAsync(i => i.Inventory.UserId == s.Owner
            && i.ItemId == "fertilizer")).Quantity);
        Assert.Null(AnimalSpeciesRules.FindProduct("fertilizer"));
        Assert.DoesNotContain(s.Service(check).GetProductOffers(), item => item.ItemId == "fertilizer");
    }

    [PostgresFact]
    public async Task AnimalHelp_SameVisitorRewardsAllFiveAnimalCyclesAndCannotRepeat()
    {
        var s = await AnimalEnclosureIntegrationTests.Scenario.Create();
        await using (var setup = s.Db())
        {
            (await setup.Users.SingleAsync(user => user.Id == s.Owner)).Level = 8;
            await setup.SaveChangesAsync();
        }
        await s.Command(s.Owner, "buy-animal-enclosure");
        foreach (var species in new[] { "chicken", "cow", "sheep", "pig", "peacock" })
            await s.Command(s.Owner, "buy-animal", item: species);

        var young = (await s.State(s.Owner)).Enclosure!.Animals;
        s.Clock.Now = young.Max(animal => animal.MaturesAt);
        var producing = (await s.State(s.Owner)).Enclosure!.Animals;
        Assert.Equal(5, producing.Count);
        Assert.All(producing, animal => Assert.False(animal.Cycle!.IsReady));

        var helper = s.Visitors[0];
        var results = await Task.WhenAll(producing.Select(animal =>
            s.Attempt(helper, "help", s.Farm, animal.Id, animal.Cycle!.Id)));
        Assert.All(results, result =>
        {
            Assert.NotNull(result.Response);
            Assert.Equal(2, result.Response!.Effect.CoinsGained);
            Assert.Equal(5, result.Response.Effect.XpGained);
            Assert.Equal(1, result.Response.Effect.OwnerBonusAdded);
        });

        var ownerState = await s.State(s.Owner);
        Assert.All(ownerState.Enclosure!.Animals, animal =>
        {
            Assert.Equal(1, animal.Cycle!.ProtectedHelpBonus);
            Assert.Equal(1, animal.Cycle.HelperCount);
            Assert.Equal(helper, Assert.Single(animal.Cycle.Helpers!).UserId);
        });
        var visitorState = await s.State(helper);
        Assert.All(visitorState.Enclosure!.Animals, animal =>
        {
            Assert.True(animal.Cycle!.ViewerHelped);
            Assert.False(animal.Permissions.CanHelp);
            Assert.False(animal.Permissions.RewardAvailable);
            Assert.Equal("HELP_ALREADY_REGISTERED", animal.Permissions.HelpReason);
            Assert.Null(animal.Permissions.NextRewardAt);
            Assert.Equal(0, animal.Cycle.BaseYield);
            Assert.Equal(0, animal.Cycle.ProtectedHelpBonus);
            Assert.Equal(0, animal.Cycle.OwnerCollectible);
            Assert.Null(animal.Cycle.Helpers);
        });

        var repeats = await Task.WhenAll(producing.Select(animal =>
            s.Attempt(helper, "help", s.Farm, animal.Id, animal.Cycle!.Id)));
        Assert.All(repeats, result => Assert.Equal("HELP_ALREADY_REGISTERED", result.Error));

        await using var db = s.Db();
        Assert.Equal(5, await db.AnimalActionCompletions.CountAsync(completion => completion.ActorUserId == helper
            && completion.Action == "help"));
        Assert.Equal(5, await db.Notifications.CountAsync(notification => notification.ActorUserId == helper
            && notification.Type == NotificationType.AnimalHelped));
        Assert.Equal(25, (await db.Users.SingleAsync(user => user.Id == helper)).CurrentXp);
        Assert.Equal(20010, (await db.Inventories.SingleAsync(inventory => inventory.UserId == helper)).Coins);
        Assert.All(await db.AnimalProductionCycles.Where(cycle => producing.Select(animal => animal.Id)
            .Contains(cycle.FarmAnimalId)).ToListAsync(), cycle => Assert.Equal(1, cycle.ProtectedHelpBonus));
    }

    [PostgresFact]
    public async Task PartialFertilizerEffectAllowsNormalTheftBeforeOriginalReadyTime()
    {
        var s = await Scenario.Create();
        await s.Apply(s.Visitor, Guid.NewGuid(), new FertilizePlotRequest(1, s.Now));
        var theft = Assert.IsType<TheftResponse>(Assert.IsType<OkObjectResult>(
            await s.Steal(s.Now.AddMinutes(100))).Value);
        Assert.Equal(1, theft.Stolen);
        Assert.Equal(1, theft.OwnerWillReceive);
        Assert.Equal(45, theft.XpGained);
        await using var db = s.Db();
        Assert.Single(await db.TheftLogs.Where(x => x.PlotId == s.Plot).ToListAsync());
        Assert.Equal(1, (await db.Plots.SingleAsync(x => x.Id == s.Plot)).RemainingYield);
        Assert.Equal(45, (await db.Users.SingleAsync(x => x.Id == s.Visitor)).CurrentXp);
        Assert.Equal(1, (await db.InventoryItems.SingleAsync(x => x.Inventory.UserId == s.Visitor
            && x.ItemType == ItemType.Crop)).Quantity);
        Assert.Equal(10000, (await db.Inventories.SingleAsync(x => x.UserId == s.Visitor)).Coins);
        Assert.Equal(10, (await db.Inventories.SingleAsync(x => x.UserId == s.Visitor)).PremiumCoins);
    }

    [PostgresFact]
    public async Task PartialEffectDoesNotAllowTheftWhileStillGrowing()
    {
        var s = await Scenario.Create();
        await s.Apply(s.Visitor, Guid.NewGuid(), new FertilizePlotRequest(1, s.Now));
        Assert.IsType<BadRequestObjectResult>(await s.Steal(s.Now.AddMinutes(59)));
        await using var db = s.Db();
        Assert.Empty(await db.TheftLogs.Where(x => x.PlotId == s.Plot).ToListAsync());
        Assert.Equal(2, (await db.Plots.SingleAsync(x => x.Id == s.Plot)).RemainingYield);
        Assert.Equal(0, (await db.Users.SingleAsync(x => x.Id == s.Visitor)).CurrentXp);
    }

    [PostgresFact]
    public async Task EqualAndExcessReductionCompleteImmediatelyAndAllowTheft()
    {
        foreach (var remainingSeconds in new[] { 3600, 3480, 1 })
        {
            var s = await Scenario.Create();
            var request = await s.SetCycle(TimeSpan.FromHours(4), TimeSpan.FromSeconds(remainingSeconds));
            var result = await s.Apply(s.Visitor, Guid.NewGuid(), request);
            Assert.Equal(s.Now, result.ReadyAt);
            Assert.Equal(s.Now.AddSeconds(remainingSeconds), result.PreviousReadyAt);
            Assert.Equal(0, result.RemainingQuantity);
            Assert.IsType<OkObjectResult>(await s.Steal(s.Now));
            await using var check = s.Db();
            var application = await check.FertilizerApplications.SingleAsync(x => x.PlotId == s.Plot);
            Assert.Equal(application.AppliedAt, application.ReadyAt);
            Assert.True(application.ReadyAt >= application.CycleStartedAt);
            Assert.Equal(1, (await check.Plots.SingleAsync(x => x.Id == s.Plot)).RemainingYield);
            Assert.Single(await check.Notifications.Where(x => x.RecipientUserId == s.Owner
                && x.Type == NotificationType.PlotFertilized).ToListAsync());
        }
    }

    [PostgresFact]
    public async Task LongCropAlwaysReceivesOneHourReduction()
    {
        var s = await Scenario.Create();
        var request = await s.SetCycle(TimeSpan.FromHours(8), TimeSpan.FromHours(2));
        var result = await s.Apply(s.Visitor, Guid.NewGuid(), request);
        Assert.Equal(s.Now.AddHours(1), result.ReadyAt);
        Assert.Equal(TimeSpan.FromHours(1), result.PreviousReadyAt - result.ReadyAt);
        Assert.IsType<BadRequestObjectResult>(await s.Steal(s.Now));
    }

    [PostgresFact]
    public async Task RegrowingCropCanCompleteImmediately()
    {
        var s = await Scenario.Create();
        var request = await s.SetCycle(TimeSpan.FromHours(8), TimeSpan.FromMinutes(30),
            cycle: 2, regrow: TimeSpan.FromHours(2));
        var result = await s.Apply(s.Visitor, Guid.NewGuid(), request);
        Assert.Equal(s.Now, result.ReadyAt);
        Assert.Equal(TimeSpan.FromMinutes(30), result.PreviousReadyAt - result.ReadyAt);
        Assert.IsType<OkObjectResult>(await s.Steal(s.Now));
    }

    [PostgresFact]
    public async Task ShortCropCanFinishAtCycleStartAndBeStolenAndHarvested()
    {
        foreach (var (cycle, duration) in new[]
        {
            (1, TimeSpan.FromMinutes(30)), (1, TimeSpan.FromHours(1)),
            (2, TimeSpan.FromMinutes(30)), (2, TimeSpan.FromHours(1))
        })
        {
            var s = await Scenario.Create();
            var request = cycle == 1
                ? await s.SetCycle(duration, duration)
                : await s.SetCycle(TimeSpan.FromHours(2), duration, 2, duration);
            Assert.Equal(s.Now, request.ExpectedCycleStartedAt);
            var result = await s.Apply(s.Visitor, Guid.NewGuid(), request);
            Assert.Equal(s.Now, result.ReadyAt);
            Assert.Equal(s.Now.Add(duration), result.PreviousReadyAt);
            await using (var snapshot = s.Db())
            {
                var farm = await snapshot.Farms.Include(f => f.User).Include(f => f.Plots)
                    .SingleAsync(f => f.Id == s.Farm);
                var states = await s.Service(snapshot).GetCurrentApplicationsAsync(s.Farm);
                var visible = FarmMapper.ToFarmResponse(farm, s.Now,
                    new Dictionary<Guid, PlotCropCareState>(), fertilizedAt: states);
                var visiblePlot = Assert.Single(visible.Plots);
                Assert.True(visiblePlot.IsReady);
                Assert.Equal(s.Now, visiblePlot.ReadyAt);
                Assert.Equal(s.Now, visiblePlot.FertilizedAt);
                Assert.Equal(2, visiblePlot.RemainingYield);
            }
            Assert.IsType<OkObjectResult>(await s.Steal(s.Now));
            Assert.IsType<OkObjectResult>(await s.Harvest(s.Now, cycle));
            await using var check = s.Db();
            var plot = await check.Plots.SingleAsync(x => x.Id == s.Plot);
            Assert.Null(plot.SeedId);
            Assert.Equal(0, (await check.InventoryItems.SingleAsync(x => x.Inventory.UserId == s.Visitor
                && x.ItemId == FertilizerOptions.ItemId)).Quantity);
            Assert.Single(await check.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
        }
    }

    [PostgresFact]
    public async Task ImmediateCompletionReplayAfterTheftAndHarvestDoesNotMutateLaterCycle()
    {
        foreach (var regrow in new TimeSpan?[] { null, TimeSpan.FromHours(2) })
        {
            var s = await Scenario.Create();
            var request = await s.SetCycle(TimeSpan.FromHours(4), TimeSpan.FromMinutes(58), regrow: regrow);
            var key = Guid.NewGuid();
            var result = await s.Apply(s.Visitor, key, request);
            Assert.IsType<OkObjectResult>(await s.Steal(s.Now));
            var replayAfterTheft = await s.Apply(s.Visitor, key, request);
            Assert.True(replayAfterTheft.Replayed);
            Assert.Equal(result.OperationId, replayAfterTheft.OperationId);
            Assert.IsType<OkObjectResult>(await s.Harvest(s.Now));
            var replayAfterHarvest = await s.Apply(s.Visitor, key, request);
            Assert.True(replayAfterHarvest.Replayed);
            Assert.Equal(result.ReadyAt, replayAfterHarvest.ReadyAt);
            Assert.Equal(result.PreviousReadyAt, replayAfterHarvest.PreviousReadyAt);
            await using var check = s.Db();
            var plot = await check.Plots.SingleAsync(x => x.Id == s.Plot);
            Assert.Equal(regrow == null ? null : 2, plot.CurrentHarvestCycle);
            Assert.Equal(regrow == null ? null : s.Now.Add(regrow.Value), plot.ReadyAt);
            Assert.Equal(regrow == null ? null : 2, plot.RemainingYield);
            Assert.Equal(2, await check.InventoryItems.Where(x => x.ItemType == ItemType.Crop
                && (x.Inventory.UserId == s.Owner || x.Inventory.UserId == s.Visitor)).SumAsync(x => x.Quantity));
            Assert.Equal(0, (await check.InventoryItems.SingleAsync(x => x.Inventory.UserId == s.Visitor
                && x.ItemId == FertilizerOptions.ItemId)).Quantity);
            Assert.Single(await check.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
            Assert.Empty(await s.Service(check).GetCurrentApplicationsAsync(s.Farm));
        }
    }

    [PostgresFact]
    public async Task ConcurrentApplicationTheftAndHarvestConserveStockAndOldYield()
    {
        var s = await Scenario.Create();
        var request = await s.SetCycle(TimeSpan.FromHours(4), TimeSpan.FromMinutes(58),
            regrow: TimeSpan.FromHours(2));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<FertilizePlotResponse> ApplyAfterGate()
        { await gate.Task; return await s.Apply(s.Visitor, Guid.NewGuid(), request); }
        async Task<IActionResult> StealAfterGate()
        { await gate.Task; return await s.Steal(s.Now); }
        async Task<IActionResult> HarvestAfterGate()
        { await gate.Task; return await s.Harvest(s.Now); }
        var apply = ApplyAfterGate();
        var steal = StealAfterGate();
        var harvest = HarvestAfterGate();
        gate.SetResult();
        await Task.WhenAll(apply, steal, harvest);
        Assert.Equal(s.Now, apply.Result.ReadyAt);
        Assert.True(steal.Result is OkObjectResult or BadRequestObjectResult);
        Assert.True(harvest.Result is OkObjectResult or BadRequestObjectResult or ConflictObjectResult);
        await using var check = s.Db();
        var plot = await check.Plots.SingleAsync(x => x.Id == s.Plot);
        var granted = await check.InventoryItems.Where(x => x.ItemType == ItemType.Crop
            && (x.Inventory.UserId == s.Owner || x.Inventory.UserId == s.Visitor)).SumAsync(x => x.Quantity);
        Assert.Equal(2, granted + (plot.CurrentHarvestCycle == 1 ? plot.RemainingYield!.Value : 0));
        Assert.Equal(plot.CurrentHarvestCycle == 1 ? s.Now : s.Now.AddHours(2), plot.ReadyAt);
        Assert.Equal(0, (await check.InventoryItems.SingleAsync(x => x.Inventory.UserId == s.Visitor
            && x.ItemId == FertilizerOptions.ItemId)).Quantity);
        Assert.Single(await check.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
        Assert.Single(await check.Notifications.Where(x => x.RecipientUserId == s.Owner
            && x.Type == NotificationType.PlotFertilized).ToListAsync());
        var theftSucceeded = steal.Result is OkObjectResult;
        Assert.Equal(theftSucceeded ? 45 : 0, (await check.Users.SingleAsync(x => x.Id == s.Visitor)).CurrentXp);
        Assert.Equal(theftSucceeded ? 1 : 0, await check.TheftLogs.CountAsync(x => x.PlotId == s.Plot));
    }

    [PostgresFact]
    public async Task FertilizerPreservesCareEligibilityForEarlierGrowingCycle()
    {
        var s = await Scenario.Create();
        await s.Apply(s.Visitor, Guid.NewGuid(), new FertilizePlotRequest(1, s.Now));
        await using var db = s.Db();
        var plot = await db.Plots.SingleAsync(x => x.Id == s.Plot);
        var opportunity = plot.CareOpportunityId!.Value;
        Assert.True(CropCareRules.WasGrowingAt(plot, opportunity, s.Now));
        Assert.True(CropCareRules.IsGrowing(plot, opportunity, s.Now.AddMinutes(1)));
        Assert.False(CropCareRules.IsGrowing(plot, opportunity, plot.ReadyAt!.Value));
    }

    [PostgresFact]
    public async Task ConcurrentDistinctApplicationsHaveOneWinnerAndOneDebit()
    {
        var s = await Scenario.Create();
        var request = await s.SetCycle(TimeSpan.FromHours(4), TimeSpan.FromMinutes(58));
        await using (var db = s.Db())
        {
            var inventory = await db.Inventories.SingleAsync(i => i.UserId == s.Owner);
            db.InventoryItems.Add(new InventoryItem { InventoryId = inventory.Id,
                ItemType = ItemType.AnimalProduct, ItemId = FertilizerOptions.ItemId, Quantity = 1 });
            await db.SaveChangesAsync();
        }
        async Task<(FertilizePlotResponse? Success, string? Failure)> Attempt(Guid actor)
        {
            try { return (await s.Apply(actor, Guid.NewGuid(), request), null); }
            catch (AnimalActionException exception) { return (null, exception.Code); }
        }
        var results = await Task.WhenAll(Attempt(s.Owner), Attempt(s.Visitor));
        Assert.Single(results, result => result.Success != null);
        Assert.Equal(s.Now, Assert.Single(results, result => result.Success != null).Success!.ReadyAt);
        Assert.Equal("ALREADY_READY", Assert.Single(results, result => result.Failure != null).Failure);
        await using var check = s.Db();
        Assert.Single(await check.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
        Assert.Equal(1, await check.InventoryItems.Where(i => i.ItemId == FertilizerOptions.ItemId
            && (i.Inventory.UserId == s.Owner || i.Inventory.UserId == s.Visitor)).SumAsync(i => i.Quantity));
    }

    [PostgresFact]
    public async Task ReadyPlotRejectsFertilizerWithoutConsuming()
    {
        var s = await Scenario.Create();
        var startedAt = s.Now.AddHours(-3);
        await using (var db = s.Db())
        {
            var plot = await db.Plots.SingleAsync(p => p.Id == s.Plot);
            plot.PlantedAt = startedAt;
            plot.CurrentHarvestCycleStartedAt = startedAt;
            plot.ReadyAt = s.Now.AddHours(-1);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<AnimalActionException>(() =>
            s.Apply(s.Visitor, Guid.NewGuid(), new FertilizePlotRequest(1, startedAt)));
        Assert.Equal("ALREADY_READY", error.Code);
        await using var check = s.Db();
        Assert.Equal(1, (await check.InventoryItems.SingleAsync(i => i.Inventory.UserId == s.Visitor
            && i.ItemId == FertilizerOptions.ItemId)).Quantity);
        Assert.Empty(await check.FertilizerApplications.Where(x => x.PlotId == s.Plot).ToListAsync());
    }

    [PostgresFact]
    public async Task FarmProjectionShowsFertilizationOnlyForCurrentCycle()
    {
        var s = await Scenario.Create();
        await s.Apply(s.Visitor, Guid.NewGuid(), new FertilizePlotRequest(1, s.Now));
        await using (var db = s.Db())
        {
            var states = await s.Service(db).GetCurrentApplicationsAsync(s.Farm);
            var farm = await db.Farms.Include(f => f.User).Include(f => f.Plots)
                .SingleAsync(f => f.Id == s.Farm);
            var response = FarmMapper.ToFarmResponse(farm, s.Now,
                new Dictionary<Guid, PlotCropCareState>(), fertilizedAt: states);
            Assert.Equal(s.Now, Assert.Single(response.Plots).FertilizedAt);
            var plot = Assert.Single(farm.Plots);
            plot.CurrentHarvestCycleStartedAt = s.Now.AddHours(1);
            plot.ReadyAt = s.Now.AddHours(3);
            await db.SaveChangesAsync();
        }
        await using (var check = s.Db())
            Assert.Empty(await s.Service(check).GetCurrentApplicationsAsync(s.Farm));
    }

    private sealed class Scenario
    {
        public Guid Owner { get; } = Guid.NewGuid();
        public Guid Visitor { get; } = Guid.NewGuid();
        public Guid Farm { get; } = Guid.NewGuid();
        public Guid Plot { get; } = Guid.NewGuid();
        public DateTime Now { get; } = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!).Options);
        public FertilizerService Service(AppDbContext db) => new(db, new PremiumCurrencyService(db, new FixedClock(Now)),
            Options.Create(new FertilizerOptions()), new FixedClock(Now));
        public static async Task<Scenario> Create()
        {
            var s = new Scenario();
            await using var db = s.Db();
            foreach (var id in new[] { s.Owner, s.Visitor })
            {
                db.Users.Add(new User { Id = id, Username = $"fert_{id:N}",
                    NormalizedUsername = $"FERT_{id:N}", PasswordHash = "test", Level = 7 });
                db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), UserId = id,
                    Coins = 10000, PremiumCoins = 10 });
                db.Farms.Add(new Farm { Id = id == s.Owner ? s.Farm : Guid.NewGuid(),
                    UserId = id, Name = "Fertilizer test" });
            }
            db.Seeds.Add(new Seed { Id = $"fert_seed_{Guid.NewGuid():N}", Name = "Test",
                CropId = $"fert_crop_{Guid.NewGuid():N}", CropName = "Test", Icon = "x",
                BuyPrice = 1, SellPrice = 2, GrowTime = TimeSpan.FromHours(2),
                CropAmount = 2, HarvestCycles = 1 });
            var seed = db.Seeds.Local.Single();
            db.Plots.Add(new Plot { Id = s.Plot, FarmId = s.Farm, X = 0, Y = 0,
                Unlocked = true, SeedId = seed.Id, PlantedAt = s.Now,
                CurrentHarvestCycle = 1, CurrentHarvestCycleStartedAt = s.Now,
                ReadyAt = s.Now.AddHours(2), RemainingYield = 2,
                CareOpportunityId = Guid.NewGuid() });
            var (a, b) = FriendshipService.GetCanonicalPair(s.Owner, s.Visitor);
            db.Friendships.Add(new Friendship { Id = Guid.NewGuid(), UserAId = a, UserBId = b,
                RequestedByUserId = a, Status = FriendshipStatus.Accepted, CreatedAt = s.Now });
            await db.SaveChangesAsync();
            var visitorInventory = await db.Inventories.SingleAsync(i => i.UserId == s.Visitor);
            db.InventoryItems.Add(new InventoryItem { InventoryId = visitorInventory.Id,
                ItemId = FertilizerOptions.ItemId, ItemType = ItemType.AnimalProduct, Quantity = 1 });
            await db.SaveChangesAsync();
            return s;
        }
        public async Task<BuyFertilizerResponse> Buy(Guid actor, Guid key, int quantity)
        { await using var db = Db(); return await Service(db).BuyAsync(actor, key, quantity); }
        public async Task<FertilizePlotResponse> Apply(Guid actor, Guid key, FertilizePlotRequest request)
        { await using var db = Db(); return await Service(db).ApplyAsync(actor, key, Farm, Plot, request); }
        public async Task<FertilizePlotRequest> SetCycle(TimeSpan grow, TimeSpan remaining,
            int cycle = 1, TimeSpan? regrow = null)
        {
            await using var db = Db();
            var plot = await db.Plots.SingleAsync(p => p.Id == Plot);
            var seed = await db.Seeds.SingleAsync(p => p.Id == plot.SeedId);
            seed.GrowTime = grow;
            seed.HarvestCycles = regrow == null ? 1 : 2;
            seed.RegrowTime = regrow;
            var duration = cycle == 1 ? grow : regrow!.Value;
            var startedAt = Now.Add(remaining - duration);
            plot.PlantedAt = cycle == 1 ? startedAt : startedAt.Subtract(grow);
            plot.CurrentHarvestCycleStartedAt = startedAt;
            plot.CurrentHarvestCycle = cycle;
            plot.ReadyAt = Now.Add(remaining);
            await db.SaveChangesAsync();
            return new(cycle, startedAt);
        }
        public async Task<IActionResult> Steal(DateTime now)
        {
            await using var db = Db();
            var controller = new TheftController(db, new TheftService(), new ExperienceService(),
                new FriendshipService(db), Pest(db, now));
            Authenticate(controller, Visitor);
            return await controller.Steal(Farm, Plot);
        }
        public async Task<IActionResult> Harvest(DateTime now, int expectedCycle = 1)
        {
            await using var db = Db();
            var controller = new PlotController(db, new ExperienceService(), Pest(db, now));
            Authenticate(controller, Owner);
            return await controller.Harvest(Plot, new HarvestCropRequest(expectedCycle));
        }
        private static PestService Pest(AppDbContext db, DateTime now) =>
            new(db, new ExperienceService(), Options.Create(new PestOptions()), new FixedClock(now));
        private static void Authenticate(ControllerBase controller, Guid actor)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "test");
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                { User = new ClaimsPrincipal(identity) } };
        }
    }
    private sealed class FixedClock(DateTime now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(now); }
}
