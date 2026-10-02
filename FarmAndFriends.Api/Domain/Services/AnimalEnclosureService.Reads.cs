using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Domain.Services;

public sealed partial class AnimalEnclosureService
{
    public async Task<AnimalStateEnvelope> GetStateAsync(Guid actorId, Guid farmId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync(actorId, ct);
        var identity = await db.Farms.AsNoTracking().SingleOrDefaultAsync(f => f.Id == farmId, ct)
            ?? throw Error("RESOURCE_NOT_FOUND", "Fazenda não encontrada.", 404);
        if (identity.UserId != actorId && !await LockFriendshipAsync(actorId, identity.UserId, ct))
            throw Error("NOT_FRIENDS", "Apenas amigos podem visitar esta fazenda.", 403);
        var farm = (await db.LockFarmAsync(farmId, ct))!;
        var aggregate = await LockAggregateAsync(farmId, ct);
        var now = UtcNow;
        await NormalizeAsync(aggregate, now, ct);
        var result = await ProjectAsync(farm, actorId, aggregate, now, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<AnimalEnclosureOffer> GetEnclosureOfferAsync(Guid actorId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await db.LockAsync(actorId, ct) ?? throw Error("RESOURCE_NOT_FOUND", "Jogador não encontrado.", 404);
        var farmId = await db.Farms.Where(f => f.UserId == actorId).Select(f => f.Id).SingleAsync(ct);
        await db.LockFarmAsync(farmId, ct);
        var plots = await db.LockFarmPlotsAsync(farmId, ct);
        var owned = await db.AnimalEnclosures.AnyAsync(e => e.FarmId == farmId, ct);
        var inventory = await db.LockInventoryAsync(actorId, ct);
        var result = EnclosureOffer(actor, plots, owned, inventory?.Coins ?? 0);
        await transaction.CommitAsync(ct);
        return result;
    }

    private AnimalEnclosureOffer EnclosureOffer(User user, List<Plot> plots, bool owned, int coins)
    {
        var valid = FarmLayoutRules.Evaluate(plots).Status != FarmLayoutStatus.Unsupported;
        var expanded = AnimalRules.FirstExpansionReached(plots);
        var initial = plots.Count(p => p.Unlocked && p.X is >= 0 and < 3 && p.Y is >= 0 and < 3);
        var reason = owned ? "ALREADY_OWNED" : !valid ? "UNSUPPORTED_FARM_LAYOUT"
            : !expanded ? "FIRST_EXPANSION_REQUIRED" : user.Level < Policy.EnclosureMinLevel ? "LEVEL_REQUIRED"
            : coins < Policy.EnclosurePriceCoins ? "INSUFFICIENT_FUNDS" : null;
        return new(Policy.EnclosurePriceCoins, Policy.EnclosureMinLevel, user.Level, expanded,
            initial, FarmLayoutRules.InitialPlotCount, owned, reason == null, reason);
    }

    private static string OfferMessage(string reason) => reason switch
    {
        "ALREADY_OWNED" => "Você já possui um cercado.",
        "UNSUPPORTED_FARM_LAYOUT" => "Não foi possível validar a expansão da fazenda.",
        "FIRST_EXPANSION_REQUIRED" => "Adquira o nono lote para liberar o cercado.",
        "LEVEL_REQUIRED" => "Seu nível ainda é insuficiente.",
        _ => "Moedas insuficientes."
    };

    public async Task<IReadOnlyList<AnimalOffer>> GetAnimalOffersAsync(Guid actorId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await db.LockAsync(actorId, ct) ?? throw Error("RESOURCE_NOT_FOUND", "Jogador não encontrado.", 404);
        var farmId = await db.Farms.Where(f => f.UserId == actorId).Select(f => f.Id).SingleAsync(ct);
        await db.LockFarmAsync(farmId, ct);
        var aggregate = await LockAggregateAsync(farmId, ct);
        var inventory = await db.LockInventoryAsync(actorId, ct);
        var offers = AnimalSpeciesRules.Available.Select(species =>
        {
            var owned = aggregate.Animals.Any(a => a.AnimalTypeId == species.Id);
            var price = AnimalSpeciesRules.Price(species, Policy);
            var minLevel = AnimalSpeciesRules.MinLevel(species, Policy);
            var reason = owned ? "ALREADY_OWNED" : aggregate.Enclosure == null ? "ENCLOSURE_REQUIRED"
                : actor.Level < minLevel ? "LEVEL_REQUIRED"
                : (inventory?.Coins ?? 0) < price ? "INSUFFICIENT_FUNDS" : null;
            var speciesPolicy = Policy.ForSpecies(species.Id);
            return new AnimalOffer(species.Id, species.DisplayName, price, minLevel,
                speciesPolicy.GrowthSeconds, speciesPolicy.ProductionSeconds, owned, reason == null, reason);
        }).ToList();
        await transaction.CommitAsync(ct);
        return offers;
    }

    public IReadOnlyList<AnimalProductOffer> GetProductOffers() => AnimalSpeciesRules.Available
        .Where(s => s.Id != "pig")
        .Select(s => new AnimalProductOffer(s.ProductId, s.ProductDisplayName, AnimalSpeciesRules.SellPrice(s, Policy))).ToList();

    private async Task<AnimalStateEnvelope> ProjectAsync(Farm farm, Guid viewerId, Aggregate aggregate, DateTime now, CancellationToken ct)
    {
        if (aggregate.Enclosure == null) return new(farm.Id, null, 0, now);
        var owner = farm.UserId == viewerId;
        var animals = new List<AnimalDto>();
        foreach (var animal in aggregate.Animals.OrderBy(a => a.AnimalTypeId))
        {
            var species = AnimalSpeciesRules.Find(animal.AnimalTypeId)
                ?? throw Error("INVALID_CONFIGURATION", "Este animal não está disponível.", 500);
            var cycle = aggregate.Cycles.SingleOrDefault(c => c.FarmAnimalId == animal.Id && c.CollectedAt == null);
            AnimalCycleDto? dto = null;
            var p = ReadPolicy(cycle?.PolicyJson ?? animal.InitialPolicyJson);
            string? collectReason = owner ? "NOT_READY" : "NOT_OWNER";
            string? helpReason = owner ? "SELF_ACTION" : "NOT_PRODUCING";
            string? stealReason = owner ? "SELF_ACTION" : "NOT_READY";
            if (cycle != null)
            {
                var ready = now >= cycle.ReadyAt;
                var helped = await db.AnimalActionCompletions.AnyAsync(c => c.CycleId == cycle.Id
                    && c.ActorUserId == viewerId && c.Action == "help", ct);
                var helperCount = await db.AnimalActionCompletions.CountAsync(c => c.CycleId == cycle.Id && c.Action == "help", ct);
                var helpers = owner ? await HelpersPageAsync(cycle.Id, null, 3, ct) : null;
                var visibleBaseYield = owner ? cycle.BaseYield : 0;
                var visibleProtectedHelpBonus = owner ? cycle.ProtectedHelpBonus : 0;
                var remainingBase = cycle.BaseYield - cycle.StolenBase;
                var visibleOwnerCollectible = owner ? remainingBase + cycle.ProtectedHelpBonus : 0;
                dto = new(cycle.Id, cycle.ProductId, cycle.StartsAt, cycle.ReadyAt, ready,
                    visibleBaseYield, visibleProtectedHelpBonus, remainingBase,
                    visibleOwnerCollectible, helperCount,
                    helped, helpers?.Items, helpers?.NextCursor);
                collectReason = !owner ? "NOT_OWNER" : ready ? null : "NOT_READY";
                helpReason = owner ? "SELF_ACTION" : helped ? "HELP_ALREADY_REGISTERED"
                    : ready || now < cycle.StartsAt ? "NOT_PRODUCING"
                    : cycle.ProtectedHelpBonus >= cycle.HelpBonusLimit ? "OWNER_BONUS_LIMIT_REACHED" : null;
                stealReason = species.Id == "pig" ? "NOT_STEALABLE" : owner ? "SELF_ACTION" : !ready ? "NOT_READY"
                    : await TheftUnavailableReasonAsync(viewerId, cycle, p, ct);
            }
            var permissions = new AnimalPermissions(collectReason == null, helpReason == null, stealReason == null,
                collectReason, helpReason, stealReason, !owner && helpReason == null,
                owner ? 0 : p.HelpCoins, owner ? 0 : p.HelpXp, null);
            animals.Add(new(animal.Id, animal.AnimalTypeId, species.DisplayName, animal.PurchasedAt, animal.MaturesAt,
                now >= animal.MaturesAt ? "adult" : species.YoungStage, dto, permissions,
                now < animal.MaturesAt ? animal.MaturesAt : cycle != null && now < cycle.ReadyAt ? cycle.ReadyAt : null));
        }
        return new(farm.Id, new(aggregate.Enclosure.Id, aggregate.Enclosure.Slot, animals), aggregate.Enclosure.Revision, now);
    }

    private async Task<string?> TheftUnavailableReasonAsync(Guid actorId, AnimalProductionCycle cycle,
        AnimalEnclosureOptions p, CancellationToken ct)
    {
        if (await db.AnimalActionCompletions.AnyAsync(c => c.ActorUserId == actorId
            && c.CycleId == cycle.Id && c.Action == "steal", ct))
            return "THEFT_ALREADY_REGISTERED";
        return AnimalRules.Stealable(cycle, p.TheftPerVisitor) == 0 ? "THEFT_LIMIT_REACHED" : null;
    }

    public async Task<AnimalHelpersPage> GetHelpersAsync(Guid actorId, Guid farmId, Guid animalId,
        Guid cycleId, string? cursor, int take, CancellationToken ct = default)
    {
        var farm = await db.Farms.AsNoTracking().SingleOrDefaultAsync(f => f.Id == farmId, ct)
            ?? throw Error("RESOURCE_NOT_FOUND", "Fazenda não encontrada.", 404);
        if (farm.UserId != actorId) throw Error("NOT_OWNER", "Somente o dono pode consultar os ajudantes.", 403);
        if (!await db.FarmAnimals.AnyAsync(a => a.Id == animalId && a.FarmId == farmId, ct)
            || !await db.AnimalProductionCycles.AnyAsync(c => c.Id == cycleId && c.FarmAnimalId == animalId, ct))
            throw Error("RESOURCE_NOT_FOUND", "Produção não encontrada.", 404);
        return await HelpersPageAsync(cycleId, cursor, Math.Clamp(take, 1, 50), ct);
    }

    private async Task<AnimalHelpersPage> HelpersPageAsync(Guid cycleId, string? cursor, int take, CancellationToken ct)
    {
        var query = db.AnimalActionCompletions.AsNoTracking().Where(c => c.CycleId == cycleId && c.Action == "help");
        var total = await query.CountAsync(ct);
        if (cursor != null)
        {
            var (at, id) = DecodeCursor(cursor);
            query = query.Where(c => EF.Functions.GreaterThan(ValueTuple.Create(c.CompletedAt, c.Id), ValueTuple.Create(at, id)));
        }
        var page = await query.OrderBy(c => c.CompletedAt).ThenBy(c => c.Id).Take(take + 1).ToListAsync(ct);
        var more = page.Count > take;
        if (more) page.RemoveAt(take);
        return new(page.Select(c => new AnimalHelperDto(c.ActorUserId, c.DisplayNameAtAction, c.CompletedAt)).ToList(),
            more ? EncodeCursor(page[^1].CompletedAt, page[^1].Id) : null, total);
    }

    private static string EncodeCursor(DateTime at, Guid id) =>
        Convert.ToBase64String(BitConverter.GetBytes(at.Ticks).Concat(id.ToByteArray()).ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static (DateTime At, Guid Id) DecodeCursor(string cursor)
    {
        try
        {
            if (cursor.Length != 32) throw new FormatException();
            var bytes = Convert.FromBase64String(cursor.Replace('-', '+').Replace('_', '/'));
            if (bytes.Length != 24) throw new FormatException();
            return (new DateTime(BitConverter.ToInt64(bytes, 0), DateTimeKind.Utc), new Guid(bytes.AsSpan(8, 16)));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        { throw Error("INVALID_REQUEST", "Cursor inválido.", 400); }
    }
}
