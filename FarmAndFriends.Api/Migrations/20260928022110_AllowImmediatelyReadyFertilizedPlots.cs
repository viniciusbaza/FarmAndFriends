using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowImmediatelyReadyFertilizedPlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Plots_CurrentHarvestCycleStartedAt_Consistent",
                table: "Plots");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plots_CurrentHarvestCycleStartedAt_Consistent",
                table: "Plots",
                sql: "(\"SeedId\" IS NULL AND \"PlantedAt\" IS NULL AND \"ReadyAt\" IS NULL AND \"RemainingYield\" IS NULL AND \"CurrentHarvestCycleStartedAt\" IS NULL) OR (\"SeedId\" IS NOT NULL AND \"PlantedAt\" IS NOT NULL AND \"ReadyAt\" IS NOT NULL AND \"RemainingYield\" IS NOT NULL AND \"CurrentHarvestCycleStartedAt\" IS NOT NULL AND \"ReadyAt\" >= \"CurrentHarvestCycleStartedAt\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Plots"
                        WHERE "SeedId" IS NOT NULL
                          AND "ReadyAt" = "CurrentHarvestCycleStartedAt"
                    ) THEN
                        RAISE EXCEPTION 'Cannot restore the old plot timing constraint while an immediately ready crop remains planted. Harvest these ready crops first.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Plots_CurrentHarvestCycleStartedAt_Consistent",
                table: "Plots");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plots_CurrentHarvestCycleStartedAt_Consistent",
                table: "Plots",
                sql: "(\"SeedId\" IS NULL AND \"PlantedAt\" IS NULL AND \"ReadyAt\" IS NULL AND \"RemainingYield\" IS NULL AND \"CurrentHarvestCycleStartedAt\" IS NULL) OR (\"SeedId\" IS NOT NULL AND \"PlantedAt\" IS NOT NULL AND \"ReadyAt\" IS NOT NULL AND \"RemainingYield\" IS NOT NULL AND \"CurrentHarvestCycleStartedAt\" IS NOT NULL AND \"ReadyAt\" > \"CurrentHarvestCycleStartedAt\")");
        }
    }
}
