using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4Ddia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticulationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AnswerMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    TradeOffsMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    EvidenceMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    SelfRating = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Minutes = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticulationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticulationPrompts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ArticulationPrompts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DdiaChapters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Edition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlannedWeekFrom = table.Column<int>(type: "int", nullable: false),
                    PlannedWeekTo = table.Column<int>(type: "int", nullable: false),
                    Topics = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AskOpsConnection = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TotalPages = table.Column<int>(type: "int", nullable: true),
                    PagesRead = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FinishedOn = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_DdiaChapters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DesignDrills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Group = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlannedWeek = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClarifyNotes = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    EstimateNotes = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    ApiDataNotes = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    HighLevelNotes = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    DeepDiveNotes = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    TradeOffNotes = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    SpokenMock = table.Column<bool>(type: "bit", nullable: false),
                    PracticedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Minutes = table.Column<int>(type: "int", nullable: false),
                    ReviewedOn = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_DesignDrills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DdiaNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChapterId = table.Column<int>(type: "int", nullable: false),
                    KeyIdea1 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KeyIdea2 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KeyIdea3 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KeyIdea4 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KeyIdea5 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TradeOffsMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    FailureModesMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    AskOpsDecision = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExplanationRating = table.Column<int>(type: "int", nullable: true),
                    ExplanationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DdiaNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DdiaNotes_DdiaChapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "DdiaChapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadingLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ChapterId = table.Column<int>(type: "int", nullable: false),
                    PagesRead = table.Column<int>(type: "int", nullable: false),
                    Minutes = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingLogs_DdiaChapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "DdiaChapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewQas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DdiaNoteId = table.Column<int>(type: "int", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Answer60sMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewQas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewQas_DdiaNotes_DdiaNoteId",
                        column: x => x.DdiaNoteId,
                        principalTable: "DdiaNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticulationLogs_Date",
                table: "ArticulationLogs",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ArticulationPrompts_SeedKey",
                table: "ArticulationPrompts",
                column: "SeedKey",
                unique: true,
                filter: "[SeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DdiaChapters_Edition_Number",
                table: "DdiaChapters",
                columns: new[] { "Edition", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_DdiaChapters_SeedKey",
                table: "DdiaChapters",
                column: "SeedKey",
                unique: true,
                filter: "[SeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DdiaNotes_ChapterId",
                table: "DdiaNotes",
                column: "ChapterId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DesignDrills_Number",
                table: "DesignDrills",
                column: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_DesignDrills_SeedKey",
                table: "DesignDrills",
                column: "SeedKey",
                unique: true,
                filter: "[SeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQas_DdiaNoteId",
                table: "InterviewQas",
                column: "DdiaNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingLogs_ChapterId",
                table: "ReadingLogs",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingLogs_Date",
                table: "ReadingLogs",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticulationLogs");

            migrationBuilder.DropTable(
                name: "ArticulationPrompts");

            migrationBuilder.DropTable(
                name: "DesignDrills");

            migrationBuilder.DropTable(
                name: "InterviewQas");

            migrationBuilder.DropTable(
                name: "ReadingLogs");

            migrationBuilder.DropTable(
                name: "DdiaNotes");

            migrationBuilder.DropTable(
                name: "DdiaChapters");
        }
    }
}
