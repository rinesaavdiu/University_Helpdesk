using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityHelpdesk.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetrySchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "PendingNotifications",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "PendingNotifications");
        }
    }
}
