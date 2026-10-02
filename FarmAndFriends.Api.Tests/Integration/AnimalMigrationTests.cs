using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Domain.Enums;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace FarmAndFriends.Api.Tests.Integration;

public sealed class AnimalMigrationTests
{
    [PostgresFact]
    public async Task IncrementalMigration_PreservesExistingPlayerEconomyAndPlotsWithoutGrantingAnimals()
    {
        var configured = Environment.GetEnvironmentVariable("CROP_CARE_TEST_CONNECTION")!;
        // Generated database lives exclusively on the configured isolated test server.
        var name = $"animal_upgrade_{Guid.NewGuid():N}";
        var adminString = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" }.ConnectionString;
        var testString = new NpgsqlConnectionStringBuilder(configured) { Database = name }.ConnectionString;
        await using var admin = new NpgsqlConnection(adminString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(testString).Options);
            await db.GetService<IMigrator>().MigrateAsync("20260904022050_AddUserAvatar");
            var owner = new User { Id = Guid.NewGuid(), Username = "upgrade_animal", NormalizedUsername = "UPGRADE_ANIMAL", PasswordHash = "test", Level = 4, CurrentXp = 32 };
            var farm = new Farm { Id = Guid.NewGuid(), UserId = owner.Id, Name = "Old farm" };
            var inventory = new Inventory { Id = Guid.NewGuid(), UserId = owner.Id, Coins = 7654, PremiumCoins = 17 };
            var plantedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            var plot = new Plot { Id = Guid.NewGuid(), FarmId = farm.Id, X = 0, Y = 0, Unlocked = true,
                SeedId = "carrot", RemainingYield = 3, PlantedAt = plantedAt,
                CurrentHarvestCycle = 1, CurrentHarvestCycleStartedAt = plantedAt, ReadyAt = plantedAt.AddHours(1) };
            db.AddRange(owner, farm, inventory, plot);
            db.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), InventoryId = inventory.Id, ItemType = ItemType.Seed, ItemId = "carrot", Quantity = 6 });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.MigrateAsync();
            Assert.False(await db.AnimalEnclosures.AnyAsync()); Assert.False(await db.FarmAnimals.AnyAsync());
            Assert.False(await db.AnimalProductionCycles.AnyAsync()); Assert.False(await db.AnimalActionCompletions.AnyAsync());
            var preserved = await db.Inventories.SingleAsync(i => i.UserId == owner.Id);
            Assert.Equal(7654, preserved.Coins); Assert.Equal(17, preserved.PremiumCoins);
            Assert.Equal(32, (await db.Users.SingleAsync(u => u.Id == owner.Id)).CurrentXp);
            Assert.Equal(3, (await db.Plots.SingleAsync(p => p.Id == plot.Id)).RemainingYield);
            Assert.Equal(6, (await db.InventoryItems.SingleAsync()).Quantity);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
