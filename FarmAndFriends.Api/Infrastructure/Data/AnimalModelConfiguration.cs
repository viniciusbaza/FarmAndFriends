using FarmAndFriends.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Infrastructure.Data;

internal static class AnimalModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<AnimalEnclosure>(e =>
        {
            e.HasIndex(x => x.FarmId).IsUnique();
            e.HasAlternateKey(x => new { x.Id, x.FarmId });
            e.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Slot).HasMaxLength(32);
            e.ToTable(t => t.HasCheckConstraint("CK_AnimalEnclosures_Revision", "\"Revision\" > 0"));
        });
        model.Entity<FarmAnimal>(e =>
        {
            e.HasIndex(x => new { x.FarmId, x.AnimalTypeId }).IsUnique();
            e.HasOne<AnimalEnclosure>().WithMany()
                .HasForeignKey(x => new { x.AnimalEnclosureId, x.FarmId })
                .HasPrincipalKey(x => new { x.Id, x.FarmId }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.AnimalTypeId).HasMaxLength(32);
            e.ToTable(t => t.HasCheckConstraint("CK_FarmAnimals_Maturity", "\"MaturesAt\" > \"PurchasedAt\""));
        });
        model.Entity<AnimalProductionCycle>(e =>
        {
            e.HasOne<FarmAnimal>().WithMany().HasForeignKey(x => x.FarmAnimalId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.FarmAnimalId).IsUnique().HasFilter("\"CollectedAt\" IS NULL");
            e.HasIndex(x => new { x.FarmAnimalId, x.StartsAt });
            e.Property(x => x.ProductId).HasMaxLength(32);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AnimalCycles_Time", "\"ReadyAt\" > \"StartsAt\" AND (\"CollectedAt\" IS NULL OR \"CollectedAt\" >= \"ReadyAt\")");
                t.HasCheckConstraint("CK_AnimalCycles_Yield", "\"BaseYield\" > 0 AND \"OwnerReserve\" > 0 AND \"OwnerReserve\" <= \"BaseYield\" AND \"TheftLimit\" >= 0 AND \"TheftLimit\" <= \"BaseYield\" - \"OwnerReserve\" AND \"StolenBase\" >= 0 AND \"StolenBase\" <= \"TheftLimit\" AND \"HelpBonusLimit\" > 0 AND \"ProtectedHelpBonus\" >= 0 AND \"ProtectedHelpBonus\" <= \"HelpBonusLimit\"");
            });
        });
        model.Entity<AnimalActionCompletion>(e =>
        {
            e.HasIndex(x => new { x.ActorUserId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.ActorUserId, x.CycleId, x.Action }).IsUnique()
                .HasFilter("\"Action\" IN ('help', 'steal')");
            e.HasIndex(x => x.CycleId).IsUnique().HasFilter("\"Action\" = 'collect'");
            e.HasIndex(x => new { x.ActorUserId, x.Action, x.CompletedAt });
            e.HasIndex(x => new { x.ActorUserId, x.OwnerUserId, x.Action, x.CompletedAt });
            e.HasIndex(x => new { x.CycleId, x.CompletedAt, x.Id });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FarmAnimal>().WithMany().HasForeignKey(x => x.AnimalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AnimalProductionCycle>().WithMany().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Action).HasMaxLength(32);
            e.Property(x => x.DisplayNameAtAction).HasMaxLength(256);
            e.ToTable(t => t.HasCheckConstraint("CK_AnimalCompletions_Rewards", "\"CoinsGained\" >= 0 AND \"XpGained\" >= 0"));
        });
        model.Entity<Notification>(e =>
        {
            e.HasIndex(x => x.AnimalCompletionId).IsUnique();
            e.HasOne<FarmAnimal>().WithMany().HasForeignKey(x => x.AnimalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AnimalProductionCycle>().WithMany().HasForeignKey(x => x.AnimalCycleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AnimalActionCompletion>().WithMany().HasForeignKey(x => x.AnimalCompletionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
