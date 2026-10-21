namespace FarmAndFriends.Api.Domain.Entities;

public sealed class AnimalEnclosure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public DateTime PurchasedAt { get; set; }
    public string Slot { get; set; } = "upper-right";
    public long Revision { get; set; } = 1;
}

public sealed class FarmAnimal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public Guid AnimalEnclosureId { get; set; }
    public string AnimalTypeId { get; set; } = "chicken";
    public DateTime PurchasedAt { get; set; }
    public DateTime MaturesAt { get; set; }
    public string InitialPolicyJson { get; set; } = null!;
}

public sealed class AnimalProductionCycle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmAnimalId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ReadyAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public string ProductId { get; set; } = "egg";
    public string PolicyJson { get; set; } = null!;
    public int BaseYield { get; set; }
    public int ProtectedHelpBonus { get; set; }
    public int StolenBase { get; set; }
    public int OwnerReserve { get; set; }
    public int TheftLimit { get; set; }
    public int HelpBonusLimit { get; set; }
}

public sealed class AnimalActionCompletion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid FarmId { get; set; }
    public Guid? AnimalId { get; set; }
    public Guid? CycleId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string Fingerprint { get; set; } = null!;
    public string Action { get; set; } = null!;
    public DateTime CompletedAt { get; set; }
    public string DisplayNameAtAction { get; set; } = null!;
    public string EffectJson { get; set; } = null!;
    public int CoinsGained { get; set; }
    public int XpGained { get; set; }
}
