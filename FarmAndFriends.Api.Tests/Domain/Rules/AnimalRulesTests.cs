using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Rules;
using Xunit;

namespace FarmAndFriends.Api.Tests.Domain.Rules;

public sealed class AnimalRulesTests
{
    [Theory]
    [InlineData(9)] [InlineData(10)] [InlineData(28)]
    public void FirstExpansion_AcceptsEverySupportedExpandedStage(int unlocked)
    {
        var plots = Enumerable.Range(1, 28).Select(n =>
        {
            var d = FarmLayoutRules.GetDefinition(n);
            return new Plot { X = d.X, Y = d.Y, Unlocked = n <= unlocked };
        }).ToList();
        Assert.True(AnimalRules.FirstExpansionReached(plots));
        plots[0].Unlocked = false;
        Assert.False(AnimalRules.FirstExpansionReached(plots));
    }

    [Fact]
    public void Production_AlwaysProtectsReserveAndAllHelpBonus()
    {
        var policy = new AnimalEnclosureOptions();
        Assert.True(policy.IsValid());
        Assert.Equal(4, AnimalRules.OwnerReserve(policy));
        Assert.Equal(4, AnimalRules.TheftLimit(policy));
        var cycle = new AnimalProductionCycle { BaseYield = 8, OwnerReserve = 4,
            TheftLimit = 4, HelpBonusLimit = 3, ProtectedHelpBonus = 2 };
        Assert.Equal(1, AnimalRules.HelpBonusAdded(cycle, 2));
        cycle.ProtectedHelpBonus = 3;
        Assert.Equal(0, AnimalRules.HelpBonusAdded(cycle, 2));
        Assert.Equal(4, AnimalRules.Stealable(cycle, 999));
        cycle.StolenBase = 4;
        Assert.Equal(0, AnimalRules.Stealable(cycle, 999));
        Assert.Equal(7, cycle.BaseYield - cycle.StolenBase + cycle.ProtectedHelpBonus);
        policy.BaseYield = 1;
        Assert.Equal(0, AnimalRules.TheftLimit(policy));
    }

    [Fact]
    public void Configuration_RejectsBrokenEconomy()
    {
        Assert.False(new AnimalEnclosureOptions { HelpIncrement = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { HelpBonusLimit = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { OwnerReserveFraction = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { ProductionSeconds = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { CowProductionSeconds = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { CowGrowthSeconds = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { CowBaseYield = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { MilkSellPriceCoins = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { SheepProductionSeconds = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { SheepBaseYield = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { SheepOwnerReserveAbsolute = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { SheepOwnerReserveFraction = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { SheepTheftPerVisitor = 0 }.IsValid());
        Assert.False(new AnimalEnclosureOptions { WoolSellPriceCoins = 0 }.IsValid());
    }

    [Fact]
    public void HistoricalChickenSnapshot_DoesNotAcquireCowProductionValues()
    {
        var historical = System.Text.Json.JsonSerializer.Deserialize<AnimalEnclosureOptions>(
            "{\"GrowthSeconds\":123,\"ProductionSeconds\":456,\"BaseYield\":7,\"GlobalHelpLimit\":10,\"PairHelpLimit\":2}")!;
        Assert.True(historical.IsValid());
        var chicken = historical.ForSpecies("chicken");
        Assert.Equal(123, chicken.GrowthSeconds);
        Assert.Equal(456, chicken.ProductionSeconds);
        Assert.Equal(7, chicken.BaseYield);
        var cow = historical.ForSpecies("cow");
        Assert.Equal(86400, cow.ProductionSeconds);
        Assert.Equal(456, historical.ProductionSeconds);
    }

    [Fact]
    public void SheepPolicy_UsesOwnConfigurableProductionAndProtectsFourOfSixWool()
    {
        var settings = new AnimalEnclosureOptions();
        var sheep = settings.ForSpecies("sheep");
        Assert.True(settings.IsValid());
        Assert.Equal(28800, sheep.GrowthSeconds);
        Assert.Equal(172800, sheep.ProductionSeconds);
        Assert.Equal(6, sheep.BaseYield);
        Assert.Equal(4, AnimalRules.OwnerReserve(sheep));
        Assert.Equal(2, AnimalRules.TheftLimit(sheep));
        Assert.Equal(1, sheep.TheftPerVisitor);
        Assert.Equal(3, sheep.HelpBonusLimit);
        Assert.Equal(1, sheep.HelpIncrement);
        Assert.Equal(40, sheep.CollectXp);
        Assert.Equal(8, settings.BaseYield);
        Assert.Equal(43200, settings.ProductionSeconds);

        settings.SheepTheftAggregateLimit = 1;
        settings.SheepCollectXp = 50;
        settings.OwnerReserveFraction = 1m;
        var adjusted = settings.ForSpecies("sheep");
        Assert.Equal(1, AnimalRules.TheftLimit(adjusted));
        Assert.Equal(4, AnimalRules.OwnerReserve(adjusted));
        Assert.Equal(0.50m, adjusted.OwnerReserveFraction);
        Assert.Equal(50, adjusted.CollectXp);
    }
}
