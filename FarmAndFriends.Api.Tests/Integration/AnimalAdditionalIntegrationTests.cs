using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Scenario = FarmAndFriends.Api.Tests.Integration.AnimalEnclosureIntegrationTests.Scenario;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class AnimalAdditionalIntegrationTests
{
    [PostgresFact]
    public async Task Theft_ConcurrentSameVisitorAndCycleCreditsExactlyOnce()
    {
        var s = await Scenario.Create();
        var animal = await s.Producing();
        s.Clock.Now = animal.Cycle!.ReadyAt;

        var results = await Task.WhenAll(
            s.Attempt(s.Visitors[0], "steal", s.Farm, animal.Id, animal.Cycle.Id),
            s.Attempt(s.Visitors[0], "steal", s.Farm, animal.Id, animal.Cycle.Id));

        Assert.Single(results, result => result.Response != null);
        Assert.Equal("THEFT_ALREADY_REGISTERED", Assert.Single(results, result => result.Error != null).Error);

        await using var db = s.Db();
        Assert.Equal(1, await db.AnimalActionCompletions.CountAsync(completion => completion.ActorUserId == s.Visitors[0]
            && completion.CycleId == animal.Cycle.Id && completion.Action == "steal"));
        Assert.Equal(1, await db.Notifications.CountAsync(notification => notification.ActorUserId == s.Visitors[0]
            && notification.AnimalCycleId == animal.Cycle.Id && notification.Type == NotificationType.AnimalProductStolen));
        Assert.Equal(1, (await db.AnimalProductionCycles.SingleAsync(cycle => cycle.Id == animal.Cycle.Id)).StolenBase);
        Assert.Equal(1, await db.InventoryItems.Where(item => item.Inventory.UserId == s.Visitors[0]
            && item.ItemType == ItemType.AnimalProduct && item.ItemId == "egg").SumAsync(item => item.Quantity));
    }

    [PostgresFact]
    public async Task Sell_LastStockConcurrentRequestsNeverDoubleCredit()
    {
        var s = await Scenario.Create();
        await using (var setup = s.Db())
        {
            var inventory = await setup.Inventories.SingleAsync(i => i.UserId == s.Owner);
            setup.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), InventoryId = inventory.Id,
                ItemId = "egg", ItemType = ItemType.AnimalProduct, Quantity = 1 });
            await setup.SaveChangesAsync();
        }
        var results = await Task.WhenAll(s.Attempt(s.Owner, "sell-animal-product", item: "egg", quantity: 1),
            s.Attempt(s.Owner, "sell-animal-product", item: "egg", quantity: 1));
        Assert.Null(Assert.Single(results, r => r.Response != null).Response!.Current);
        Assert.Equal("INSUFFICIENT_STOCK", Assert.Single(results, r => r.Error != null).Error);
        await using var db = s.Db();
        Assert.Equal(20020, (await db.Inventories.SingleAsync(i => i.UserId == s.Owner)).Coins);
        Assert.Equal(0, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "egg").SumAsync(i => i.Quantity));
    }

    [PostgresFact]
    public async Task Collect_ConcurrentSameKeyCreditsOnceAndReturnsCurrentCycle()
    {
        var s = await Scenario.Create(); var a = await s.Producing(); s.Clock.Now = a.Cycle!.ReadyAt;
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(s.Command(s.Owner, "collect", key, s.Farm, a.Id, a.Cycle.Id),
            s.Command(s.Owner, "collect", key, s.Farm, a.Id, a.Cycle.Id));
        Assert.Single(results, r => r.Replayed);
        Assert.Equal(results[0].OperationId, results[1].OperationId);
        Assert.All(results, r => Assert.NotEqual(a.Cycle.Id, r.Current!.Enclosure!.Animals[0].Cycle!.Id));
        await using var db = s.Db();
        Assert.Equal(8, await db.InventoryItems.Where(i => i.Inventory.UserId == s.Owner && i.ItemId == "egg").SumAsync(i => i.Quantity));
        Assert.Equal(25, (await db.Users.SingleAsync(u => u.Id == s.Owner)).CurrentXp);
        Assert.Equal(2, await db.AnimalProductionCycles.CountAsync(c => c.FarmAnimalId == a.Id));
    }

    [PostgresFact]
    public async Task CollectVersusTheft_ConservesProductsAndNeverTransfersOldHelpToNewCycle()
    {
        var s = await Scenario.Create(); var a = await s.Producing();
        await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: a.Id, cycle: a.Cycle!.Id);
        s.Clock.Now = a.Cycle.ReadyAt;
        var results = await Task.WhenAll(s.Attempt(s.Owner, "collect", s.Farm, a.Id, a.Cycle.Id),
            s.Attempt(s.Visitors[0], "steal", s.Farm, a.Id, a.Cycle.Id));
        Assert.NotNull(results[0].Response);
        Assert.True(results[1].Response != null || results[1].Error == "CYCLE_CHANGED");
        await using var db = s.Db();
        var ids = s.Visitors.Prepend(s.Owner).ToArray();
        Assert.Equal(9, await db.InventoryItems.Where(i => ids.Contains(i.Inventory.UserId) && i.ItemId == "egg").SumAsync(i => i.Quantity));
        var old = await db.AnimalProductionCycles.SingleAsync(c => c.Id == a.Cycle.Id);
        Assert.InRange(old.BaseYield - old.StolenBase, 4, 8);
        var next = await db.AnimalProductionCycles.SingleAsync(c => c.FarmAnimalId == a.Id && c.CollectedAt == null);
        Assert.Equal(0, next.ProtectedHelpBonus); Assert.Equal(0, next.StolenBase);
    }

    [PostgresFact]
    public async Task PolicyChange_PreservesOpenCycleAndAppliesFourFourToSuccessor()
    {
        var previous = new AnimalEnclosureOptions
        {
            OwnerReserveAbsolute = 4,
            OwnerReserveFraction = 0.75m,
            TheftAggregateLimit = 2
        };
        var s = await Scenario.Create(previous);
        var animal = await s.Producing();

        await using (var before = s.Db())
        {
            var open = await before.AnimalProductionCycles.SingleAsync(c => c.Id == animal.Cycle!.Id);
            Assert.Equal(6, open.OwnerReserve);
            Assert.Equal(2, open.TheftLimit);
        }

        s.Policy.OwnerReserveFraction = 0.50m;
        s.Policy.TheftAggregateLimit = 4;
        s.Clock.Now = animal.Cycle!.ReadyAt;
        await s.Command(s.Owner, "collect", farm: s.Farm, animal: animal.Id, cycle: animal.Cycle.Id);

        await using var after = s.Db();
        var historical = await after.AnimalProductionCycles.SingleAsync(c => c.Id == animal.Cycle.Id);
        var successor = await after.AnimalProductionCycles.SingleAsync(c => c.FarmAnimalId == animal.Id && c.CollectedAt == null);
        Assert.Equal(6, historical.OwnerReserve);
        Assert.Equal(2, historical.TheftLimit);
        Assert.Equal(4, successor.OwnerReserve);
        Assert.Equal(4, successor.TheftLimit);
    }

    [PostgresFact]
    public async Task Help_NewAnimalCycleReopensImmediatelyWithoutMovingWindow()
    {
        var s = await Scenario.Create();
        var a = await s.Producing();
        await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: a.Id, cycle: a.Cycle!.Id);
        s.Clock.Now = a.Cycle.ReadyAt;
        var collected = await s.Command(s.Owner, "collect", farm: s.Farm, animal: a.Id, cycle: a.Cycle.Id);
        var next = Assert.Single(collected.Current!.Enclosure!.Animals).Cycle!;
        var second = await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: a.Id, cycle: next.Id);
        Assert.Equal(2, second.Effect.CoinsGained);
        await using var db = s.Db();
        Assert.Equal(2, await db.AnimalActionCompletions.CountAsync(c => c.ActorUserId == s.Visitors[0] && c.Action == "help"));
        Assert.Equal(10, (await db.Users.SingleAsync(u => u.Id == s.Visitors[0])).CurrentXp);
        Assert.Equal(20004, (await db.Inventories.SingleAsync(i => i.UserId == s.Visitors[0])).Coins);
    }

    [PostgresFact]
    public async Task OwnerBonusFull_DisablesRewardUntilCollectionStartsNewCycle()
    {
        var s = await Scenario.Create(new() { HelpBonusLimit = 1 }); var a = await s.Producing();
        await s.Command(s.Visitors[0], "help", farm: s.Farm, animal: a.Id, cycle: a.Cycle!.Id);
        s.Policy.HelpBonusLimit = 999;
        var view = (await s.State(s.Visitors[1])).Enclosure!.Animals[0];
        Assert.False(view.Permissions.RewardAvailable); Assert.False(view.Permissions.CanHelp);
        Assert.Equal("OWNER_BONUS_LIMIT_REACHED", view.Permissions.HelpReason);
        Assert.Equal("OWNER_BONUS_LIMIT_REACHED", (await s.Attempt(s.Visitors[1], "help", s.Farm, a.Id, a.Cycle.Id)).Error);
        s.Clock.Now = a.Cycle.ReadyAt;
        await s.Command(s.Owner, "collect", farm: s.Farm, animal: a.Id, cycle: a.Cycle.Id);
        Assert.True((await s.State(s.Visitors[1])).Enclosure!.Animals[0].Permissions.CanHelp);
    }

    [PostgresFact]
    public async Task TimeBoundary_ReadyProjectionChangesWithoutRevisionAndRejectsHelp()
    {
        var s = await Scenario.Create(); var a = await s.Producing();
        var before = await s.State(s.Visitors[0]);
        s.Clock.Now = a.Cycle!.ReadyAt;
        var after = await s.State(s.Visitors[0]);
        Assert.Equal(before.Revision, after.Revision); Assert.True(after.ObservedAt > before.ObservedAt);
        Assert.False(before.Enclosure!.Animals[0].Cycle!.IsReady); Assert.True(after.Enclosure!.Animals[0].Cycle!.IsReady);
        Assert.Equal("NOT_PRODUCING", (await s.Attempt(s.Visitors[0], "help", s.Farm, a.Id, a.Cycle.Id)).Error);
        await using var db = s.Db();
        Assert.False(await db.AnimalActionCompletions.AnyAsync(c => c.ActorUserId == s.Visitors[0]));
    }

    [PostgresFact]
    public async Task Helpers_PaginationHasStableNamesAndRejectsCrossFarmCycle()
    {
        var s = await Scenario.Create(); var a = await s.Producing();
        foreach (var actor in s.Visitors.Take(3)) await s.Command(actor, "help", farm: s.Farm, animal: a.Id, cycle: a.Cycle!.Id);
        await using var db = s.Db();
        var user = await db.Users.SingleAsync(u => u.Id == s.Visitors[0]); var historicalName = user.Username;
        user.Username = "new_name"; await db.SaveChangesAsync();
        var helpers = new List<AnimalHelperDto>(); string? cursor = null;
        do
        {
            var page = await s.Service(db).GetHelpersAsync(s.Owner, s.Farm, a.Id, a.Cycle!.Id, cursor, 1);
            Assert.Equal(3, page.Total); helpers.AddRange(page.Items); cursor = page.NextCursor;
        } while (cursor != null);
        Assert.Equal(3, helpers.Select(h => h.UserId).Distinct().Count());
        Assert.Equal(historicalName, helpers.Single(h => h.UserId == s.Visitors[0]).DisplayNameAtHelp);
        var wrong = await Assert.ThrowsAsync<AnimalActionException>(() => s.Service(db).GetHelpersAsync(s.Owner, s.Farm, Guid.NewGuid(), a.Cycle!.Id, null, 1));
        Assert.Equal("RESOURCE_NOT_FOUND", wrong.Code);
    }
}
