using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M5PlanReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExitCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhaseId = table.Column<int>(type: "int", nullable: false),
                    Group = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceHint = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Done = table.Column<bool>(type: "bit", nullable: false),
                    DoneOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EvidenceText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SeedKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsUserModified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExitCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExitCriteria_Phases_PhaseId",
                        column: x => x.PhaseId,
                        principalTable: "Phases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    PhaseProgressMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    MistakeThemesMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    PostingsCaptured = table.Column<int>(type: "int", nullable: false),
                    LighterWeekNeeded = table.Column<bool>(type: "bit", nullable: false),
                    SomedayTriageMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplanEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EvidenceMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    DecisionMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    CheckpointId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplanEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReplanEvents_Checkpoints_CheckpointId",
                        column: x => x.CheckpointId,
                        principalTable: "Checkpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SomedayItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NotesMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReviewedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PromotedTaskId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SomedayItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyHourTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Categories = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MinHours = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxHours = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsTotal = table.Column<bool>(type: "bit", nullable: false),
                    IsStretch = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SeedKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsUserModified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyHourTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    Energy = table.Column<int>(type: "int", nullable: true),
                    Sleep = table.Column<int>(type: "int", nullable: true),
                    Mood = table.Column<int>(type: "int", nullable: true),
                    ShippedMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    BlockedMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    NextSliceMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    RecoveryNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RepeatedRequirement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Start = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Stop = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Keep = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExitCriteria_PhaseId",
                table: "ExitCriteria",
                column: "PhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExitCriteria_SeedKey",
                table: "ExitCriteria",
                column: "SeedKey",
                unique: true,
                filter: "[SeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyReviews_Month",
                table: "MonthlyReviews",
                column: "Month",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReplanEvents_CheckpointId",
                table: "ReplanEvents",
                column: "CheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplanEvents_Date",
                table: "ReplanEvents",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_SomedayItems_Status",
                table: "SomedayItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyHourTargets_SeedKey",
                table: "WeeklyHourTargets",
                column: "SeedKey",
                unique: true,
                filter: "[SeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReviews_WeekStart",
                table: "WeeklyReviews",
                column: "WeekStart",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExitCriteria");

            migrationBuilder.DropTable(
                name: "MonthlyReviews");

            migrationBuilder.DropTable(
                name: "ReplanEvents");

            migrationBuilder.DropTable(
                name: "SomedayItems");

            migrationBuilder.DropTable(
                name: "WeeklyHourTargets");

            migrationBuilder.DropTable(
                name: "WeeklyReviews");
        }
    }
}
