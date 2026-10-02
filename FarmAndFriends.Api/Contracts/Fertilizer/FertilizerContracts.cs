namespace FarmAndFriends.Api.Contracts.Fertilizer;

public sealed record FertilizerOffer(string ItemId, string DisplayName, int PricePremiumCoins,
    int MaxReductionSeconds, int MinRemainingSeconds);
public sealed record BuyFertilizerRequest(int Quantity);
public sealed record BuyFertilizerResponse(Guid OperationId, bool Replayed, string ItemId,
    int Quantity, int RemainingQuantity, int PremiumCoins);
public sealed record FertilizePlotRequest(int ExpectedHarvestCycle, DateTime ExpectedCycleStartedAt);
public sealed record FertilizePlotResponse(Guid OperationId, bool Replayed, DateTime PreviousReadyAt,
    DateTime ReadyAt, int RemainingQuantity);
