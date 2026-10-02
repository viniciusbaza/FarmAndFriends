namespace FarmAndFriends.Api.Configuration;

public sealed class FertilizerOptions
{
    public const string SectionName = "Fertilizer";
    public const string ItemId = "fertilizer";
    public int PremiumPrice { get; set; } = 2;
    public int MaxPurchaseQuantity { get; set; } = 10000;
    public int MaximumReductionSeconds { get; set; } = 3600;
    public int MinimumRemainingSeconds { get; set; } = 0;
    public bool IsValid() => PremiumPrice > 0 && MaxPurchaseQuantity > 0
        && MaximumReductionSeconds > 0 && MinimumRemainingSeconds >= 0;
}
