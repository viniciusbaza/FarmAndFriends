using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Scenario = FarmAndFriends.Api.Tests.Integration.AnimalEnclosureIntegrationTests.Scenario;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class CowIntegrationTests
{
    private static AnimalDto Cow(AnimalStateEnvelope state) => state.Enclosure!.Animals.Single(a => a.AnimalTypeId == "cow");
    private static AnimalDto Chicken(AnimalStateEnvelope state) => state.Enclosure!.Animals.Single(a => a.AnimalTypeId == "chicken");
    private static async Task<Scenario> Setup(AnimalEnclosureOptions? policy = null, bool chicken = true)
    {
        var s = await Scenario.Create(policy);
        await using (var db = s.Db())
        {
            (await db.Users.SingleAsync(u => u.Id == s.Owner)).Level = 5;
            await db.SaveChangesAsync();
        }
        await s.Command(s.Owner, "buy-animal-enclosure");
        if (chicken) await s.Command(s.Owner, "buy-animal", item: "chicken");
        await s.Command(s.Owner, "buy-animal", item: "cow");
        return s;
    }

    [PostgresFact]
    public async Task CowOffers_LevelAndEnclosureGatesAndConcurrentUniquePurchase()
    {
        var s = await Scenario.Create();
        await using (var db = s.Db())
        {
            var offers = await s.Service(db).GetAnimalOffersAsync(s.Owner);
            Assert.Equal(new[] { "chicken", "cow", "sheep", "pig", "peacock" }, offers.Select(o => o.AnimalTypeId));
            Assert.All(offers, o => Assert.Equal("ENCLOSURE_REQUIRED", o.Reason));
            var products = s.Service(db).GetProductOffers();
            Assert.Equal(new[] { "egg", "milk", "wool", "feather" }, products.Select(p => p.ItemId));
            Assert.Equal(40, products.Single(p => p.ItemId == "milk").SellPriceCoins);
        }
        await s.Command(s.Owner, "buy-animal-enclosure");
        Assert.Equal("LEVEL_REQUIRED", (await s.Attempt(s.Owner, "buy-animal", item: "cow")).Error);
        await using (var db = s.Db())
        {
            (await db.Users.SingleAsync(u => u.Id == s.Owner)).Level = 5;
            await db.SaveChangesAsync();
        }
        var results = await Task.WhenAll(s.Attempt(s.Owner, "buy-animal", item: "cow"), s.Attempt(s.Owner, "buy-animal", item: "cow"));
        var bought = Assert.Single(results, r => r.Response != null).Response!;
        Assert.Equal("cow", bought.Effect.AnimalTypeId);
        Assert.Equal("ALREADY_OWNED", Assert.Single(results, r => r.Error != null).Error);
        Assert.Equal("calf", Cow(bought.Current!).Stage);
        Assert.Null(Cow(bought.Current!).Cycle);
        Assert.Single(bought.Current!.Enclosure!.Animals); // Cow does not require chicken ownership.
        await using var check = s.Db();
        Assert.Equal(18000, (await check.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
        Assert.Single(await check.FarmAnimals.Where(a => a.FarmId == s.Farm).ToListAsync());
    }

    [PostgresFact]
    public async Task CowSnapshot_IsFrozenAtPurchaseAndCollectionOnlyRestartsItsOwnSpecies()
    {
        var s = await Setup();
        var young = Cow(await s.State(s.Owner));
        Assert.Equal(young.PurchasedAt.AddSeconds(28800), young.MaturesAt);
        s.Policy.CowProductionSeconds = 3600; s.Policy.CowBaseYield = 12;
        s.Clock.Now = young.MaturesAt;
        var state = await s.State(s.Owner); var cow = Cow(state); var chicken = Chicken(state);
        Assert.Equal("adult", cow.Stage);
        Assert.Equal("milk", cow.Cycle!.ProductId);
        Assert.Equal(8, cow.Cycle.BaseYield);
        Assert.Equal(cow.MaturesAt.AddSeconds(86400), cow.Cycle.ReadyAt);
        Assert.Equal(8, chicken.Cycle!.BaseYield);
        Assert.Equal(chicken.MaturesAt.AddSeconds(43200), chicken.Cycle.ReadyAt);
        s.Clock.Now = cow.Cycle.ReadyAt;
        var collected = await s.Command(s.Owner, "collect", farm: s.Farm, animal: cow.Id, cycle: cow.Cycle.Id);
        var newCow = Cow(collected.Current!); var untouched = Chicken(collected.Current!);
        Assert.Equal(12, newCow.Cycle!.BaseYield);
        Assert.Equal(s.Clock.Now.AddSeconds(3600), newCow.Cycle.ReadyAt);
        Assert.Equal(chicken.Cycle.Id, untouched.Cycle!.Id);
        Assert.Equal(chicken.Cycle.ReadyAt, untouched.Cycle.ReadyAt);
        Assert.Equal(cow.Id, newCow.Id);
        await using var db = s.Db();
        Assert.Equal(8, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "milk").SumAsync(i => i.Quantity));
        Assert.Equal(0, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "egg").SumAsync(i => i.Quantity));
    }

    [PostgresFact]
    public async Task CowLifecycle_HelpNamesProtectedMilkTheftAndReplayConserveProduction()
    {
        var s = await Setup(); s.Clock.Now = Cow(await s.State(s.Owner)).MaturesAt;
        var cow = Cow(await s.State(s.Owner)); var chicken = Chicken(await s.State(s.Owner));
        var help = await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: cow.Id, cycle: cow.Cycle!.Id);
        Assert.Equal(1, help.Effect.OwnerBonusAdded); Assert.Equal(2, help.Effect.CoinsGained); Assert.Equal(5, help.Effect.XpGained);
        var owner = Cow(await s.State(s.Owner));
        Assert.Equal(s.Visitors[0], Assert.Single(owner.Cycle!.Helpers!).UserId);
        Assert.Equal(0, Chicken(await s.State(s.Owner)).Cycle!.ProtectedHelpBonus);
        s.Clock.Now = cow.Cycle.ReadyAt;
        var thefts = await Task.WhenAll(s.Visitors.Select(visitor =>
            s.Attempt(visitor, "steal", s.Farm, cow.Id, cow.Cycle.Id)));
        Assert.Equal(4, thefts.Count(r => r.Response != null));
        Assert.Equal("THEFT_LIMIT_REACHED", Assert.Single(thefts, r => r.Error != null).Error);
        Assert.All(thefts.Where(r => r.Response != null), r => Assert.Equal("milk", r.Response!.Effect.ProductId));
        var key = Guid.NewGuid();
        var collected = await s.Command(s.Owner, "collect", key, s.Farm, cow.Id, cow.Cycle.Id);
        Assert.Equal(5, collected.Effect.Quantity); Assert.Equal("milk", collected.Effect.ProductId);
        var replay = await s.Command(s.Owner, "collect", key, s.Farm, cow.Id, cow.Cycle.Id);
        Assert.True(replay.Replayed); Assert.Equal(collected.OperationId, replay.OperationId);
        Assert.Equal(0, Cow(replay.Current!).Cycle!.ProtectedHelpBonus);
        Assert.Equal(chicken.Cycle!.Id, Chicken(replay.Current!).Cycle!.Id);
        await using (var db = s.Db())
        {
            var inventoryIds = await db.Inventories.Where(i => i.UserId == s.Owner || s.Visitors.Contains(i.UserId)).Select(i => i.Id).ToListAsync();
            Assert.Equal(9, await db.InventoryItems.Where(i => inventoryIds.Contains(i.InventoryId) && i.ItemId == "milk").SumAsync(i => i.Quantity));
            var notices = await db.Notifications.Where(n => n.AnimalCycleId == cow.Cycle.Id).ToListAsync();
            Assert.Equal(5, notices.Count);
            Assert.All(notices, n => { Assert.Contains("leite", n.Message); Assert.DoesNotContain("ovo", n.Message); });
            var helpNotice = Assert.Single(notices, n => n.Type == NotificationType.AnimalHelped);
            var helperName = (await db.Users.SingleAsync(u => u.Id == s.Visitors[0])).Username;
            Assert.Equal($"Você receberá +1 leite na próxima coleta com a ajuda de {helperName}.", helpNotice.Message);
            Assert.DoesNotContain("protegid", helpNotice.Message, StringComparison.OrdinalIgnoreCase);
            Assert.All(notices.Where(n => n.Type == NotificationType.AnimalProductStolen), n => Assert.Contains("vaca", n.Message));
        }
        var sellKey = Guid.NewGuid();
        var sold = await s.Command(s.Owner, "sell-animal-product", sellKey, item: "milk", quantity: 5);
        Assert.Equal(200, sold.Effect.CoinsGained);
        Assert.True((await s.Command(s.Owner, "sell-animal-product", sellKey, item: "milk", quantity: 5)).Replayed);
        await using var check = s.Db();
        Assert.Equal(17700, (await check.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
        Assert.Equal(0, await check.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "milk").SumAsync(i => i.Quantity));
    }

    [PostgresFact]
    public async Task Help_SameVisitorRewardsChickenAndCowIndependently()
    {
        var s = await Setup(); s.Clock.Now = Cow(await s.State(s.Owner)).MaturesAt;
        var state = await s.State(s.Owner); var cow = Cow(state); var chicken = Chicken(state);
        var results = await Task.WhenAll(s.Attempt(s.Visitors[0], "help", s.Farm, cow.Id, cow.Cycle!.Id),
            s.Attempt(s.Visitors[0], "help", s.Farm, chicken.Id, chicken.Cycle!.Id));
        Assert.All(results, r => Assert.NotNull(r.Response));
        var after = await s.State(s.Owner);
        Assert.All(after.Enclosure!.Animals, animal => Assert.Equal(1, animal.Cycle!.ProtectedHelpBonus));
        await using var db = s.Db();
        Assert.Equal(2, await db.AnimalActionCompletions.CountAsync(c => c.ActorUserId == s.Visitors[0] && c.Action == "help"));
        Assert.Equal(10, (await db.Users.SingleAsync(u => u.Id == s.Visitors[0])).CurrentXp);
        Assert.Equal(20004, (await db.Inventories.SingleAsync(i => i.UserId == s.Visitors[0])).Coins);
    }

    [PostgresFact]
    public async Task Theft_PerAnimalCycleAllowsSameVisitorAcrossSpeciesAndReopensNextCycle()
    {
        var s = await Setup(); s.Clock.Now = Cow(await s.State(s.Owner)).MaturesAt.AddSeconds(s.Policy.CowProductionSeconds);
        var state = await s.State(s.Owner); var cow = Cow(state); var chicken = Chicken(state);
        var results = await Task.WhenAll(s.Attempt(s.Visitors[0], "steal", s.Farm, cow.Id, cow.Cycle!.Id),
            s.Attempt(s.Visitors[0], "steal", s.Farm, chicken.Id, chicken.Cycle!.Id));
        Assert.All(results, r => Assert.NotNull(r.Response));
        Assert.Equal(new[] { "egg", "milk" }, results.Select(r => r.Response!.Effect.ProductId).OrderBy(id => id));

        var visitorState = await s.State(s.Visitors[0]);
        Assert.False(Cow(visitorState).Permissions.CanSteal);
        Assert.Equal("THEFT_ALREADY_REGISTERED", Cow(visitorState).Permissions.StealReason);
        Assert.False(Chicken(visitorState).Permissions.CanSteal);
        Assert.Equal("THEFT_ALREADY_REGISTERED", Chicken(visitorState).Permissions.StealReason);
        Assert.Equal("THEFT_ALREADY_REGISTERED",
            (await s.Attempt(s.Visitors[0], "steal", s.Farm, chicken.Id, chicken.Cycle.Id)).Error);

        var collected = await s.Command(s.Owner, "collect", farm: s.Farm, animal: chicken.Id, cycle: chicken.Cycle.Id);
        var nextChicken = Chicken(collected.Current!);
        s.Clock.Now = nextChicken.Cycle!.ReadyAt;
        Assert.True(Chicken(await s.State(s.Visitors[0])).Permissions.CanSteal);
        await s.Command(s.Visitors[0], "steal", farm: s.Farm, animal: nextChicken.Id, cycle: nextChicken.Cycle.Id);

        await using var db = s.Db();
        Assert.Equal(3, await db.AnimalActionCompletions.CountAsync(c => c.ActorUserId == s.Visitors[0] && c.Action == "steal"));
        Assert.Equal(2, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Visitors[0]
            && i.ItemType == ItemType.AnimalProduct && i.ItemId == "egg").SumAsync(i => i.Quantity));
        Assert.Equal(1, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Visitors[0]
            && i.ItemType == ItemType.AnimalProduct && i.ItemId == "milk").SumAsync(i => i.Quantity));
        Assert.Equal(1, (await db.AnimalProductionCycles.SingleAsync(c => c.Id == cow.Cycle.Id)).StolenBase);
        Assert.Equal(1, (await db.AnimalProductionCycles.SingleAsync(c => c.Id == chicken.Cycle.Id)).StolenBase);
        Assert.Equal(1, (await db.AnimalProductionCycles.SingleAsync(c => c.Id == nextChicken.Cycle.Id)).StolenBase);
        var notices = await db.Notifications.Where(n => n.ActorUserId == s.Visitors[0]
            && n.Type == NotificationType.AnimalProductStolen).ToListAsync();
        Assert.Equal(3, notices.Count);
        Assert.Equal(2, notices.Select(n => n.AnimalId).Distinct().Count());
    }

    [PostgresFact]
    public async Task CowCollection_ConcurrentSameKeyCreditsMilkAndXpOnce()
    {
        var s = await Setup(chicken: false);
        s.Clock.Now = Cow(await s.State(s.Owner)).MaturesAt.AddSeconds(s.Policy.CowProductionSeconds);
        var cow = Cow(await s.State(s.Owner)); var key = Guid.NewGuid();
        var results = await Task.WhenAll(s.Command(s.Owner, "collect", key, s.Farm, cow.Id, cow.Cycle!.Id),
            s.Command(s.Owner, "collect", key, s.Farm, cow.Id, cow.Cycle!.Id));
        Assert.Single(results, r => r.Replayed);
        Assert.Equal(results[0].OperationId, results[1].OperationId);
        Assert.All(results, r => Assert.NotEqual(cow.Cycle!.Id, Cow(r.Current!).Cycle!.Id));
        await using var db = s.Db();
        Assert.Equal(8, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "milk").SumAsync(i => i.Quantity));
        Assert.Equal(25, (await db.Users.SingleAsync(u => u.Id == s.Owner)).CurrentXp);
        Assert.Equal(2, await db.AnimalProductionCycles.CountAsync(c => c.FarmAnimalId == cow.Id));
    }

    [PostgresFact]
    public async Task MilkSale_LastStockConcurrentRequestsNeverDoubleCredit()
    {
        var s = await Scenario.Create();
        await using (var db = s.Db())
        {
            var inventory = await db.Inventories.SingleAsync(i => i.UserId == s.Owner);
            db.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), InventoryId = inventory.Id, ItemType = ItemType.AnimalProduct, ItemId = "milk", Quantity = 1 });
            await db.SaveChangesAsync();
        }
        var results = await Task.WhenAll(s.Attempt(s.Owner, "sell-animal-product", item: "milk", quantity: 1), s.Attempt(s.Owner, "sell-animal-product", item: "milk", quantity: 1));
        Assert.Single(results, r => r.Response != null);
        Assert.Equal("INSUFFICIENT_STOCK", Assert.Single(results, r => r.Error != null).Error);
        await using var check = s.Db();
        Assert.Equal(20040, (await check.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
        Assert.Equal(0, await check.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "milk").SumAsync(i => i.Quantity));
    }
}
