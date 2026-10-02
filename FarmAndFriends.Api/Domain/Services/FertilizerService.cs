using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Contracts.Fertilizer;
using FarmAndFriends.Api.Controllers;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FarmAndFriends.Api.Domain.Services;

public sealed class FertilizerService(AppDbContext db, PremiumCurrencyService premium,
    IOptions<FertilizerOptions> options, TimeProvider clock)
{
    private FertilizerOptions Policy => options.Value;
    private static AnimalActionException Error(string code, string message, int status = 409) => new(code, message, status);
    private DateTime UtcNow
    {
        get
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        }
    }

    public FertilizerOffer GetOffer() => new(FertilizerOptions.ItemId, "Adubo", Policy.PremiumPrice,
        Policy.MaximumReductionSeconds, Policy.MinimumRemainingSeconds);

    public async Task<IReadOnlyDictionary<Guid, DateTime>> GetCurrentApplicationsAsync(Guid farmId,
        CancellationToken ct = default) =>
        await FertilizerApplicationQuery.CurrentForFarm(db, farmId)
            .Select(application => new { application.PlotId, application.AppliedAt })
            .ToDictionaryAsync(x => x.PlotId, x => x.AppliedAt, ct);

    public async Task<BuyFertilizerResponse> BuyAsync(Guid actorId, Guid key, int quantity, CancellationToken ct = default)
    {
        if (key == Guid.Empty) throw Error("INVALID_IDEMPOTENCY_KEY", "Chave de operação inválida.", 400);
        if (quantity < 1 || quantity > Policy.MaxPurchaseQuantity)
            throw Error("INVALID_QUANTITY", $"A quantidade deve estar entre 1 e {Policy.MaxPurchaseQuantity}.", 400);
        var cost = (long)quantity * Policy.PremiumPrice;
        if (cost > int.MaxValue) throw Error("INVALID_QUANTITY", "Custo acima do limite permitido.", 400);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var actor = await db.LockAsync(actorId, ct) ?? throw Error("RESOURCE_NOT_FOUND", "Jogador não encontrado.", 404);
        var inventory = await db.LockInventoryAsync(actorId, ct)
            ?? throw Error("RESOURCE_NOT_FOUND", "Inventário não encontrado.", 404);
        var ledgerKey = $"fertilizer-buy:{key:D}";
        var prior = await db.PremiumCurrencyTransactions.AsNoTracking()
            .Include(t => t.PurchaseItems)
            .SingleOrDefaultAsync(t => t.UserId == actorId && t.IdempotencyKey == ledgerKey, ct);
        if (prior != null && (prior.EventType != PremiumCurrencyEventType.PremiumItemPurchase
            || prior.EventReference != FertilizerOptions.ItemId
            || prior.PurchaseItems.SingleOrDefault()?.Quantity != quantity))
            throw Error("IDEMPOTENCY_KEY_REUSED", "Esta chave já foi usada em outra compra.");

        var item = await LockFertilizerItemAsync(inventory.Id, ct);
        if (prior != null)
        {
            await tx.CommitAsync(ct);
            return new(prior.Id, true, FertilizerOptions.ItemId, quantity,
                item?.Quantity ?? 0, inventory.PremiumCoins);
        }
        if ((long)(item?.Quantity ?? 0) + quantity > int.MaxValue)
            throw Error("STOCK_LIMIT", "Limite de estoque de adubo atingido.");
        var debit = await premium.DebitAsync(actorId, (int)cost, PremiumCurrencyEventType.PremiumItemPurchase,
            FertilizerOptions.ItemId, ledgerKey,
            [new PremiumCurrencyPurchaseItemSnapshot(FertilizerOptions.ItemId, "Adubo", quantity, Policy.PremiumPrice)],
            cancellationToken: ct);
        if (!debit.Succeeded)
            throw debit.Failure == PremiumCurrencyOperationFailure.InsufficientBalance
                ? Error("INSUFFICIENT_FUNDS", "Notas insuficientes.")
                : Error("PURCHASE_UNAVAILABLE", "Não foi possível concluir a compra.");
        if (item == null)
        {
            item = new InventoryItem { InventoryId = inventory.Id, ItemType = ItemType.AnimalProduct,
                ItemId = FertilizerOptions.ItemId, Quantity = quantity };
            db.InventoryItems.Add(item);
        }
        else item.Quantity += quantity;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(debit.Transaction!.Id, false, FertilizerOptions.ItemId, quantity,
            item.Quantity, inventory.PremiumCoins);
    }

    public async Task<FertilizePlotResponse> ApplyAsync(Guid actorId, Guid key, Guid farmId,
        Guid plotId, FertilizePlotRequest request, CancellationToken ct = default)
    {
        if (key == Guid.Empty) throw Error("INVALID_IDEMPOTENCY_KEY", "Chave de operação inválida.", 400);
        if (request.ExpectedHarvestCycle < 1 || request.ExpectedCycleStartedAt.Kind != DateTimeKind.Utc)
            throw Error("INVALID_REQUEST", "Informe o ciclo atual do lote em UTC.", 400);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var actor = await db.LockAsync(actorId, ct)
            ?? throw Error("RESOURCE_NOT_FOUND", "Jogador não encontrado.", 404);
        var prior = await db.FertilizerApplications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorUserId == actorId && x.IdempotencyKey == key, ct);
        if (prior != null)
        {
            if (prior.FarmId != farmId || prior.PlotId != plotId || prior.HarvestCycle != request.ExpectedHarvestCycle
                || prior.CycleStartedAt != request.ExpectedCycleStartedAt)
                throw Error("IDEMPOTENCY_KEY_REUSED", "Esta chave já foi usada em outra aplicação.");
            await tx.CommitAsync(ct);
            return new(prior.Id, true, prior.PreviousReadyAt, prior.ReadyAt, prior.RemainingQuantityAfter);
        }
        var identity = await db.Farms.AsNoTracking().SingleOrDefaultAsync(f => f.Id == farmId, ct)
            ?? throw Error("RESOURCE_NOT_FOUND", "Fazenda não encontrada.", 404);
        if (identity.UserId != actorId)
        {
            var (a, b) = FriendshipService.GetCanonicalPair(actorId, identity.UserId);
            var friendship = await db.Friendships.FromSqlInterpolated($"""
                SELECT * FROM "Friendships" WHERE "UserAId" = {a} AND "UserBId" = {b} FOR UPDATE
                """).SingleOrDefaultAsync(ct);
            if (friendship?.Status != FriendshipStatus.Accepted)
                throw Error("NOT_FRIENDS", "Apenas amigos podem adubar esta fazenda.", 403);
        }
        var farm = await db.LockFarmAsync(farmId, ct)
            ?? throw Error("RESOURCE_NOT_FOUND", "Fazenda não encontrada.", 404);
        var plot = (await db.LockFarmPlotsAsync(farmId, ct)).SingleOrDefault(p => p.Id == plotId);
        if (plot == null || !plot.Unlocked) throw Error("RESOURCE_NOT_FOUND", "Lote não encontrado.", 404);
        prior = await db.FertilizerApplications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorUserId == actorId && x.IdempotencyKey == key, ct);
        if (prior != null)
        {
            if (prior.FarmId != farmId || prior.PlotId != plotId || prior.HarvestCycle != request.ExpectedHarvestCycle
                || prior.CycleStartedAt != request.ExpectedCycleStartedAt)
                throw Error("IDEMPOTENCY_KEY_REUSED", "Esta chave já foi usada em outra aplicação.");
            await tx.CommitAsync(ct);
            return new(prior.Id, true, prior.PreviousReadyAt, prior.ReadyAt, prior.RemainingQuantityAfter);
        }
        var now = UtcNow;
        if (plot.SeedId == null) throw Error("PLOT_EMPTY", "Este lote está vazio.");
        if (plot.CurrentHarvestCycle != request.ExpectedHarvestCycle
            || plot.CurrentHarvestCycleStartedAt != request.ExpectedCycleStartedAt)
            throw Error("CYCLE_CHANGED", "O ciclo do lote mudou. Atualize a fazenda.");
        var seed = await db.Seeds.SingleOrDefaultAsync(s => s.Id == plot.SeedId, ct)
            ?? throw Error("INVALID_CROP_STATE", "A cultura deste lote está indisponível.", 500);
        try { CropCycleRules.ValidateCurrentCycle(plot, seed); }
        catch (InvalidOperationException) { throw Error("INVALID_CROP_STATE", "O ciclo deste lote está inconsistente.", 500); }
        if (plot.ReadyAt <= now) throw Error("ALREADY_READY", "Esta plantação já está pronta.");
        if (await db.FertilizerApplications.AnyAsync(x => x.PlotId == plotId
            && x.HarvestCycle == request.ExpectedHarvestCycle
            && x.CycleStartedAt == request.ExpectedCycleStartedAt, ct))
            throw Error("ALREADY_FERTILIZED", "Este ciclo já recebeu adubo.");
        // A zero floor allows fertilizer to complete growth at the captured server time.
        var minimumReadyAt = now.AddSeconds(Policy.MinimumRemainingSeconds);
        var readyAt = plot.ReadyAt!.Value.AddSeconds(-Policy.MaximumReductionSeconds);
        if (readyAt < minimumReadyAt) readyAt = minimumReadyAt;
        if (readyAt >= plot.ReadyAt) throw Error("NO_ACCELERATION", "Este lote está próximo de ficar pronto.");

        var inventory = await db.LockInventoryAsync(actorId, ct)
            ?? throw Error("RESOURCE_NOT_FOUND", "Inventário não encontrado.", 404);
        var item = await LockFertilizerItemAsync(inventory.Id, ct);
        if (item == null || item.Quantity < 1) throw Error("INSUFFICIENT_STOCK", "Você não possui adubo.");
        item.Quantity--;
        var previousReadyAt = plot.ReadyAt.Value;
        plot.ReadyAt = readyAt;
        var completion = new FertilizerApplication { ActorUserId = actorId, OwnerUserId = farm.UserId,
            FarmId = farmId, PlotId = plotId, HarvestCycle = request.ExpectedHarvestCycle,
            CycleStartedAt = request.ExpectedCycleStartedAt, IdempotencyKey = key, AppliedAt = now,
            PreviousReadyAt = previousReadyAt, ReadyAt = readyAt,
            RemainingQuantityAfter = item.Quantity, ActorUsernameAtAction = actor.Username };
        db.FertilizerApplications.Add(completion);
        if (farm.UserId != actorId)
            db.Notifications.Add(new Notification { RecipientUserId = farm.UserId, ActorUserId = actorId,
                Type = NotificationType.PlotFertilized, CreatedAt = now,
                Message = $"{actor.Username} adubou uma plantação na sua fazenda." });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(completion.Id, false, previousReadyAt, readyAt, item.Quantity);
    }

    private async Task<InventoryItem?> LockFertilizerItemAsync(Guid inventoryId, CancellationToken ct) =>
        await db.InventoryItems.FromSqlInterpolated($"""
            SELECT * FROM "InventoryItems" WHERE "InventoryId" = {inventoryId}
            AND "ItemType" = {(int)ItemType.AnimalProduct} AND "ItemId" = {FertilizerOptions.ItemId}
            FOR UPDATE
            """).SingleOrDefaultAsync(ct);
}
