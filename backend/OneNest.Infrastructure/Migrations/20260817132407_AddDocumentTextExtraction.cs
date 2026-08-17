using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTextExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AISummarizedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AISummary",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTextExtracted",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TextExtractedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AISummarizedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "AISummary",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsTextExtracted",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TextExtractedAt",
                table: "Documents");
        }
    }
}
