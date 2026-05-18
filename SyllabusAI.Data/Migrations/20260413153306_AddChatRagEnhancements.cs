using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyllabusAI.Data.Migrations
{
    public partial class AddChatRagEnhancements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "SyllabusPdfUploads",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedCategory",
                table: "SyllabusChunks",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalSectionTitle",
                table: "SyllabusChunks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageEnd",
                table: "SyllabusChunks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageStart",
                table: "SyllabusChunks",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE SyllabusPdfUploads SET IsActive = 1 WHERE IsActive = 0");

            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentUserId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatSessions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatSessions_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatSessionId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetrievedChunkIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetrievedCategoriesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsOutOfScope = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatSessions_ChatSessionId",
                        column: x => x.ChatSessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "FeedbackQuestions",
                keyColumn: "Id",
                keyValue: 14,
                column: "Text",
                value: "The content and the instructor's style keep my interest and motivation for the course.");

            migrationBuilder.CreateIndex(
                name: "IX_SyllabusChunks_CourseId_NormalizedCategory",
                table: "SyllabusChunks",
                columns: new[] { "CourseId", "NormalizedCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatSessionId_CreatedAtUtc",
                table: "ChatMessages",
                columns: new[] { "ChatSessionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_CourseId",
                table: "ChatSessions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_StudentUserId_CourseId_CreatedAtUtc",
                table: "ChatSessions",
                columns: new[] { "StudentUserId", "CourseId", "CreatedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChatMessages");
            migrationBuilder.DropTable(name: "ChatSessions");

            migrationBuilder.DropIndex(
                name: "IX_SyllabusChunks_CourseId_NormalizedCategory",
                table: "SyllabusChunks");

            migrationBuilder.DropColumn(name: "IsActive", table: "SyllabusPdfUploads");
            migrationBuilder.DropColumn(name: "NormalizedCategory", table: "SyllabusChunks");
            migrationBuilder.DropColumn(name: "OriginalSectionTitle", table: "SyllabusChunks");
            migrationBuilder.DropColumn(name: "PageEnd", table: "SyllabusChunks");
            migrationBuilder.DropColumn(name: "PageStart", table: "SyllabusChunks");

            migrationBuilder.UpdateData(
                table: "FeedbackQuestions",
                keyColumn: "Id",
                keyValue: 14,
                column: "Text",
                value: "The content and the instructor’s style keep my interest and motivation for the course.");
        }
    }
}
