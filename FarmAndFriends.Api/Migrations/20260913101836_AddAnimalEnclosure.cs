using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmAndFriends.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalEnclosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AnimalCompletionId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnimalCycleId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnimalId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnimalEnclosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Slot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalEnclosures", x => x.Id);
                    table.UniqueConstraint("AK_AnimalEnclosures_Id_FarmId", x => new { x.Id, x.FarmId });
                    table.CheckConstraint("CK_AnimalEnclosures_Revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_AnimalEnclosures_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FarmAnimals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalEnclosureId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalTypeId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaturesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InitialPolicyJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmAnimals", x => x.Id);
                    table.CheckConstraint("CK_FarmAnimals_Maturity", "\"MaturesAt\" > \"PurchasedAt\"");
                    table.ForeignKey(
                        name: "FK_FarmAnimals_AnimalEnclosures_AnimalEnclosureId_FarmId",
                        columns: x => new { x.AnimalEnclosureId, x.FarmId },
                        principalTable: "AnimalEnclosures",
                        principalColumns: new[] { "Id", "FarmId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimalProductionCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmAnimalId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProductId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PolicyJson = table.Column<string>(type: "text", nullable: false),
                    BaseYield = table.Column<int>(type: "integer", nullable: false),
                    ProtectedHelpBonus = table.Column<int>(type: "integer", nullable: false),
                    StolenBase = table.Column<int>(type: "integer", nullable: false),
                    OwnerReserve = table.Column<int>(type: "integer", nullable: false),
                    TheftLimit = table.Column<int>(type: "integer", nullable: false),
                    HelpBonusLimit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalProductionCycles", x => x.Id);
                    table.CheckConstraint("CK_AnimalCycles_Time", "\"ReadyAt\" > \"StartsAt\" AND (\"CollectedAt\" IS NULL OR \"CollectedAt\" >= \"ReadyAt\")");
                    table.CheckConstraint("CK_AnimalCycles_Yield", "\"BaseYield\" > 0 AND \"OwnerReserve\" > 0 AND \"OwnerReserve\" <= \"BaseYield\" AND \"TheftLimit\" >= 0 AND \"TheftLimit\" <= \"BaseYield\" - \"OwnerReserve\" AND \"StolenBase\" >= 0 AND \"StolenBase\" <= \"TheftLimit\" AND \"HelpBonusLimit\" > 0 AND \"ProtectedHelpBonus\" >= 0 AND \"ProtectedHelpBonus\" <= \"HelpBonusLimit\"");
                    table.ForeignKey(
                        name: "FK_AnimalProductionCycles_FarmAnimals_FarmAnimalId",
                        column: x => x.FarmAnimalId,
                        principalTable: "FarmAnimals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimalActionCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayNameAtAction = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EffectJson = table.Column<string>(type: "text", nullable: false),
                    CoinsGained = table.Column<int>(type: "integer", nullable: false),
                    XpGained = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalActionCompletions", x => x.Id);
                    table.CheckConstraint("CK_AnimalCompletions_Rewards", "\"CoinsGained\" >= 0 AND \"XpGained\" >= 0");
                    table.ForeignKey(
                        name: "FK_AnimalActionCompletions_AnimalProductionCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "AnimalProductionCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalActionCompletions_FarmAnimals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "FarmAnimals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalActionCompletions_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalActionCompletions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalActionCompletions_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AnimalCompletionId",
                table: "Notifications",
                column: "AnimalCompletionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AnimalCycleId",
                table: "Notifications",
                column: "AnimalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AnimalId",
                table: "Notifications",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_ActorUserId_Action_CompletedAt",
                table: "AnimalActionCompletions",
                columns: new[] { "ActorUserId", "Action", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_ActorUserId_CycleId_Action",
                table: "AnimalActionCompletions",
                columns: new[] { "ActorUserId", "CycleId", "Action" },
                unique: true,
                filter: "\"Action\" IN ('help', 'steal')");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_ActorUserId_IdempotencyKey",
                table: "AnimalActionCompletions",
                columns: new[] { "ActorUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_ActorUserId_OwnerUserId_Action_Comp~",
                table: "AnimalActionCompletions",
                columns: new[] { "ActorUserId", "OwnerUserId", "Action", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_AnimalId",
                table: "AnimalActionCompletions",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_CycleId",
                table: "AnimalActionCompletions",
                column: "CycleId",
                unique: true,
                filter: "\"Action\" = 'collect'");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_CycleId_CompletedAt_Id",
                table: "AnimalActionCompletions",
                columns: new[] { "CycleId", "CompletedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_FarmId",
                table: "AnimalActionCompletions",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalActionCompletions_OwnerUserId",
                table: "AnimalActionCompletions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalEnclosures_FarmId",
                table: "AnimalEnclosures",
                column: "FarmId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalProductionCycles_FarmAnimalId",
                table: "AnimalProductionCycles",
                column: "FarmAnimalId",
                unique: true,
                filter: "\"CollectedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalProductionCycles_FarmAnimalId_StartsAt",
                table: "AnimalProductionCycles",
                columns: new[] { "FarmAnimalId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmAnimals_AnimalEnclosureId_FarmId",
                table: "FarmAnimals",
                columns: new[] { "AnimalEnclosureId", "FarmId" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmAnimals_FarmId_AnimalTypeId",
                table: "FarmAnimals",
                columns: new[] { "FarmId", "AnimalTypeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_AnimalActionCompletions_AnimalCompletionId",
                table: "Notifications",
                column: "AnimalCompletionId",
                principalTable: "AnimalActionCompletions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_AnimalProductionCycles_AnimalCycleId",
                table: "Notifications",
                column: "AnimalCycleId",
                principalTable: "AnimalProductionCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_FarmAnimals_AnimalId",
                table: "Notifications",
                column: "AnimalId",
                principalTable: "FarmAnimals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_AnimalActionCompletions_AnimalCompletionId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_AnimalProductionCycles_AnimalCycleId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_FarmAnimals_AnimalId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "AnimalActionCompletions");

            migrationBuilder.DropTable(
                name: "AnimalProductionCycles");

            migrationBuilder.DropTable(
                name: "FarmAnimals");

            migrationBuilder.DropTable(
                name: "AnimalEnclosures");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_AnimalCompletionId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_AnimalCycleId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_AnimalId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AnimalCompletionId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AnimalCycleId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AnimalId",
                table: "Notifications");
        }
    }
}
