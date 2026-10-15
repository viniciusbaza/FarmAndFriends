using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Scenario = FarmAndFriends.Api.Tests.Integration.AnimalEnclosureIntegrationTests.Scenario;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class PeacockIntegrationTests
{
    private static AnimalDto Peacock(AnimalStateEnvelope state) =>
        state.Enclosure!.Animals.Single(animal => animal.AnimalTypeId == "peacock");

    private static async Task<Scenario> Setup(AnimalEnclosureOptions? policy = null, bool allSpecies = false)
    {
        var scenario = await Scenario.Create(policy);
        await using (var db = scenario.Db())
        {
            (await db.Users.SingleAsync(user => user.Id == scenario.Owner)).Level = 8;
            await db.SaveChangesAsync();
        }
        await scenario.Command(scenario.Owner, "buy-animal-enclosure");
        if (allSpecies)
            foreach (var species in new[] { "chicken", "cow", "sheep", "pig" })
                await scenario.Command(scenario.Owner, "buy-animal", item: species);
        await scenario.Command(scenario.Owner, "buy-animal", item: "peacock");
        return scenario;
    }

    [PostgresFact]
    public async Task PeacockOffers_EnclosureLevelFundsAndConcurrentPurchaseDebitOnce()
    {
        var s = await Scenario.Create();
        await using (var db = s.Db())
        {
            var offer = (await s.Service(db).GetAnimalOffersAsync(s.Owner)).Single(item => item.AnimalTypeId == "peacock");
            Assert.Equal("Pavão", offer.DisplayName);
            Assert.Equal(2500, offer.PriceCoins);
            Assert.Equal(8, offer.MinLevel);
            Assert.Equal(28800, offer.GrowthDuration);
            Assert.Equal(259200, offer.ProductionDuration);
            Assert.Equal("ENCLOSURE_REQUIRED", offer.Reason);
            var feathers = s.Service(db).GetProductOffers().Single(item => item.ItemId == "feather");
            Assert.Equal("Penas", feathers.DisplayName);
            Assert.Equal(150, feathers.SellPriceCoins);
        }
        Assert.Equal("ENCLOSURE_REQUIRED", (await s.Attempt(s.Owner, "buy-animal", item: "peacock")).Error);
        await s.Command(s.Owner, "buy-animal-enclosure");
        Assert.Equal("LEVEL_REQUIRED", (await s.Attempt(s.Owner, "buy-animal", item: "peacock")).Error);
        await using (var db = s.Db())
        {
            (await db.Users.SingleAsync(user => user.Id == s.Owner)).Level = 8;
            (await db.Inventories.SingleAsync(inventory => inventory.UserId == s.Owner)).Coins = 2499;
            await db.SaveChangesAsync();
        }
        Assert.Equal("INSUFFICIENT_FUNDS", (await s.Attempt(s.Owner, "buy-animal", item: "peacock")).Error);
        await using (var db = s.Db())
        {
            Assert.Empty(await db.FarmAnimals.Where(animal => animal.FarmId == s.Farm).ToListAsync());
            (await db.Inventories.SingleAsync(inventory => inventory.UserId == s.Owner)).Coins = 19000;
            await db.SaveChangesAsync();
        }
        var attempts = await Task.WhenAll(s.Attempt(s.Owner, "buy-animal", item: "peacock"),
            s.Attempt(s.Owner, "buy-animal", item: "peacock"));
        var bought = Assert.Single(attempts, attempt => attempt.Response != null).Response!;
        Assert.Equal("ALREADY_OWNED", Assert.Single(attempts, attempt => attempt.Error != null).Error);
        Assert.Equal("peachick", Peacock(bought.Current!).Stage);
        Assert.Null(Peacock(bought.Current!).Cycle);
        Assert.Equal(2500, bought.Effect.CoinsSpent);
        var duplicate = await Assert.ThrowsAsync<AnimalActionException>(() => s.Command(s.Owner, "buy-animal", item: "peacock"));
        Assert.Equal("Você já possui um pavão.", duplicate.Message);
        await using var check = s.Db();
        Assert.Equal(16500, (await check.Inventories.SingleAsync(inventory => inventory.UserId == s.Owner)).Coins);
        Assert.Single(await check.FarmAnimals.Where(animal => animal.FarmId == s.Farm).ToListAsync());
    }

    [PostgresFact]
    public async Task PeacockSnapshot_PurchaseAndOpenCycleFreezePolicyAndCollectionRestartsOnlyPeacock()
    {
        var s = await Setup(allSpecies: true);
        var young = Peacock(await s.State(s.Owner));
        Assert.Equal(young.PurchasedAt.AddHours(8), young.MaturesAt);
        s.Policy.PeacockGrowthSeconds = 3600;
        s.Policy.PeacockProductionSeconds = 3600;
        s.Policy.PeacockBaseYield = 10;
        s.Policy.PeacockCollectXp = 50;
        s.Policy.PeacockOwnerReserveAbsolute = 2;
        s.Policy.PeacockOwnerReserveFraction = .6m;
        s.Policy.PeacockTheftAggregateLimit = 4;
        s.Policy.PeacockHelpBonusLimit = 5;
        s.Clock.Now = young.MaturesAt;
        var mature = await s.State(s.Owner);
        var peacock = Peacock(mature);
        Assert.Equal("adult", peacock.Stage);
        Assert.Equal("feather", peacock.Cycle!.ProductId);
        Assert.Equal(6, peacock.Cycle.BaseYield);
        Assert.Equal(young.MaturesAt.AddHours(72), peacock.Cycle.ReadyAt);
        Assert.False(peacock.Cycle.IsReady);
        var repeated = Peacock(await s.State(s.Owner));
        Assert.Equal(peacock.Cycle.Id, repeated.Cycle!.Id);
        Assert.Equal(mature.Revision, (await s.State(s.Owner)).Revision);
        await using (var db = s.Db())
        {
            var cycle = await db.AnimalProductionCycles.SingleAsync(cycle => cycle.Id == peacock.Cycle.Id);
            Assert.Equal(4, cycle.OwnerReserve);
            Assert.Equal(2, cycle.TheftLimit);
            Assert.Equal(3, cycle.HelpBonusLimit);
        }
        s.Clock.Now = peacock.Cycle.ReadyAt.AddDays(7); // Ready production never expires or accumulates extra cycles.
        var collected = await s.Command(s.Owner, "collect", farm: s.Farm, animal: peacock.Id, cycle: peacock.Cycle.Id);
        Assert.Equal(6, collected.Effect.Quantity);
        Assert.Equal(40, collected.Effect.XpGained);
        var successor = Peacock(collected.Current!);
        Assert.Equal(10, successor.Cycle!.BaseYield);
        Assert.False(successor.Cycle.IsReady);
        Assert.Equal(s.Clock.Now.AddHours(1), successor.Cycle.ReadyAt);
        foreach (var other in mature.Enclosure!.Animals.Where(animal => animal.AnimalTypeId != "peacock"))
            Assert.Equal(other.Cycle!.Id, collected.Current!.Enclosure!.Animals.Single(animal => animal.Id == other.Id).Cycle!.Id);
        await using var check = s.Db();
        var next = await check.AnimalProductionCycles.SingleAsync(cycle => cycle.Id == successor.Cycle.Id);
        Assert.Equal(6, next.OwnerReserve);
        Assert.Equal(4, next.TheftLimit);
        Assert.Equal(5, next.HelpBonusLimit);
        Assert.Equal(2, await check.AnimalProductionCycles.CountAsync(cycle => cycle.FarmAnimalId == peacock.Id));
    }

    [PostgresFact]
    public async Task Feather_HelpAndConcurrentTheftProtectReservePrivacyCollectionAndSale()
    {
        var s = await Setup();
        var young = Peacock(await s.State(s.Owner));
        Assert.False(Peacock(await s.State(s.Visitors[0])).Permissions.CanHelp);
        s.Clock.Now = young.MaturesAt;
        var peacock = Peacock(await s.State(s.Owner));
        Assert.Equal("NOT_READY", (await s.Attempt(s.Owner, "collect", s.Farm, peacock.Id, peacock.Cycle!.Id)).Error);
        Assert.Equal("SELF_ACTION", (await s.Attempt(s.Owner, "help", s.Farm, peacock.Id, peacock.Cycle.Id)).Error);
        Assert.Equal("NOT_OWNER", (await s.Attempt(s.Visitors[0], "collect", s.Farm, peacock.Id, peacock.Cycle.Id)).Error);
        var help = await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: peacock.Id, cycle: peacock.Cycle.Id);
        Assert.Equal(1, help.Effect.OwnerBonusAdded);
        Assert.Equal(2, help.Effect.CoinsGained);
        Assert.Equal(5, help.Effect.XpGained);
        s.Clock.Now = peacock.Cycle.ReadyAt;
        Assert.False(Peacock(await s.State(s.Visitors[1])).Permissions.CanHelp);
        Assert.Equal("NOT_PRODUCING", (await s.Attempt(s.Visitors[1], "help", s.Farm, peacock.Id, peacock.Cycle.Id)).Error);
        var thefts = await Task.WhenAll(s.Visitors.Take(3).Select(visitor => s.Attempt(visitor, "steal", s.Farm, peacock.Id, peacock.Cycle.Id)));
        Assert.Equal(2, thefts.Count(attempt => attempt.Response != null));
        Assert.Equal("THEFT_LIMIT_REACHED", Assert.Single(thefts, attempt => attempt.Error != null).Error);
        var thief = s.Visitors[Array.FindIndex(thefts, attempt => attempt.Response != null)];
        Assert.Equal("THEFT_ALREADY_REGISTERED", (await s.Attempt(thief, "steal", s.Farm, peacock.Id, peacock.Cycle.Id)).Error);
        Assert.All(thefts.Where(attempt => attempt.Response != null), attempt =>
        {
            Assert.Equal("feather", attempt.Response!.Effect.ProductId);
            Assert.Equal(1, attempt.Response.Effect.Quantity);
            Assert.True(Peacock(attempt.Response.Current!).Cycle!.IsReady);
        });
        var ownerView = Peacock(await s.State(s.Owner));
        Assert.Equal(6, ownerView.Cycle!.BaseYield);
        Assert.Equal(1, ownerView.Cycle.ProtectedHelpBonus);
        Assert.Equal(4, ownerView.Cycle.RemainingBase);
        Assert.Equal(5, ownerView.Cycle.OwnerCollectible);
        var visitorView = Peacock(await s.State(s.Visitors[4]));
        Assert.Equal(0, visitorView.Cycle!.BaseYield);
        Assert.Equal(0, visitorView.Cycle.ProtectedHelpBonus);
        Assert.Equal(0, visitorView.Cycle.OwnerCollectible);
        Assert.Equal(4, visitorView.Cycle.RemainingBase);
        Assert.Null(visitorView.Cycle.Helpers);
        Assert.False(visitorView.Permissions.CanSteal);
        Assert.Equal("THEFT_LIMIT_REACHED", visitorView.Permissions.StealReason);
        var key = Guid.NewGuid();
        var collected = await s.Command(s.Owner, "collect", key, s.Farm, peacock.Id, peacock.Cycle.Id);
        Assert.Equal(5, collected.Effect.Quantity);
        Assert.Equal(40, collected.Effect.XpGained);
        Assert.False(Peacock(collected.Current!).Cycle!.IsReady);
        var replay = await s.Command(s.Owner, "collect", key, s.Farm, peacock.Id, peacock.Cycle.Id);
        Assert.True(replay.Replayed);
        Assert.Equal(collected.OperationId, replay.OperationId);
        await using (var db = s.Db())
        {
            Assert.Equal(5, await db.InventoryItems.Where(item => item.Inventory.UserId == s.Owner && item.ItemId == "feather").SumAsync(item => item.Quantity));
            Assert.Equal(2, await db.InventoryItems.Where(item => s.Visitors.Contains(item.Inventory.UserId) && item.ItemId == "feather").SumAsync(item => item.Quantity));
            var notices = await db.Notifications.Where(notice => notice.AnimalCycleId == peacock.Cycle.Id).ToListAsync();
            Assert.Equal(3, notices.Count);
            var helperName = (await db.Users.SingleAsync(user => user.Id == s.Visitors[0])).Username;
            Assert.Equal($"Você receberá +1 pena na próxima coleta com a ajuda de {helperName}.", Assert.Single(notices, notice => notice.Type == NotificationType.AnimalHelped).Message);
            Assert.All(notices.Where(notice => notice.Type == NotificationType.AnimalProductStolen), notice => Assert.EndsWith("roubou 1 pena do pavão.", notice.Message));
        }
        var saleKey = Guid.NewGuid();
        var sold = await s.Command(s.Owner, "sell-animal-product", saleKey, item: "feather", quantity: 5);
        Assert.Equal(750, sold.Effect.CoinsGained);
        Assert.Equal(0, sold.Effect.XpGained);
        Assert.True((await s.Command(s.Owner, "sell-animal-product", saleKey, item: "feather", quantity: 5)).Replayed);
        Assert.Equal("INSUFFICIENT_STOCK", (await s.Attempt(s.Owner, "sell-animal-product", item: "feather", quantity: 1)).Error);
        await using var check = s.Db();
        Assert.Equal(17250, (await check.Inventories.SingleAsync(inventory => inventory.UserId == s.Owner)).Coins);
        Assert.Equal(0, await check.InventoryItems.Where(item => item.Inventory.UserId == s.Owner && item.ItemId == "feather").SumAsync(item => item.Quantity));
    }

    [PostgresFact]
    public async Task PeacockHelp_ConcurrentRepeatedKeysRewardOnceAndFullBonusDoesNotBlockOtherAnimals()
    {
        var s = await Setup(allSpecies: true);
        s.Clock.Now = Peacock(await s.State(s.Owner)).MaturesAt;
        var producing = await s.State(s.Owner);
        var peacock = Peacock(producing);
        var helper = s.Visitors[0];
        var key = Guid.NewGuid();
        var replayed = await Task.WhenAll(s.Command(helper, "help", key, s.Farm, peacock.Id, peacock.Cycle!.Id),
            s.Command(helper, "help", key, s.Farm, peacock.Id, peacock.Cycle.Id));
        Assert.Single(replayed, response => response.Replayed);
        Assert.Equal(replayed[0].OperationId, replayed[1].OperationId);
        var races = await Task.WhenAll(s.Attempt(s.Visitors[1], "help", s.Farm, peacock.Id, peacock.Cycle.Id),
            s.Attempt(s.Visitors[1], "help", s.Farm, peacock.Id, peacock.Cycle.Id));
        Assert.Single(races, attempt => attempt.Response != null);
        Assert.Equal("HELP_ALREADY_REGISTERED", Assert.Single(races, attempt => attempt.Error != null).Error);
        await s.Command(s.Visitors[2], "help", farm: s.Farm, animal: peacock.Id, cycle: peacock.Cycle.Id);
        var capped = Peacock(await s.State(s.Visitors[3]));
        Assert.False(capped.Permissions.CanHelp);
        Assert.Equal("OWNER_BONUS_LIMIT_REACHED", capped.Permissions.HelpReason);
        Assert.Equal("OWNER_BONUS_LIMIT_REACHED", (await s.Attempt(s.Visitors[3], "help", s.Farm, peacock.Id, peacock.Cycle.Id)).Error);
        var others = await Task.WhenAll(producing.Enclosure!.Animals.Where(animal => animal.Id != peacock.Id)
            .Select(animal => s.Attempt(s.Visitors[3], "help", s.Farm, animal.Id, animal.Cycle!.Id)));
        Assert.Equal(4, others.Length);
        Assert.All(others, attempt => Assert.NotNull(attempt.Response));
        await using (var db = s.Db())
        {
            Assert.Equal(3, (await db.AnimalProductionCycles.SingleAsync(cycle => cycle.Id == peacock.Cycle.Id)).ProtectedHelpBonus);
            Assert.Equal(3, await db.AnimalActionCompletions.CountAsync(completion => completion.CycleId == peacock.Cycle.Id && completion.Action == "help"));
            Assert.Equal(20002, (await db.Inventories.SingleAsync(inventory => inventory.UserId == helper)).Coins);
            Assert.Equal(5, (await db.Users.SingleAsync(user => user.Id == helper)).CurrentXp);
            Assert.Equal(20008, (await db.Inventories.SingleAsync(inventory => inventory.UserId == s.Visitors[3])).Coins);
            Assert.Equal(20, (await db.Users.SingleAsync(user => user.Id == s.Visitors[3])).CurrentXp);
        }
        s.Clock.Now = peacock.Cycle.ReadyAt;
        var collected = await s.Command(s.Owner, "collect", farm: s.Farm, animal: peacock.Id, cycle: peacock.Cycle.Id);
        Assert.Equal(9, collected.Effect.Quantity);
        var successor = Peacock(collected.Current!);
        var newVisit = Peacock(await s.State(helper));
        Assert.True(newVisit.Permissions.CanHelp);
        Assert.False(newVisit.Cycle!.ViewerHelped);
        await s.Command(helper, "help", farm: s.Farm, animal: successor.Id, cycle: successor.Cycle!.Id);
        await using var check = s.Db();
        Assert.Equal(20004, (await check.Inventories.SingleAsync(inventory => inventory.UserId == helper)).Coins);
        Assert.Equal(10, (await check.Users.SingleAsync(user => user.Id == helper)).CurrentXp);
    }

    [PostgresFact]
    public async Task PeacockCollection_ConcurrentReplayCreditsFeathersAndXpOnceAndSaleIsAtomic()
    {
        var s = await Setup();
        s.Clock.Now = Peacock(await s.State(s.Owner)).MaturesAt.AddHours(72);
        var peacock = Peacock(await s.State(s.Owner));
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(s.Command(s.Owner, "collect", key, s.Farm, peacock.Id, peacock.Cycle!.Id),
            s.Command(s.Owner, "collect", key, s.Farm, peacock.Id, peacock.Cycle.Id));
        Assert.Single(results, response => response.Replayed);
        Assert.Equal(results[0].OperationId, results[1].OperationId);
        await using (var db = s.Db())
        {
            Assert.Equal(6, await db.InventoryItems.Where(item => item.Inventory.UserId == s.Owner && item.ItemId == "feather").SumAsync(item => item.Quantity));
            Assert.Equal(40, (await db.Users.SingleAsync(user => user.Id == s.Owner)).CurrentXp);
            Assert.Equal(2, await db.AnimalProductionCycles.CountAsync(cycle => cycle.FarmAnimalId == peacock.Id));
        }
        var sales = await Task.WhenAll(s.Attempt(s.Owner, "sell-animal-product", item: "feather", quantity: 6),
            s.Attempt(s.Owner, "sell-animal-product", item: "feather", quantity: 6));
        Assert.Equal(900, Assert.Single(sales, attempt => attempt.Response != null).Response!.Effect.CoinsGained);
        Assert.Equal("INSUFFICIENT_STOCK", Assert.Single(sales, attempt => attempt.Error != null).Error);
        await using var check = s.Db();
        Assert.Equal(17400, (await check.Inventories.SingleAsync(inventory => inventory.UserId == s.Owner)).Coins);
        Assert.Equal(0, await check.InventoryItems.Where(item => item.Inventory.UserId == s.Owner && item.ItemId == "feather").SumAsync(item => item.Quantity));
    }

    [PostgresFact]
    public async Task PeacockNotifications_ConfiguredMultiFeatherActionsUsePluralAndMasculineAnimal()
    {
        var s = await Setup(new AnimalEnclosureOptions { PeacockHelpIncrement = 2, PeacockTheftPerVisitor = 2 });
        s.Clock.Now = Peacock(await s.State(s.Owner)).MaturesAt;
        var peacock = Peacock(await s.State(s.Owner));
        var help = await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: peacock.Id, cycle: peacock.Cycle!.Id);
        Assert.Equal(2, help.Effect.OwnerBonusAdded);
        s.Clock.Now = peacock.Cycle.ReadyAt;
        var theft = await s.Command(s.Visitors[1], "steal", farm: s.Farm, animal: peacock.Id, cycle: peacock.Cycle.Id);
        Assert.Equal(2, theft.Effect.Quantity);
        await using var db = s.Db();
        var notices = await db.Notifications.Where(notice => notice.AnimalCycleId == peacock.Cycle.Id).ToListAsync();
        Assert.Contains("+2 penas", Assert.Single(notices, notice => notice.Type == NotificationType.AnimalHelped).Message);
        Assert.EndsWith("roubou 2 penas do pavão.", Assert.Single(notices, notice => notice.Type == NotificationType.AnimalProductStolen).Message);
    }
}
