using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    CompactSidebar = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAnimations = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWorkspaceContext = table.Column<bool>(type: "boolean", nullable: false),
                    ContextDepth = table.Column<string>(type: "text", nullable: false),
                    DefaultConversationMode = table.Column<string>(type: "text", nullable: false),
                    ResponseStyle = table.Column<string>(type: "text", nullable: false),
                    EnableSmartSuggestions = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAppointmentReminders = table.Column<bool>(type: "boolean", nullable: false),
                    EnableMedicineReminders = table.Column<bool>(type: "boolean", nullable: false),
                    EnableTaskReminders = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWeeklySummary = table.Column<bool>(type: "boolean", nullable: false),
                    EnableDesktopNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    AutoDeleteTrashDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    DefaultHeightUnit = table.Column<string>(type: "text", nullable: false),
                    DefaultWeightUnit = table.Column<string>(type: "text", nullable: false),
                    ReminderLeadTimeHours = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
