using FarmAndFriends.Api.Configuration;

namespace FarmAndFriends.Api.Domain.Rules;

public sealed record AnimalSpeciesDefinition(string Id, string DisplayName, string YoungStage,
    string ProductId, string ProductDisplayName, string ProductUnits);

public static class AnimalSpeciesRules
{
    public static IReadOnlyList<AnimalSpeciesDefinition> Available { get; } = Array.AsReadOnly(new[]
    {
        new AnimalSpeciesDefinition("chicken", "Galinha", "chick", "egg", "Ovos", "ovo(s)"),
        new AnimalSpeciesDefinition("cow", "Vaca", "calf", "milk", "Leite", "leite(s)"),
        new AnimalSpeciesDefinition("sheep", "Ovelha", "lamb", "wool", "Lã", "lã(s)"),
        new AnimalSpeciesDefinition("pig", "Porco", "piglet", "fertilizer", "Adubo", "adubo(s)")
    });

    public static AnimalSpeciesDefinition? Find(string? id) => Available.SingleOrDefault(s => s.Id == id);
    public static AnimalSpeciesDefinition? FindProduct(string? id) => Available.SingleOrDefault(s => s.ProductId == id && s.Id != "pig");
    public static int Price(AnimalSpeciesDefinition species, AnimalEnclosureOptions policy) =>
        species.Id switch
        {
            "cow" => policy.CowPriceCoins,
            "sheep" => policy.SheepPriceCoins,
            "pig" => policy.PigPriceCoins,
            _ => policy.ChickenPriceCoins
        };
    public static int MinLevel(AnimalSpeciesDefinition species, AnimalEnclosureOptions policy) =>
        species.Id switch
        {
            "cow" => policy.CowMinLevel,
            "sheep" => policy.SheepMinLevel,
            "pig" => policy.PigMinLevel,
            _ => policy.ChickenMinLevel
        };
    public static int SellPrice(AnimalSpeciesDefinition species, AnimalEnclosureOptions policy) =>
        species.Id switch
        {
            "cow" => policy.MilkSellPriceCoins,
            "sheep" => policy.WoolSellPriceCoins,
            _ => policy.EggSellPriceCoins
        };
}
