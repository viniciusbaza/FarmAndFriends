using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Scenario = FarmAndFriends.Api.Tests.Integration.AnimalEnclosureIntegrationTests.Scenario;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class SheepIntegrationTests
{
    private static AnimalDto Sheep(AnimalStateEnvelope state) => state.Enclosure!.Animals.Single(a => a.AnimalTypeId == "sheep");

    private static async Task<Scenario> Setup(AnimalEnclosureOptions? policy = null, bool chicken = false)
    {
        var s = await Scenario.Create(policy);
        await using (var db = s.Db())
        {
            (await db.Users.SingleAsync(u => u.Id == s.Owner)).Level = 6;
            await db.SaveChangesAsync();
        }
        await s.Command(s.Owner, "buy-animal-enclosure");
        if (chicken) await s.Command(s.Owner, "buy-animal", item: "chicken");
        await s.Command(s.Owner, "buy-animal", item: "sheep");
        return s;
    }

    [PostgresFact]
    public async Task SheepOffers_LevelGateAndConcurrentPurchaseDebitOnce()
    {
        var s = await Scenario.Create();
        await using (var db = s.Db())
        {
            var offers = await s.Service(db).GetAnimalOffersAsync(s.Owner);
            Assert.Equal(new[] { "chicken", "cow", "sheep", "pig" }, offers.Select(o => o.AnimalTypeId));
            var sheep = offers.Single(o => o.AnimalTypeId == "sheep");
            Assert.Equal(1500, sheep.PriceCoins);
            Assert.Equal(6, sheep.MinLevel);
            Assert.Equal(28800, sheep.GrowthDuration);
            Assert.Equal(172800, sheep.ProductionDuration);
            Assert.Equal("ENCLOSURE_REQUIRED", sheep.Reason);
            var wool = s.Service(db).GetProductOffers().Single(o => o.ItemId == "wool");
            Assert.Equal("Lã", wool.DisplayName);
            Assert.Equal(100, wool.SellPriceCoins);
        }
        await s.Command(s.Owner, "buy-animal-enclosure");
        Assert.Equal("LEVEL_REQUIRED", (await s.Attempt(s.Owner, "buy-animal", item: "sheep")).Error);
        await using (var db = s.Db())
        {
            (await db.Users.SingleAsync(u => u.Id == s.Owner)).Level = 6;
            await db.SaveChangesAsync();
        }
        var results = await Task.WhenAll(s.Attempt(s.Owner, "buy-animal", item: "sheep"),
            s.Attempt(s.Owner, "buy-animal", item: "sheep"));
        var success = Assert.Single(results, r => r.Response != null).Response!;
        Assert.Equal("ALREADY_OWNED", Assert.Single(results, r => r.Error != null).Error);
        Assert.Equal("lamb", Sheep(success.Current!).Stage);
        Assert.Null(Sheep(success.Current!).Cycle);
        await using var check = s.Db();
        Assert.Equal(17500, (await check.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
        Assert.Single(await check.FarmAnimals.Where(a => a.FarmId == s.Farm).ToListAsync());
    }

    [PostgresFact]
    public async Task SheepCycle_PurchaseSnapshotNextCycleAndOtherSpeciesRemainIndependent()
    {
        var s = await Setup(chicken: true);
        await s.Command(s.Owner, "buy-animal", item: "cow");
        var lamb = Sheep(await s.State(s.Owner));
        Assert.Equal(lamb.PurchasedAt.AddHours(8), lamb.MaturesAt);
        s.Policy.SheepProductionSeconds = 3600;
        s.Policy.SheepBaseYield = 7;
        s.Policy.SheepCollectXp = 50;
        s.Clock.Now = lamb.MaturesAt;
        var state = await s.State(s.Owner);
        var sheep = Sheep(state);
        var chicken = state.Enclosure!.Animals.Single(a => a.AnimalTypeId == "chicken");
        var cow = state.Enclosure.Animals.Single(a => a.AnimalTypeId == "cow");
        Assert.Equal("adult", sheep.Stage);
        Assert.Equal("wool", sheep.Cycle!.ProductId);
        Assert.Equal(6, sheep.Cycle.BaseYield);
        Assert.Equal(sheep.MaturesAt.AddHours(48), sheep.Cycle.ReadyAt);
        Assert.Equal(8, chicken.Cycle!.BaseYield);
        Assert.Equal(8, cow.Cycle!.BaseYield);
        s.Clock.Now = sheep.Cycle.ReadyAt;
        Assert.True(Sheep(await s.State(s.Owner)).Cycle!.IsReady);
        var collected = await s.Command(s.Owner, "collect", farm: s.Farm, animal: sheep.Id, cycle: sheep.Cycle.Id);
        Assert.Equal(6, collected.Effect.Quantity);
        Assert.Equal(40, collected.Effect.XpGained);
        Assert.Equal(7, Sheep(collected.Current!).Cycle!.BaseYield);
        Assert.False(Sheep(collected.Current!).Cycle!.IsReady);
        Assert.Equal(s.Clock.Now.AddHours(1), Sheep(collected.Current!).Cycle!.ReadyAt);
        Assert.Equal(chicken.Cycle.Id, collected.Current!.Enclosure!.Animals.Single(a => a.AnimalTypeId == "chicken").Cycle!.Id);
        Assert.Equal(cow.Cycle.Id, collected.Current.Enclosure.Animals.Single(a => a.AnimalTypeId == "cow").Cycle!.Id);
        await using var db = s.Db();
        Assert.Equal(6, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "wool").SumAsync(i => i.Quantity));
    }

    [PostgresFact]
    public async Task Wool_HelpTheftReserveReplayAndSaleConserveProduction()
    {
        var s = await Setup();
        s.Clock.Now = Sheep(await s.State(s.Owner)).MaturesAt;
        var sheep = Sheep(await s.State(s.Owner));
        var help = await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: sheep.Id, cycle: sheep.Cycle!.Id);
        Assert.Equal(1, help.Effect.OwnerBonusAdded);
        Assert.Equal(2, help.Effect.CoinsGained);
        Assert.Equal(5, help.Effect.XpGained);
        s.Clock.Now = sheep.Cycle.ReadyAt;
        var thefts = await Task.WhenAll(s.Visitors.Take(3).Select(v => s.Attempt(v, "steal", s.Farm, sheep.Id, sheep.Cycle.Id)));
        Assert.Equal(2, thefts.Count(r => r.Response != null));
        Assert.Equal("THEFT_LIMIT_REACHED", Assert.Single(thefts, r => r.Error != null).Error);
        Assert.All(thefts.Where(r => r.Response != null), r => Assert.Equal("wool", r.Response!.Effect.ProductId));
        var key = Guid.NewGuid();
        var collected = await s.Command(s.Owner, "collect", key, s.Farm, sheep.Id, sheep.Cycle.Id);
        Assert.Equal(5, collected.Effect.Quantity); // Four reserved plus one protected help bonus.
        Assert.Equal("wool", collected.Effect.ProductId);
        Assert.Equal(40, collected.Effect.XpGained);
        var replay = await s.Command(s.Owner, "collect", key, s.Farm, sheep.Id, sheep.Cycle.Id);
        Assert.True(replay.Replayed);
        Assert.Equal(collected.OperationId, replay.OperationId);
        await using (var db = s.Db())
        {
            Assert.Equal(5, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "wool").SumAsync(i => i.Quantity));
            Assert.Equal(2, await db.InventoryItems.Where(i => s.Visitors.Contains(i.Inventory.UserId) && i.ItemId == "wool").SumAsync(i => i.Quantity));
            var notices = await db.Notifications.Where(n => n.AnimalCycleId == sheep.Cycle.Id).ToListAsync();
            Assert.Equal(3, notices.Count);
            Assert.All(notices, n => Assert.Contains("lã", n.Message));
            var helpNotice = Assert.Single(notices, n => n.Type == NotificationType.AnimalHelped);
            var helperName = (await db.Users.SingleAsync(u => u.Id == s.Visitors[0])).Username;
            Assert.Equal($"Você receberá +1 lã na próxima coleta com a ajuda de {helperName}.", helpNotice.Message);
            Assert.All(notices.Where(n => n.Type == NotificationType.AnimalProductStolen), n => Assert.Contains("ovelha", n.Message));
        }
        var sold = await s.Command(s.Owner, "sell-animal-product", item: "wool", quantity: 5);
        Assert.Equal(500, sold.Effect.CoinsGained);
        await using var check = s.Db();
        Assert.Equal(18000, (await check.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
        Assert.Equal(0, await check.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "wool").SumAsync(i => i.Quantity));
    }

    [PostgresFact]
    public async Task SheepCollection_ConcurrentReplayCreditsWoolAndXpOnce()
    {
        var s = await Setup();
        s.Clock.Now = Sheep(await s.State(s.Owner)).MaturesAt.AddHours(48);
        var sheep = Sheep(await s.State(s.Owner));
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(s.Command(s.Owner, "collect", key, s.Farm, sheep.Id, sheep.Cycle!.Id),
            s.Command(s.Owner, "collect", key, s.Farm, sheep.Id, sheep.Cycle!.Id));
        Assert.Single(results, r => r.Replayed);
        Assert.Equal(results[0].OperationId, results[1].OperationId);
        await using var db = s.Db();
        Assert.Equal(6, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "wool").SumAsync(i => i.Quantity));
        Assert.Equal(40, (await db.Users.SingleAsync(u => u.Id == s.Owner)).CurrentXp);
        Assert.Equal(2, await db.AnimalProductionCycles.CountAsync(c => c.FarmAnimalId == sheep.Id));
    }

    [PostgresFact]
    public async Task SheepHelp_IsIndependentFromChickenCycle()
    {
        var s = await Setup(chicken: true);
        s.Clock.Now = Sheep(await s.State(s.Owner)).MaturesAt;
        var state = await s.State(s.Owner);
        var sheep = Sheep(state);
        var chicken = state.Enclosure!.Animals.Single(a => a.AnimalTypeId == "chicken");
        var results = await Task.WhenAll(s.Attempt(s.Visitors[0], "help", s.Farm, sheep.Id, sheep.Cycle!.Id),
            s.Attempt(s.Visitors[0], "help", s.Farm, chicken.Id, chicken.Cycle!.Id));
        Assert.All(results, r => Assert.NotNull(r.Response));
        var after = await s.State(s.Owner);
        Assert.Equal(2, after.Enclosure!.Animals.Sum(a => a.Cycle!.ProtectedHelpBonus));
        await using var db = s.Db();
        Assert.Equal(2, await db.AnimalActionCompletions.CountAsync(c => c.ActorUserId == s.Visitors[0] && c.Action == "help"));
        Assert.Equal(10, (await db.Users.SingleAsync(u => u.Id == s.Visitors[0])).CurrentXp);
    }
}
