using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Domain.Entities;

namespace FarmAndFriends.Api.Domain.Rules;

public static class AnimalRules
{
    public static bool FirstExpansionReached(IReadOnlyCollection<Plot> plots) =>
        plots.Count == FarmLayoutRules.MaxPlotCount
        && FarmLayoutRules.Evaluate(plots).Status != FarmLayoutStatus.Unsupported
        && plots.Count(p => p.Unlocked) >= FarmLayoutRules.InitialPlotCount;

    public static int OwnerReserve(AnimalEnclosureOptions policy) =>
        Math.Min(policy.BaseYield, Math.Max(policy.OwnerReserveAbsolute,
            checked((int)decimal.Ceiling(policy.OwnerReserveFraction * policy.BaseYield))));

    public static int TheftLimit(AnimalEnclosureOptions policy) =>
        Math.Min(policy.TheftAggregateLimit, policy.BaseYield - OwnerReserve(policy));

    public static int Stealable(AnimalProductionCycle cycle, int individualLimit) =>
        Math.Max(0, Math.Min(individualLimit, Math.Min(cycle.TheftLimit - cycle.StolenBase,
            cycle.BaseYield - cycle.StolenBase - cycle.OwnerReserve)));

    public static int HelpBonusAdded(AnimalProductionCycle cycle, int increment) =>
        Math.Max(0, Math.Min(increment, cycle.HelpBonusLimit - cycle.ProtectedHelpBonus));
}
