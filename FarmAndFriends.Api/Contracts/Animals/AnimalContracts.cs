namespace FarmAndFriends.Api.Contracts.Animals;

public sealed record BuyAnimalRequest(string AnimalTypeId);
public sealed record AnimalCycleRequest(Guid ExpectedCycleId);
public sealed record SellAnimalProductRequest(string ItemId, int Quantity);
public sealed record AnimalOperationResponse(Guid OperationId, bool Replayed,
    AnimalOperationEffect Effect, AnimalStateEnvelope? Current);
public sealed record AnimalOperationEffect(string Action, DateTime CompletedAt,
    Guid FarmId, Guid? AnimalId, Guid? CycleId, string? AnimalTypeId,
    string? ProductId, int Quantity, int CoinsSpent, int CoinsGained,
    int XpGained, int OwnerBonusAdded);
public sealed record AnimalStateEnvelope(Guid FarmId, AnimalEnclosureDto? Enclosure,
    long Revision, DateTime ObservedAt);
public sealed record AnimalEnclosureDto(Guid Id, string Slot, IReadOnlyList<AnimalDto> Animals);
public sealed record AnimalDto(Guid Id, string AnimalTypeId, string DisplayName,
    DateTime PurchasedAt, DateTime MaturesAt, string Stage, AnimalCycleDto? Cycle,
    AnimalPermissions Permissions, DateTime? NextStateChangeAt);
public sealed record AnimalCycleDto(Guid Id, string ProductId, DateTime StartsAt,
    DateTime ReadyAt, bool IsReady, int BaseYield, int ProtectedHelpBonus,
    int RemainingBase, int OwnerCollectible, int HelperCount, bool ViewerHelped,
    IReadOnlyList<AnimalHelperDto>? Helpers, string? HelpersNextCursor);
public sealed record AnimalPermissions(bool CanCollect, bool CanHelp, bool CanSteal,
    string? CollectReason, string? HelpReason, string? StealReason,
    bool RewardAvailable, int CoinsReward, int XpReward, DateTime? NextRewardAt);
public sealed record AnimalHelperDto(Guid UserId, string DisplayNameAtHelp, DateTime HelpedAt);
public sealed record AnimalHelpersPage(IReadOnlyList<AnimalHelperDto> Items, string? NextCursor, int Total);
public sealed record AnimalEnclosureOffer(int PriceCoins, int MinLevel, int CurrentLevel,
    bool FirstExpansionReached, int InitialPlotsUnlocked, int RequiredInitialPlots,
    bool Owned, bool CanBuy, string? Reason);
// Durations are seconds, interpreted only as catalog presentation. Persisted deadlines are UTC.
public sealed record AnimalOffer(string AnimalTypeId, string DisplayName, int PriceCoins,
    int MinLevel, int GrowthDuration, int ProductionDuration, bool Owned, bool CanBuy, string? Reason);
public sealed record AnimalProductOffer(string ItemId, string DisplayName, int SellPriceCoins);

public sealed class AnimalActionException(string code, string message, int status = 409)
    : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
