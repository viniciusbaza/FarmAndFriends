namespace FarmAndFriends.Api.Configuration;

public sealed class AnimalEnclosureOptions
{
    public const string SectionName = "Animals";
    // Provisional prototype tuning; no activation flag. Values are validated at startup.
    public int EnclosurePriceCoins { get; set; } = 1000;
    public int EnclosureMinLevel { get; set; } = 4;
    public int ChickenPriceCoins { get; set; } = 500;
    public int ChickenMinLevel { get; set; } = 4;
    public int GrowthSeconds { get; set; } = 14400;
    public int ProductionSeconds { get; set; } = 43200;
    public int BaseYield { get; set; } = 8;
    public int EggSellPriceCoins { get; set; } = 20;
    public int CowPriceCoins { get; set; } = 1000;
    public int CowMinLevel { get; set; } = 5;
    public int CowGrowthSeconds { get; set; } = 28800;
    public int CowProductionSeconds { get; set; } = 86400;
    public int CowBaseYield { get; set; } = 8;
    public int MilkSellPriceCoins { get; set; } = 40;
    public int SheepPriceCoins { get; set; } = 1500;
    public int SheepMinLevel { get; set; } = 6;
    public int SheepGrowthSeconds { get; set; } = 28800;
    public int SheepProductionSeconds { get; set; } = 172800;
    public int SheepBaseYield { get; set; } = 6;
    public int WoolSellPriceCoins { get; set; } = 100;
    public int SheepOwnerReserveAbsolute { get; set; } = 4;
    public decimal SheepOwnerReserveFraction { get; set; } = 0.50m;
    public int SheepTheftAggregateLimit { get; set; } = 2;
    public int SheepTheftPerVisitor { get; set; } = 1;
    public int SheepHelpBonusLimit { get; set; } = 3;
    public int SheepHelpIncrement { get; set; } = 1;
    public int SheepCollectXp { get; set; } = 40;
    public int PeacockPriceCoins { get; set; } = 2500;
    public int PeacockMinLevel { get; set; } = 8;
    public int PeacockGrowthSeconds { get; set; } = 28800;
    public int PeacockProductionSeconds { get; set; } = 259200;
    public int PeacockBaseYield { get; set; } = 6;
    public int FeatherSellPriceCoins { get; set; } = 150;
    public int PeacockOwnerReserveAbsolute { get; set; } = 4;
    public decimal PeacockOwnerReserveFraction { get; set; } = 0.50m;
    public int PeacockTheftAggregateLimit { get; set; } = 2;
    public int PeacockTheftPerVisitor { get; set; } = 1;
    public int PeacockHelpBonusLimit { get; set; } = 3;
    public int PeacockHelpIncrement { get; set; } = 1;
    public int PeacockCollectXp { get; set; } = 40;
    public int PigPriceCoins { get; set; } = 2000;
    public int PigMinLevel { get; set; } = 7;
    public int PigGrowthSeconds { get; set; } = 28800;
    public int PigProductionSeconds { get; set; } = 86400;
    public int PigBaseYield { get; set; } = 4;
    public int PigHelpBonusLimit { get; set; } = 2;
    public int PigHelpIncrement { get; set; } = 1;
    public int PigCollectXp { get; set; } = 25;
    public int HelpBonusLimit { get; set; } = 3;
    public int HelpIncrement { get; set; } = 1;
    public int OwnerReserveAbsolute { get; set; } = 4;
    public decimal OwnerReserveFraction { get; set; } = 0.50m;
    public int TheftAggregateLimit { get; set; } = 4;
    public int TheftPerVisitor { get; set; } = 1;
    public int HelpCoins { get; set; } = 2;
    public int HelpXp { get; set; } = 5;
    public int CollectXp { get; set; } = 25;
    public int TheftXp { get; set; } = 0;

    public bool IsValid() => EnclosurePriceCoins > 0 && EnclosureMinLevel > 0
        && ChickenPriceCoins > 0 && ChickenMinLevel > 0 && EggSellPriceCoins > 0
        && CowPriceCoins > 0 && CowMinLevel > 0 && MilkSellPriceCoins > 0
        && CowGrowthSeconds is > 0 and <= 31536000 && CowProductionSeconds is > 0 and <= 31536000
        && CowBaseYield is > 0 and <= 1000000
        && SheepPriceCoins > 0 && SheepMinLevel > 0 && WoolSellPriceCoins > 0
        && SheepGrowthSeconds is > 0 and <= 31536000 && SheepProductionSeconds is > 0 and <= 31536000
        && SheepBaseYield is > 0 and <= 1000000 && SheepOwnerReserveAbsolute > 0
        && SheepOwnerReserveFraction is > 0 and <= 1
        && SheepTheftAggregateLimit >= 0 && SheepTheftPerVisitor > 0
        && SheepHelpBonusLimit is > 0 and <= 1000000 && SheepHelpIncrement > 0 && SheepCollectXp >= 0
        && PeacockPriceCoins > 0 && PeacockMinLevel > 0 && FeatherSellPriceCoins > 0
        && PeacockGrowthSeconds is > 0 and <= 31536000 && PeacockProductionSeconds is > 0 and <= 31536000
        && PeacockBaseYield is > 0 and <= 1000000 && PeacockOwnerReserveAbsolute > 0
        && PeacockOwnerReserveFraction is > 0 and <= 1
        && PeacockTheftAggregateLimit >= 0 && PeacockTheftPerVisitor > 0
        && PeacockHelpBonusLimit is > 0 and <= 1000000 && PeacockHelpIncrement > 0 && PeacockCollectXp >= 0
        && PigPriceCoins > 0 && PigMinLevel > 0
        && PigGrowthSeconds is > 0 and <= 31536000 && PigProductionSeconds is > 0 and <= 31536000
        && PigBaseYield is > 0 and <= 1000000 && PigHelpBonusLimit is > 0 and <= 1000000
        && PigHelpIncrement > 0 && PigCollectXp >= 0
        && GrowthSeconds is > 0 and <= 31536000 && ProductionSeconds is > 0 and <= 31536000
        && BaseYield is > 0 and <= 1000000 && HelpBonusLimit is > 0 and <= 1000000
        && HelpIncrement > 0 && OwnerReserveAbsolute > 0
        && OwnerReserveFraction is > 0 and <= 1 && TheftAggregateLimit >= 0
        && TheftPerVisitor > 0
        && HelpCoins > 0 && HelpXp > 0 && CollectXp >= 0 && TheftXp >= 0;

    // Freeze the effective production promise using the existing snapshot fields.
    // Older chicken JSON remains readable without changing its stored values.
    public AnimalEnclosureOptions ForSpecies(string animalTypeId)
    {
        var snapshot = (AnimalEnclosureOptions)MemberwiseClone();
        if (animalTypeId == "cow")
        {
            snapshot.GrowthSeconds = CowGrowthSeconds;
            snapshot.ProductionSeconds = CowProductionSeconds;
            snapshot.BaseYield = CowBaseYield;
        }
        else if (animalTypeId == "sheep")
        {
            snapshot.GrowthSeconds = SheepGrowthSeconds;
            snapshot.ProductionSeconds = SheepProductionSeconds;
            snapshot.BaseYield = SheepBaseYield;
            snapshot.OwnerReserveAbsolute = SheepOwnerReserveAbsolute;
            snapshot.OwnerReserveFraction = SheepOwnerReserveFraction;
            snapshot.TheftAggregateLimit = SheepTheftAggregateLimit;
            snapshot.TheftPerVisitor = SheepTheftPerVisitor;
            snapshot.HelpBonusLimit = SheepHelpBonusLimit;
            snapshot.HelpIncrement = SheepHelpIncrement;
            snapshot.CollectXp = SheepCollectXp;
        }
        else if (animalTypeId == "peacock")
        {
            snapshot.GrowthSeconds = PeacockGrowthSeconds;
            snapshot.ProductionSeconds = PeacockProductionSeconds;
            snapshot.BaseYield = PeacockBaseYield;
            snapshot.OwnerReserveAbsolute = PeacockOwnerReserveAbsolute;
            snapshot.OwnerReserveFraction = PeacockOwnerReserveFraction;
            snapshot.TheftAggregateLimit = PeacockTheftAggregateLimit;
            snapshot.TheftPerVisitor = PeacockTheftPerVisitor;
            snapshot.HelpBonusLimit = PeacockHelpBonusLimit;
            snapshot.HelpIncrement = PeacockHelpIncrement;
            snapshot.CollectXp = PeacockCollectXp;
        }
        else if (animalTypeId == "pig")
        {
            snapshot.GrowthSeconds = PigGrowthSeconds;
            snapshot.ProductionSeconds = PigProductionSeconds;
            snapshot.BaseYield = PigBaseYield;
            snapshot.TheftAggregateLimit = 0;
            snapshot.HelpBonusLimit = PigHelpBonusLimit;
            snapshot.HelpIncrement = PigHelpIncrement;
            snapshot.CollectXp = PigCollectXp;
        }
        return snapshot;
    }
}
