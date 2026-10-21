using System.Data;
using System.Data.Common;
using System.Security.Claims;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class AnimalEnclosureIntegrationTests
{
    [PostgresFact]
    public async Task Purchases_ConcurrentUniqueOwnershipAndReplayDebitOnce()
    {
        var s = await Scenario.Create();
        var key = Guid.NewGuid();
        var purchase = await s.Command(s.Owner, "buy-animal-enclosure", key);
        var replay = await s.Command(s.Owner, "buy-animal-enclosure", key);
        Assert.True(replay.Replayed); Assert.Equal(purchase.OperationId, replay.OperationId);
        Assert.Equal(purchase.Current!.Revision, replay.Current!.Revision);
        var animals = await Task.WhenAll(s.Attempt(s.Owner, "buy-animal", item: "chicken"), s.Attempt(s.Owner, "buy-animal", item: "chicken"));
        Assert.Single(animals, a => a.Response != null);
        Assert.Equal("ALREADY_OWNED", Assert.Single(animals, a => a.Error != null).Error);
        await using var db = s.Db();
        Assert.Equal(18500, (await db.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
        Assert.Single(await db.FarmAnimals.Where(a => a.FarmId == s.Farm).ToListAsync());
        Assert.Equal(2, await db.AnimalActionCompletions.CountAsync(c => c.ActorUserId == s.Owner));
    }

    [PostgresFact]
    public async Task PurchaseGate_RequiresValidFirstExpansionNotTwentyEightUnlocked()
    {
        var s = await Scenario.Create();
        await using (var db = s.Db())
        {
            var plots = await db.Plots.Where(p => p.FarmId == s.Farm).ToListAsync();
            db.Plots.RemoveRange(plots.Where(p => p.X > 2 || p.Y > 2));
            plots.Single(p => p.X == 2 && p.Y == 2).Unlocked = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal("FIRST_EXPANSION_REQUIRED", (await s.Attempt(s.Owner, "buy-animal-enclosure")).Error);
        await using (var db = s.Db())
        {
            (await db.Plots.SingleAsync(p => p.FarmId == s.Farm && p.X == 2 && p.Y == 2)).Unlocked = true;
            await db.SaveChangesAsync();
        }
        Assert.Equal("UNSUPPORTED_FARM_LAYOUT", (await s.Attempt(s.Owner, "buy-animal-enclosure")).Error);
        await using (var db = s.Db())
        {
            foreach (var d in FarmLayoutRules.ExpansionPlots()) db.Plots.Add(new Plot { Id = Guid.NewGuid(), FarmId = s.Farm, X = d.X, Y = d.Y });
            await db.SaveChangesAsync();
        }
        var results = await Task.WhenAll(s.Attempt(s.Owner, "buy-animal-enclosure"), s.Attempt(s.Owner, "buy-animal-enclosure"));
        Assert.Single(results, r => r.Response != null);
        await using var check = s.Db();
        Assert.Equal(9, await check.Plots.CountAsync(p => p.FarmId == s.Farm && p.Unlocked));
        Assert.Equal(19000, (await check.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
    }

    [PostgresFact]
    public async Task Maturity_FreezesInitialPolicyAndMaterializesOnlyOneHistoricalCycle()
    {
        var s = await Scenario.Create();
        await s.Buy();
        var chick = Assert.Single((await s.State(s.Owner)).Enclosure!.Animals);
        Assert.Equal("chick", chick.Stage); Assert.Null(chick.Cycle);
        var originalDuration = s.Policy.ProductionSeconds;
        s.Policy.ProductionSeconds *= 2; s.Policy.BaseYield = 100;
        s.Clock.Now = chick.MaturesAt.AddDays(30);
        var reads = await Task.WhenAll(s.State(s.Owner), s.State(s.Visitors[0]));
        var animal = Assert.Single(reads[0].Enclosure!.Animals);
        Assert.Equal(chick.MaturesAt, animal.Cycle!.StartsAt);
        Assert.Equal(chick.MaturesAt.AddSeconds(originalDuration), animal.Cycle.ReadyAt);
        Assert.Equal(8, animal.Cycle.BaseYield); Assert.True(animal.Cycle.IsReady);
        await using var db = s.Db();
        Assert.Equal(1, await db.AnimalProductionCycles.CountAsync(c => c.FarmAnimalId == animal.Id));
        Assert.Equal(0, await db.InventoryItems.CountAsync(i => i.ItemType == ItemType.AnimalProduct && i.Inventory.UserId == s.Owner));
        var collect = await s.Command(s.Owner, "collect", farm: s.Farm, animal: animal.Id, cycle: animal.Cycle.Id);
        Assert.Equal(8, collect.Effect.Quantity);
        Assert.Equal(100, collect.Current!.Enclosure!.Animals[0].Cycle!.BaseYield);
        Assert.Equal(s.Clock.Now, collect.Current.Enclosure.Animals[0].Cycle!.StartsAt);
    }

    [PostgresFact]
    public async Task Help_LastBonusRacePaysPartialBonusFullPackageAndLoserHasNoEffects()
    {
        var s = await Scenario.Create(new() { HelpIncrement = 2, HelpBonusLimit = 3 });
        var animal = await s.Producing();
        await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: animal.Id, cycle: animal.Cycle!.Id);
        var results = await Task.WhenAll(s.Attempt(s.Visitors[1], "help", s.Farm, animal.Id, animal.Cycle.Id),
            s.Attempt(s.Visitors[2], "help", s.Farm, animal.Id, animal.Cycle.Id));
        var winner = Assert.Single(results, r => r.Response != null).Response!;
        Assert.Equal(1, winner.Effect.OwnerBonusAdded); Assert.Equal(2, winner.Effect.CoinsGained); Assert.Equal(5, winner.Effect.XpGained);
        Assert.Equal("OWNER_BONUS_LIMIT_REACHED", Assert.Single(results, r => r.Error != null).Error);
        await using var db = s.Db();
        Assert.Equal(2, await db.AnimalActionCompletions.CountAsync(c => c.CycleId == animal.Cycle.Id && c.Action == "help"));
        Assert.Equal(2, await db.Notifications.CountAsync(n => n.AnimalCycleId == animal.Cycle.Id));
        Assert.Equal(3, (await db.AnimalProductionCycles.SingleAsync(c => c.Id == animal.Cycle.Id)).ProtectedHelpBonus);
        Assert.Equal(10, await db.Users.Where(u => s.Visitors.Contains(u.Id)).SumAsync(u => u.CurrentXp));
        var visitorState = await s.State(s.Visitors[2]);
        Assert.Null(visitorState.Enclosure!.Animals[0].Cycle!.Helpers);
        var ownerState = await s.State(s.Owner);
        Assert.Equal(2, ownerState.Enclosure!.Animals[0].Cycle!.HelperCount);
        Assert.Equal(2, ownerState.Enclosure.Animals[0].Cycle!.Helpers!.Count);
    }

    [PostgresFact]
    public async Task Help_SameVisitorConcurrentDifferentKeysRewardsCycleOnce()
    {
        var s = await Scenario.Create();
        var animal = await s.Producing();
        var helper = s.Visitors[0];

        var results = await Task.WhenAll(
            s.Attempt(helper, "help", s.Farm, animal.Id, animal.Cycle!.Id),
            s.Attempt(helper, "help", s.Farm, animal.Id, animal.Cycle.Id));

        Assert.Single(results, result => result.Response != null);
        Assert.Equal("HELP_ALREADY_REGISTERED", Assert.Single(results, result => result.Error != null).Error);
        await using var db = s.Db();
        Assert.Equal(1, await db.AnimalActionCompletions.CountAsync(completion => completion.ActorUserId == helper
            && completion.CycleId == animal.Cycle.Id && completion.Action == "help"));
        var helpNotice = await db.Notifications.SingleAsync(notification => notification.ActorUserId == helper
            && notification.AnimalCycleId == animal.Cycle.Id && notification.Type == NotificationType.AnimalHelped);
        var helperName = (await db.Users.SingleAsync(user => user.Id == helper)).Username;
        Assert.Equal($"Você receberá +1 ovo na próxima coleta com a ajuda de {helperName}.", helpNotice.Message);
        Assert.Equal(1, (await db.AnimalProductionCycles.SingleAsync(cycle => cycle.Id == animal.Cycle.Id)).ProtectedHelpBonus);
        Assert.Equal(5, (await db.Users.SingleAsync(user => user.Id == helper)).CurrentXp);
        Assert.Equal(20002, (await db.Inventories.SingleAsync(inventory => inventory.UserId == helper)).Coins);
    }

    [PostgresFact]
    public async Task Help_AcrossFarmsRewardsEachAnimalCycleIndependently()
    {
        var policy = new AnimalEnclosureOptions();
        var s = await Scenario.Create(policy);
        var other = await Scenario.Create(policy, s.Clock);
        await s.Befriend(s.Visitors[0], other.Owner);
        var a = await s.Producing(); var b = await other.Producing();
        var results = await Task.WhenAll(s.Attempt(s.Visitors[0], "help", s.Farm, a.Id, a.Cycle!.Id),
            s.Attempt(s.Visitors[0], "help", other.Farm, b.Id, b.Cycle!.Id));
        Assert.All(results, r => Assert.NotNull(r.Response));
        await using var db = s.Db();
        Assert.Equal(2, await db.AnimalActionCompletions.CountAsync(c => c.ActorUserId == s.Visitors[0] && c.Action == "help"));
        Assert.Equal(10, (await db.Users.SingleAsync(u => u.Id == s.Visitors[0])).CurrentXp);
        Assert.Equal(20004, (await db.Inventories.SingleAsync(i => i.UserId == s.Visitors[0])).Coins);
        var states = await Task.WhenAll(s.State(s.Visitors[0]), other.State(s.Visitors[0]));
        Assert.All(states, state =>
        {
            var animal = Assert.Single(state.Enclosure!.Animals);
            Assert.True(animal.Cycle!.ViewerHelped);
            Assert.Equal(0, animal.Cycle.BaseYield);
            Assert.Equal(0, animal.Cycle.ProtectedHelpBonus);
            Assert.Equal(0, animal.Cycle.OwnerCollectible);
            Assert.False(animal.Permissions.CanHelp);
            Assert.False(animal.Permissions.RewardAvailable);
            Assert.Equal("HELP_ALREADY_REGISTERED", animal.Permissions.HelpReason);
            Assert.Null(animal.Permissions.NextRewardAt);
        });
    }

    [PostgresFact]
    public async Task Lifecycle_TheftConservationReplaySaleAndNamesSurviveCollection()
    {
        var s = await Scenario.Create();
        var a = await s.Producing(); var helpKey = Guid.NewGuid();
        var help = await s.Command(s.Visitors[0], "help", helpKey, s.Farm, a.Id, a.Cycle!.Id);
        Assert.Equal("HELP_ALREADY_REGISTERED", (await s.Attempt(s.Visitors[0], "help", s.Farm, a.Id, a.Cycle.Id)).Error);
        s.Clock.Now = a.Cycle.ReadyAt;
        var thefts = await Task.WhenAll(s.Visitors.Select(visitor =>
            s.Attempt(visitor, "steal", s.Farm, a.Id, a.Cycle.Id)));
        Assert.Equal(4, thefts.Count(t => t.Response != null));
        Assert.Equal("THEFT_LIMIT_REACHED", Assert.Single(thefts, t => t.Error != null).Error);

        var successfulVisitor = s.Visitors[Array.FindIndex(thefts, result => result.Response != null)];
        var exhaustedVisitor = s.Visitors[Array.FindIndex(thefts, result => result.Error != null)];

        var ownerReady = Assert.Single((await s.State(s.Owner)).Enclosure!.Animals);
        Assert.Equal(8, ownerReady.Cycle!.BaseYield);
        Assert.Equal(1, ownerReady.Cycle.ProtectedHelpBonus);
        Assert.Equal(4, ownerReady.Cycle!.RemainingBase);
        Assert.Equal(5, ownerReady.Cycle.OwnerCollectible);

        var participatingVisitorReady = Assert.Single((await s.State(successfulVisitor)).Enclosure!.Animals);
        Assert.Equal(0, participatingVisitorReady.Cycle!.BaseYield);
        Assert.Equal(0, participatingVisitorReady.Cycle.ProtectedHelpBonus);
        Assert.Equal(4, participatingVisitorReady.Cycle!.RemainingBase);
        Assert.Equal(0, participatingVisitorReady.Cycle.OwnerCollectible);
        Assert.False(participatingVisitorReady.Permissions.CanSteal);
        Assert.Equal("THEFT_ALREADY_REGISTERED", participatingVisitorReady.Permissions.StealReason);

        var exhaustedVisitorReady = Assert.Single((await s.State(exhaustedVisitor)).Enclosure!.Animals);
        Assert.Equal(0, exhaustedVisitorReady.Cycle!.BaseYield);
        Assert.Equal(0, exhaustedVisitorReady.Cycle.ProtectedHelpBonus);
        Assert.Equal(4, exhaustedVisitorReady.Cycle!.RemainingBase);
        Assert.Equal(0, exhaustedVisitorReady.Cycle.OwnerCollectible);
        Assert.False(exhaustedVisitorReady.Permissions.CanSteal);
        Assert.Equal("THEFT_LIMIT_REACHED", exhaustedVisitorReady.Permissions.StealReason);

        var exhausted = await Assert.ThrowsAsync<AnimalActionException>(() =>
            s.Command(exhaustedVisitor, "steal", farm: s.Farm, animal: a.Id, cycle: a.Cycle.Id));
        Assert.Equal("A parte disponível para roubo já acabou.", exhausted.Message);
        var key = Guid.NewGuid();
        var collect = await s.Command(s.Owner, "collect", key, s.Farm, a.Id, a.Cycle.Id);
        Assert.Equal(5, collect.Effect.Quantity);
        var replay = await s.Command(s.Owner, "collect", key, s.Farm, a.Id, a.Cycle.Id);
        Assert.True(replay.Replayed); Assert.Equal(collect.OperationId, replay.OperationId);
        Assert.NotEqual(a.Cycle.Id, replay.Current!.Enclosure!.Animals[0].Cycle!.Id);
        var helpReplay = await s.Command(s.Visitors[0], "help", helpKey, s.Farm, a.Id, a.Cycle.Id);
        Assert.True(helpReplay.Replayed); Assert.Equal(help.OperationId, helpReplay.OperationId);
        await using (var db = s.Db())
        {
            var inventoryIds = await db.Inventories.Where(i => i.UserId == s.Owner || s.Visitors.Contains(i.UserId)).Select(i => i.Id).ToListAsync();
            Assert.Equal(9, await db.InventoryItems.Where(i => inventoryIds.Contains(i.InventoryId) && i.ItemId == "egg").SumAsync(i => i.Quantity));
            var page = await s.Service(db).GetHelpersAsync(s.Owner, s.Farm, a.Id, a.Cycle.Id, null, 1);
            Assert.Single(page.Items); Assert.Equal(s.Visitors[0], page.Items[0].UserId);
        }
        var sellKey = Guid.NewGuid();
        var sold = await s.Command(s.Owner, "sell-animal-product", sellKey, item: "egg", quantity: 5);
        Assert.Equal(100, sold.Effect.CoinsGained); Assert.Equal(0, sold.Effect.XpGained);
        var soldReplay = await s.Command(s.Owner, "sell-animal-product", sellKey, item: "egg", quantity: 5);
        Assert.True(soldReplay.Replayed);
        var mismatch = await Assert.ThrowsAsync<AnimalActionException>(() => s.Command(s.Owner, "sell-animal-product", sellKey, item: "egg", quantity: 6));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", mismatch.Code);
        await using var check = s.Db();
        Assert.Equal(18600, (await check.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
        Assert.Equal(25, (await check.Users.SingleAsync(u => u.Id == s.Owner)).CurrentXp);
    }

    [PostgresFact]
    public async Task FailureAndPrivacy_NoRewardsFromWrongTargetsAndOverflowRollsBackAllHelpEffects()
    {
        var s = await Scenario.Create(); var a = await s.Producing();
        Assert.Equal("SELF_ACTION", (await s.Attempt(s.Owner, "help", s.Farm, a.Id, a.Cycle!.Id)).Error);
        Assert.Equal("NOT_OWNER", (await s.Attempt(s.Visitors[0], "collect", s.Farm, a.Id, a.Cycle.Id)).Error);
        Assert.Equal("RESOURCE_NOT_FOUND", (await s.Attempt(s.Visitors[0], "help", s.Farm, Guid.NewGuid(), a.Cycle.Id)).Error);
        Assert.Equal("CYCLE_CHANGED", (await s.Attempt(s.Visitors[0], "help", s.Farm, a.Id, Guid.NewGuid())).Error);
        await using (var db = s.Db())
        {
            (await db.Inventories.SingleAsync(i => i.UserId == s.Visitors[0])).Coins = int.MaxValue;
            await db.SaveChangesAsync();
        }
        Assert.Equal("BALANCE_LIMIT", (await s.Attempt(s.Visitors[0], "help", s.Farm, a.Id, a.Cycle.Id)).Error);
        await using (var db = s.Db())
        {
            Assert.Equal(0, await db.AnimalActionCompletions.CountAsync(c => c.CycleId == a.Cycle.Id));
            Assert.Equal(0, await db.Notifications.CountAsync(n => n.AnimalCycleId == a.Cycle.Id));
            Assert.Equal(0, (await db.AnimalProductionCycles.SingleAsync(c => c.Id == a.Cycle.Id)).ProtectedHelpBonus);
            Assert.Equal(0, (await db.Users.SingleAsync(u => u.Id == s.Visitors[0])).CurrentXp);
            var forbidden = await Assert.ThrowsAsync<AnimalActionException>(() => s.Service(db).GetHelpersAsync(s.Visitors[0], s.Farm, a.Id, a.Cycle.Id, null, 10));
            Assert.Equal("NOT_OWNER", forbidden.Code);
        }
    }

    [PostgresFact]
    public async Task Replay_AfterFriendshipRemovalReturnsOnlyOwnReceipt()
    {
        var s = await Scenario.Create(); var a = await s.Producing(); var key = Guid.NewGuid();
        var paid = await s.Command(s.Visitors[0], "help", key, s.Farm, a.Id, a.Cycle!.Id);
        await using (var db = s.Db())
        {
            var (x, y) = FriendshipService.GetCanonicalPair(s.Owner, s.Visitors[0]);
            db.Friendships.Remove(await db.Friendships.SingleAsync(f => f.UserAId == x && f.UserBId == y));
            await db.SaveChangesAsync();
        }
        var replay = await s.Command(s.Visitors[0], "help", key, s.Farm, a.Id, a.Cycle.Id);
        Assert.True(replay.Replayed); Assert.Equal(paid.Effect, replay.Effect); Assert.Null(replay.Current);
        Assert.Equal("NOT_FRIENDS", (await s.Attempt(s.Visitors[0], "help", s.Farm, a.Id, a.Cycle.Id)).Error);
    }

    [PostgresFact]
    public async Task FarmRead_UsesOneRepeatableReadSnapshotThenStartsAnimalTransaction()
    {
        var s = await Scenario.Create();
        await using (var setup = s.Db())
        {
            var plot = await setup.Plots.FirstAsync(candidate =>
                candidate.FarmId == s.Farm && candidate.Unlocked);
            plot.SeedId = "carrot";
            plot.PlantedAt = s.Clock.Now.AddMinutes(-1);
            plot.CurrentHarvestCycle = 1;
            plot.CurrentHarvestCycleStartedAt = plot.PlantedAt;
            plot.ReadyAt = s.Clock.Now.AddHours(1);
            plot.RemainingYield = 8;
            plot.CareOpportunityId = Guid.NewGuid();
            await setup.SaveChangesAsync();
        }
        var probe = new FarmReadTransactionProbe();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!)
            .AddInterceptors(probe)
            .Options;
        await using var db = new AppDbContext(options);
        var premium = new PremiumCurrencyService(db, s.Clock);
        var controller = new FarmsController(
            db,
            new FriendshipService(db),
            new CropCareService(db, new ExperienceService(), Options.Create(new CropCareOptions()),
                NullLogger<CropCareService>.Instance, s.Clock),
            new PestService(db, new ExperienceService(), Options.Create(new PestOptions { Enabled = false }), s.Clock),
            new LandExpansionService(db, Options.Create(new LandExpansionOptions()), s.Clock,
                NullLogger<LandExpansionService>.Instance, premium),
            s.Service(db),
            new FertilizerService(db, premium, Options.Create(new FertilizerOptions()), s.Clock))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, s.Owner.ToString())],
                        "test"))
                }
            }
        };

        Assert.IsType<OkObjectResult>(await controller.GetMyFarm(CancellationToken.None));

        Assert.NotNull(probe.FarmSnapshotTransaction);
        Assert.Same(probe.FarmSnapshotTransaction, probe.CareTransaction);
        Assert.Same(probe.FarmSnapshotTransaction, probe.FertilizerTransaction);
        Assert.Equal(IsolationLevel.RepeatableRead, probe.FarmSnapshotIsolationLevel);
        Assert.NotNull(probe.AnimalTransaction);
        Assert.NotSame(probe.FarmSnapshotTransaction, probe.AnimalTransaction);
    }

    private sealed class FarmReadTransactionProbe : DbCommandInterceptor
    {
        public DbTransaction? FarmSnapshotTransaction { get; private set; }
        public IsolationLevel? FarmSnapshotIsolationLevel { get; private set; }
        public DbTransaction? CareTransaction { get; private set; }
        public DbTransaction? FertilizerTransaction { get; private set; }
        public DbTransaction? AnimalTransaction { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            var transaction = eventData.Context?.Database.CurrentTransaction?.GetDbTransaction();
            if (command.CommandText.Contains("FROM \"Farms\"", StringComparison.Ordinal)
                && command.CommandText.Contains("JOIN \"Plots\"", StringComparison.Ordinal))
            {
                FarmSnapshotTransaction = transaction;
                FarmSnapshotIsolationLevel = transaction?.IsolationLevel;
            }
            else if (command.CommandText.Contains("FROM \"CropCareCompletions\"", StringComparison.Ordinal))
                CareTransaction = transaction;
            else if (command.CommandText.Contains("FROM \"FertilizerApplications\"", StringComparison.Ordinal))
                FertilizerTransaction = transaction;
            else if (command.CommandText.Contains("FROM \"AnimalEnclosures\"", StringComparison.Ordinal))
                AnimalTransaction = transaction;

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    internal sealed class TestClock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    internal sealed class Scenario
    {
        public Guid Owner { get; } = Guid.NewGuid();
        public Guid Farm { get; } = Guid.NewGuid();
        public Guid[] Visitors { get; } =
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        public AnimalEnclosureOptions Policy { get; private init; } = new();
        public TestClock Clock { get; private init; } = new();
        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!).Options);
        public AnimalEnclosureService Service(AppDbContext db) => new(db, Options.Create(Policy), Clock, new ExperienceService());
        public static async Task<Scenario> Create(AnimalEnclosureOptions? policy = null, TestClock? clock = null)
        {
            var s = new Scenario { Policy = policy ?? new(), Clock = clock ?? new() };
            await using var db = s.Db();
            foreach (var id in s.Visitors.Prepend(s.Owner))
            {
                db.Users.Add(new User { Id = id, Username = $"animal_{id:N}", NormalizedUsername = $"ANIMAL_{id:N}", PasswordHash = "test", Level = 4 });
                db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), UserId = id, Coins = 20000 });
                db.Farms.Add(new Farm { Id = id == s.Owner ? s.Farm : Guid.NewGuid(), UserId = id, Name = "Animal test farm" });
            }
            foreach (var d in Enumerable.Range(1, 28).Select(FarmLayoutRules.GetDefinition))
                db.Plots.Add(new Plot { Id = Guid.NewGuid(), FarmId = s.Farm, X = d.X, Y = d.Y, Unlocked = d.PlotNumber <= 9 });
            await db.SaveChangesAsync();
            foreach (var visitor in s.Visitors) await s.Befriend(s.Owner, visitor);
            return s;
        }
        public async Task Befriend(Guid a, Guid b)
        {
            await using var db = Db(); var (x, y) = FriendshipService.GetCanonicalPair(a, b);
            db.Friendships.Add(new Friendship { Id = Guid.NewGuid(), UserAId = x, UserBId = y, RequestedByUserId = x, Status = FriendshipStatus.Accepted, CreatedAt = Clock.Now });
            await db.SaveChangesAsync();
        }
        public async Task<AnimalOperationResponse> Command(Guid actor, string action, Guid? key = null,
            Guid? farm = null, Guid? animal = null, Guid? cycle = null, string? item = null, int quantity = 0)
        {
            await using var db = Db(); return await Service(db).ExecuteAsync(actor, action, key ?? Guid.NewGuid(), farm, animal, cycle, item, quantity);
        }
        public async Task<(AnimalOperationResponse? Response, string? Error)> Attempt(Guid actor, string action,
            Guid? farm = null, Guid? animal = null, Guid? cycle = null, string? item = null, int quantity = 0)
        {
            try { return (await Command(actor, action, farm: farm, animal: animal, cycle: cycle, item: item, quantity: quantity), null); }
            catch (AnimalActionException e) { return (null, e.Code); }
        }
        public async Task<AnimalStateEnvelope> State(Guid actor)
        { await using var db = Db(); return await Service(db).GetStateAsync(actor, Farm); }
        public async Task Buy()
        { await Command(Owner, "buy-animal-enclosure"); await Command(Owner, "buy-animal", item: "chicken"); }
        public async Task<AnimalDto> Producing()
        {
            await Buy(); Clock.Now = (await State(Owner)).Enclosure!.Animals[0].MaturesAt;
            return (await State(Owner)).Enclosure!.Animals[0];
        }
    }
}
