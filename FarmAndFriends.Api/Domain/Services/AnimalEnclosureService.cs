using System.Text.Json;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FarmAndFriends.Api.Domain.Services;

public sealed partial class AnimalEnclosureService(
    AppDbContext db, IOptions<AnimalEnclosureOptions> options,
    TimeProvider clock, ExperienceService experience,
    ILogger<AnimalEnclosureService>? logger = null)
{
    private AnimalEnclosureOptions Policy => options.Value;
    private DateTime UtcNow
    {
        get
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        }
    }
    private static AnimalActionException Error(string code, string message, int status = 409) => new(code, message, status);
    private static AnimalEnclosureOptions ReadPolicy(string json) =>
        JsonSerializer.Deserialize<AnimalEnclosureOptions>(json)
        ?? throw Error("INVALID_CONFIGURATION", "Não foi possível carregar a produção.", 500);

    // The farm row serializes the whole enclosure aggregate. Explicit child locks retain
    // the same order for future species and coexist with land purchases and plant actions.
    // The actor user lock additionally serializes rewards across DIFFERENT farms.
    public async Task<AnimalOperationResponse> ExecuteAsync(Guid actorId, string action,
        Guid key, Guid? requestedFarmId = null, Guid? animalId = null,
        Guid? expectedCycleId = null, string? itemId = null, int quantity = 0,
        CancellationToken ct = default)
    {
        if (key == Guid.Empty) throw Error("INVALID_IDEMPOTENCY_KEY", "Envie uma chave de operação válida.", 400);
        if (action is not ("buy-animal-enclosure" or "buy-animal" or "collect" or "help" or "steal" or "sell-animal-product"))
            throw Error("INVALID_REQUEST", "Ação inválida.", 400);
        if (action is "collect" or "help" or "steal"
            && (requestedFarmId is null || animalId is null || expectedCycleId is null || expectedCycleId == Guid.Empty))
            throw Error("INVALID_REQUEST", "Informe o animal e o ciclo.", 400);
        if (action == "sell-animal-product" && quantity <= 0)
            throw Error("INVALID_QUANTITY", "Informe uma quantidade positiva.", 400);
        if (!Policy.IsValid()) throw Error("INVALID_CONFIGURATION", "Configuração de animais indisponível.", 500);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var actor = await db.LockAsync(actorId, ct)
                ?? throw Error("RESOURCE_NOT_FOUND", "Jogador não encontrado.", 404);
            var ownAction = action is "buy-animal-enclosure" or "buy-animal" or "sell-animal-product";
            var farmId = ownAction
                ? await db.Farms.Where(f => f.UserId == actorId).Select(f => f.Id).SingleOrDefaultAsync(ct)
                : requestedFarmId!.Value;
            var fingerprint = JsonSerializer.Serialize(new { action, farmId, animalId, expectedCycleId, itemId, quantity });
            // Replay precedes access/state/stock predicates consumed by the committed operation.
            var prior = await db.AnimalActionCompletions.AsNoTracking()
                .SingleOrDefaultAsync(c => c.ActorUserId == actorId && c.IdempotencyKey == key, ct);
            if (prior != null && prior.Fingerprint != fingerprint)
                throw Error("IDEMPOTENCY_KEY_REUSED", "Esta chave já foi usada em outra operação.");
            var identity = await db.Farms.AsNoTracking().SingleOrDefaultAsync(f => f.Id == farmId, ct);
            var authorized = identity != null && (identity.UserId == actorId
                || await LockFriendshipAsync(actorId, identity.UserId, ct));
            if (prior != null && !authorized)
            {
                await transaction.CommitAsync(ct);
                return new(prior.Id, true, JsonSerializer.Deserialize<AnimalOperationEffect>(prior.EffectJson)!, null);
            }
            if (identity == null) throw Error("RESOURCE_NOT_FOUND", "Fazenda não encontrada.", 404);
            if (!authorized) throw Error("NOT_FRIENDS", "Apenas amigos podem visitar esta fazenda.", 403);
            var farm = (await db.LockFarmAsync(farmId, ct))!;
            List<Plot>? plots = action == "buy-animal-enclosure" ? await db.LockFarmPlotsAsync(farmId, ct) : null;
            var aggregate = await LockAggregateAsync(farmId, ct);
            var now = UtcNow;
            await NormalizeAsync(aggregate, now, ct);
            if (prior != null)
            {
                await db.SaveChangesAsync(ct);
                var currentReplay = action == "sell-animal-product" && aggregate.Enclosure == null
                    ? null : await ProjectAsync(farm, actorId, aggregate, now, ct);
                await transaction.CommitAsync(ct);
                return new(prior.Id, true, JsonSerializer.Deserialize<AnimalOperationEffect>(prior.EffectJson)!, currentReplay);
            }
            if (action is "help" or "steal" && farm.UserId == actorId)
                throw Error("SELF_ACTION", "Esta ação é para visitantes.", 403);
            if (action == "collect" && farm.UserId != actorId)
                throw Error("NOT_OWNER", "Somente o dono pode coletar.", 403);
            if (action == "buy-animal" && AnimalSpeciesRules.Find(itemId) == null
                || action == "sell-animal-product" && AnimalSpeciesRules.FindProduct(itemId) == null)
                throw Error("INVALID_REQUEST", "Este item não está disponível.", 400);

            FarmAnimal? animal = null;
            AnimalProductionCycle? cycle = null;
            if (action is "collect" or "help" or "steal")
            {
                animal = aggregate.Animals.SingleOrDefault(a => a.Id == animalId)
                    ?? throw Error("RESOURCE_NOT_FOUND", "Animal não encontrado nesta fazenda.", 404);
                cycle = aggregate.Cycles.SingleOrDefault(c => c.FarmAnimalId == animalId && c.CollectedAt == null);
                if (cycle == null || cycle.Id != expectedCycleId)
                    throw Error("CYCLE_CHANGED", "A produção mudou. Atualize a fazenda.");
            }
            var inventory = await db.LockInventoryAsync(actorId, ct)
                ?? throw Error("RESOURCE_NOT_FOUND", "Inventário não encontrado.", 404);
            var spent = 0; var gained = 0; var xp = 0; var bonus = 0; var productQuantity = 0;
            var effectAnimalId = animal?.Id; var effectCycleId = cycle?.Id;
            string? productId = null; string? animalType = animal?.AnimalTypeId;

            switch (action)
            {
                case "buy-animal-enclosure":
                    var offer = EnclosureOffer(actor, plots!, aggregate.Enclosure != null, inventory.Coins);
                    if (!offer.CanBuy)
                    {
                        if (offer.Reason == "UNSUPPORTED_FARM_LAYOUT")
                            logger?.LogWarning("Animal enclosure purchase rejected for unsupported layout in farm {FarmId}.", farmId);
                        throw Error(offer.Reason!, OfferMessage(offer.Reason!));
                    }
                    spent = Policy.EnclosurePriceCoins;
                    inventory.Coins = checked(inventory.Coins - spent);
                    aggregate.Enclosure = new AnimalEnclosure { FarmId = farmId, PurchasedAt = now };
                    db.AnimalEnclosures.Add(aggregate.Enclosure);
                    break;
                case "buy-animal":
                    var species = AnimalSpeciesRules.Find(itemId)!;
                    if (aggregate.Enclosure == null) throw Error("ENCLOSURE_REQUIRED", "Compre o cercado primeiro.");
                    if (aggregate.Animals.Any(a => a.AnimalTypeId == species.Id)) throw Error("ALREADY_OWNED", $"Você já possui {(species.Id is "pig" or "peacock" ? "um" : "uma")} {species.DisplayName.ToLowerInvariant()}.");
                    if (actor.Level < AnimalSpeciesRules.MinLevel(species, Policy)) throw Error("LEVEL_REQUIRED", "Seu nível ainda é insuficiente.");
                    spent = AnimalSpeciesRules.Price(species, Policy);
                    if (inventory.Coins < spent) throw Error("INSUFFICIENT_FUNDS", "Moedas insuficientes.");
                    inventory.Coins = checked(inventory.Coins - spent);
                    var initialPolicy = Policy.ForSpecies(species.Id);
                    animal = new FarmAnimal { FarmId = farmId, AnimalEnclosureId = aggregate.Enclosure.Id,
                        AnimalTypeId = species.Id, PurchasedAt = now, MaturesAt = now.AddSeconds(initialPolicy.GrowthSeconds),
                        InitialPolicyJson = JsonSerializer.Serialize(initialPolicy) };
                    db.FarmAnimals.Add(animal); aggregate.Animals.Add(animal);
                    effectAnimalId = animal.Id; animalType = species.Id;
                    aggregate.Enclosure.Revision++;
                    break;
                case "collect":
                    if (now < cycle!.ReadyAt) throw Error("NOT_READY", "A produção ainda não está pronta.");
                    productId = cycle.ProductId;
                    productQuantity = checked(cycle.BaseYield - cycle.StolenBase + cycle.ProtectedHelpBonus);
                    xp = ReadPolicy(cycle.PolicyJson).CollectXp;
                    await ChangeProductAsync(inventory.Id, productId, productQuantity, ct);
                    cycle.CollectedAt = now;
                    // Close the old filtered-unique row before inserting its successor, within this transaction.
                    await db.SaveChangesAsync(ct);
                    var next = NewCycle(animal!, now, JsonSerializer.Serialize(Policy.ForSpecies(animal!.AnimalTypeId)));
                    db.AnimalProductionCycles.Add(next); aggregate.Cycles.Add(next);
                    aggregate.Enclosure!.Revision++;
                    break;
                case "help":
                    var helpPolicy = ReadPolicy(cycle!.PolicyJson);
                    if (await db.AnimalActionCompletions.AnyAsync(c => c.ActorUserId == actorId && c.CycleId == cycle.Id && c.Action == "help", ct))
                        throw Error("HELP_ALREADY_REGISTERED", "Você já ajudou neste ciclo.");
                    if (now < cycle.StartsAt || now >= cycle.ReadyAt) throw Error("NOT_PRODUCING", "A ajuda está disponível durante a produção.");
                    bonus = AnimalRules.HelpBonusAdded(cycle, helpPolicy.HelpIncrement);
                    if (bonus == 0) throw Error("OWNER_BONUS_LIMIT_REACHED", "O bônus de produção deste ciclo está completo.");
                    gained = helpPolicy.HelpCoins; xp = helpPolicy.HelpXp;
                    inventory.Coins = checked(inventory.Coins + gained);
                    cycle.ProtectedHelpBonus = checked(cycle.ProtectedHelpBonus + bonus);
                    aggregate.Enclosure!.Revision++;
                    break;
                case "steal":
                    if (animal!.AnimalTypeId == "pig") throw Error("NOT_STEALABLE", "Adubo não pode ser roubado.");
                    if (now < cycle!.ReadyAt) throw Error("NOT_READY", "A produção ainda não está pronta.");
                    var theftPolicy = ReadPolicy(cycle.PolicyJson);
                    var theftReason = await TheftUnavailableReasonAsync(actorId, cycle, theftPolicy, ct);
                    if (theftReason != null)
                        throw theftReason == "THEFT_ALREADY_REGISTERED"
                            ? Error(theftReason, "Você já roubou deste animal neste ciclo.")
                            : Error(theftReason, "A parte disponível para roubo já acabou.");
                    productQuantity = AnimalRules.Stealable(cycle, theftPolicy.TheftPerVisitor);
                    productId = cycle.ProductId; xp = theftPolicy.TheftXp;
                    cycle.StolenBase = checked(cycle.StolenBase + productQuantity);
                    await ChangeProductAsync(inventory.Id, productId, productQuantity, ct);
                    aggregate.Enclosure!.Revision++;
                    break;
                case "sell-animal-product":
                    productId = itemId!; productQuantity = quantity;
                    gained = checked(quantity * AnimalSpeciesRules.SellPrice(AnimalSpeciesRules.FindProduct(productId)!, Policy));
                    await ChangeProductAsync(inventory.Id, productId, -quantity, ct);
                    inventory.Coins = checked(inventory.Coins + gained);
                    break;
            }
            experience.AddXp(actor, xp);
            var effect = new AnimalOperationEffect(action, now, farmId, effectAnimalId, effectCycleId,
                animalType, productId, productQuantity, spent, gained, xp, bonus);
            var completion = new AnimalActionCompletion { ActorUserId = actorId, OwnerUserId = farm.UserId,
                FarmId = farmId, AnimalId = effectAnimalId, CycleId = effectCycleId, IdempotencyKey = key,
                Fingerprint = fingerprint, Action = action, CompletedAt = now, DisplayNameAtAction = actor.Username,
                EffectJson = JsonSerializer.Serialize(effect), CoinsGained = gained, XpGained = xp };
            db.AnimalActionCompletions.Add(completion);
            if (action is "help" or "steal")
            {
                var socialSpecies = AnimalSpeciesRules.Find(animalType)!;
                db.Notifications.Add(new Notification { RecipientUserId = farm.UserId, ActorUserId = actorId,
                    Type = action == "help" ? NotificationType.AnimalHelped : NotificationType.AnimalProductStolen,
                    AnimalId = animalId, AnimalCycleId = effectCycleId, AnimalCompletionId = completion.Id,
                    CreatedAt = now, Message = action == "help"
                        ? $"Você receberá +{FormatProductQuantity(socialSpecies, bonus)} na próxima coleta com a ajuda de {actor.Username}."
                        : $"{actor.Username} roubou {FormatProductQuantity(socialSpecies, productQuantity)} {(socialSpecies.Id is "pig" or "peacock" ? "do" : "da")} {socialSpecies.DisplayName.ToLowerInvariant()}." });
            }
            await db.SaveChangesAsync(ct);
            var current = action == "sell-animal-product" && aggregate.Enclosure == null
                ? null : await ProjectAsync(farm, actorId, aggregate, now, ct);
            await transaction.CommitAsync(ct);
            logger?.LogInformation("Animal operation {OperationId} committed: {Action} by {ActorId} in farm {FarmId}.",
                completion.Id, action, actorId, farmId);
            return new(completion.Id, false, effect, current);
        }
        catch (OverflowException)
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
            throw Error("BALANCE_LIMIT", "Esta operação ultrapassaria o limite de saldo ou estoque.");
        }
        catch
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); throw;
        }
    }

    private async Task ChangeProductAsync(Guid inventoryId, string itemId, int change, CancellationToken ct)
    {
        // The inventory lock serializes all its items, including insertion of a missing row.
        var item = await db.InventoryItems.FromSqlInterpolated($"""
            SELECT * FROM "InventoryItems" WHERE "InventoryId" = {inventoryId}
            AND "ItemType" = {(int)ItemType.AnimalProduct} AND "ItemId" = {itemId} FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        if (item == null)
        {
            if (change < 0) throw Error("INSUFFICIENT_STOCK", "Você não possui produção suficiente.");
            item = new InventoryItem { Id = Guid.NewGuid(), InventoryId = inventoryId,
                ItemId = itemId, ItemType = ItemType.AnimalProduct };
            db.InventoryItems.Add(item);
        }
        var next = checked(item.Quantity + change);
        if (next < 0) throw Error("INSUFFICIENT_STOCK", "Você não possui produção suficiente.");
        item.Quantity = next;
    }

    private static string FormatProductQuantity(AnimalSpeciesDefinition species, int quantity) => species.ProductId switch
    {
        "egg" => $"{quantity} {(quantity == 1 ? "ovo" : "ovos")}",
        "milk" => $"{quantity} {(quantity == 1 ? "leite" : "leites")}",
        "wool" => $"{quantity} {(quantity == 1 ? "lã" : "lãs")}",
        "feather" => $"{quantity} {(quantity == 1 ? "pena" : "penas")}",
        "fertilizer" => $"{quantity} {(quantity == 1 ? "adubo" : "adubos")}",
        _ => $"{quantity} {species.ProductUnits}"
    };

    private async Task<bool> LockFriendshipAsync(Guid actorId, Guid ownerId, CancellationToken ct)
    {
        var (a, b) = FriendshipService.GetCanonicalPair(actorId, ownerId);
        var friendship = await db.Friendships.FromSqlInterpolated($"""
            SELECT * FROM "Friendships" WHERE "UserAId" = {a} AND "UserBId" = {b} FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        return friendship?.Status == FriendshipStatus.Accepted;
    }

    private sealed class Aggregate(AnimalEnclosure? enclosure, List<FarmAnimal> animals, List<AnimalProductionCycle> cycles)
    {
        public AnimalEnclosure? Enclosure { get; set; } = enclosure;
        public List<FarmAnimal> Animals { get; } = animals;
        public List<AnimalProductionCycle> Cycles { get; } = cycles;
    }

    private async Task<Aggregate> LockAggregateAsync(Guid farmId, CancellationToken ct)
    {
        var enclosure = await db.AnimalEnclosures.FromSqlInterpolated($"""
            SELECT * FROM "AnimalEnclosures" WHERE "FarmId" = {farmId} FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        var animals = await db.FarmAnimals.FromSqlInterpolated($"""
            SELECT * FROM "FarmAnimals" WHERE "FarmId" = {farmId} ORDER BY "Id" FOR UPDATE
            """).ToListAsync(ct);
        var cycles = await db.AnimalProductionCycles.FromSqlInterpolated($"""
            SELECT c.* FROM "AnimalProductionCycles" c JOIN "FarmAnimals" a ON a."Id" = c."FarmAnimalId"
            WHERE a."FarmId" = {farmId} AND c."CollectedAt" IS NULL ORDER BY c."Id" FOR UPDATE OF c
            """).ToListAsync(ct);
        return new(enclosure, animals, cycles);
    }

    private async Task NormalizeAsync(Aggregate aggregate, DateTime now, CancellationToken ct)
    {
        foreach (var animal in aggregate.Animals.Where(a => a.MaturesAt <= now))
        {
            if (aggregate.Cycles.Any(c => c.FarmAnimalId == animal.Id)) continue;
            // A mature animal has exactly one uncollected cycle. No offline replay loop.
            var cycle = NewCycle(animal, animal.MaturesAt, animal.InitialPolicyJson);
            aggregate.Cycles.Add(cycle); db.AnimalProductionCycles.Add(cycle);
            aggregate.Enclosure!.Revision++;
        }
        await db.SaveChangesAsync(ct);
    }

    private static AnimalProductionCycle NewCycle(FarmAnimal animal, DateTime startsAt, string policyJson)
    {
        var p = ReadPolicy(policyJson);
        var species = AnimalSpeciesRules.Find(animal.AnimalTypeId)
            ?? throw Error("INVALID_CONFIGURATION", "Este animal não está disponível.", 500);
        return new() { FarmAnimalId = animal.Id, ProductId = species.ProductId, StartsAt = startsAt,
            ReadyAt = startsAt.AddSeconds(p.ProductionSeconds), PolicyJson = policyJson,
            BaseYield = p.BaseYield, OwnerReserve = AnimalRules.OwnerReserve(p),
            TheftLimit = AnimalRules.TheftLimit(p), HelpBonusLimit = p.HelpBonusLimit };
    }
}
