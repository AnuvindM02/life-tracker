using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M3Dsa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DsaTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeferred = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_DsaTopics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DsaProblems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Difficulty = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PlanWeek = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_DsaProblems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DsaProblems_DsaTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "DsaTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DsaAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ProblemId = table.Column<int>(type: "int", nullable: true),
                    TitleFreeText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Pass1Min = table.Column<int>(type: "int", nullable: false),
                    HintLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Pass2Min = table.Column<int>(type: "int", nullable: false),
                    Pass3Min = table.Column<int>(type: "int", nullable: false),
                    TotalMin = table.Column<int>(type: "int", nullable: false),
                    SolvedUnaided = table.Column<bool>(type: "bit", nullable: false),
                    Pattern = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KeyInsight = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WhatIMissed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TimeComplexity = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SpaceComplexity = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    CodeMd = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    Company = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsCounted = table.Column<bool>(type: "bit", nullable: false),
                    NotCountedReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewItemId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DsaAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DsaAttempts_DsaProblems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "DsaProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DsaAttemptTags",
                columns: table => new
                {
                    DsaAttemptId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DsaAttemptTags", x => new { x.DsaAttemptId, x.TagId });
                    table.ForeignKey(
                        name: "FK_DsaAttemptTags_DsaAttempts_DsaAttemptId",
                        column: x => x.DsaAttemptId,
                        principalTable: "DsaAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DsaAttemptTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DsaReviewItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemId = table.Column<int>(type: "int", nullable: false),
                    SourceAttemptId = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IntervalDays = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompletedAttemptId = table.Column<int>(type: "int", nullable: true),
                    SkipReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RescheduleReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DsaReviewItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DsaReviewItems_DsaAttempts_SourceAttemptId",
                        column: x => x.SourceAttemptId,
                        principalTable: "DsaAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DsaReviewItems_DsaProblems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "DsaProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DsaAttempts_Date",
                table: "DsaAttempts",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_DsaAttempts_ProblemId",
                table: "DsaAttempts",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_DsaAttemptTags_TagId",
                table: "DsaAttemptTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_DsaProblems_PlanWeek",
                table: "DsaProblems",
                column: "PlanWeek");

            migrationBuilder.CreateIndex(
                name: "IX_DsaProblems_SeedKey",
                table: "DsaProblems",
                column: "SeedKey",
                unique: true,
                filter: "[SeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DsaProblems_TopicId_SortOrder",
                table: "DsaProblems",
                columns: new[] { "TopicId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DsaReviewItems_DueDate_Status",
                table: "DsaReviewItems",
                columns: new[] { "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DsaReviewItems_ProblemId_Status",
                table: "DsaReviewItems",
                columns: new[] { "ProblemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DsaReviewItems_SourceAttemptId",
                table: "DsaReviewItems",
                column: "SourceAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_DsaTopics_SeedKey",
                table: "DsaTopics",
                column: "SeedKey",
                unique: true,
                filter: "[SeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Kind_Name",
                table: "Tags",
                columns: new[] { "Kind", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DsaAttemptTags");

            migrationBuilder.DropTable(
                name: "DsaReviewItems");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "DsaAttempts");

            migrationBuilder.DropTable(
                name: "DsaProblems");

            migrationBuilder.DropTable(
                name: "DsaTopics");
        }
    }
}
