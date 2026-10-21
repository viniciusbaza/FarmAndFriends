namespace FarmAndFriends.Api.Domain.Entities;

public sealed class FertilizerApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid FarmId { get; set; }
    public Guid PlotId { get; set; }
    public int HarvestCycle { get; set; }
    public DateTime CycleStartedAt { get; set; }
    public Guid IdempotencyKey { get; set; }
    public DateTime AppliedAt { get; set; }
    public DateTime PreviousReadyAt { get; set; }
    public DateTime ReadyAt { get; set; }
    public int RemainingQuantityAfter { get; set; }
    public string ActorUsernameAtAction { get; set; } = null!;
}
