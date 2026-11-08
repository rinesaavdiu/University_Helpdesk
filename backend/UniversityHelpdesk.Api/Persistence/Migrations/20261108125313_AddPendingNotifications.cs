using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityHelpdesk.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingNotifications", x => x.Id);
                    table.CheckConstraint("CK_PendingNotifications_Attempts_NotNegative", "\"Attempts\" >= 0");
                    table.CheckConstraint("CK_PendingNotifications_EventType", "\"EventType\" IN ('TicketResolved')");
                    table.ForeignKey(
                        name: "FK_PendingNotifications_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingNotifications_TicketId_EventType",
                table: "PendingNotifications",
                columns: new[] { "TicketId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingNotifications_Unpublished",
                table: "PendingNotifications",
                column: "CreatedAt",
                filter: "\"PublishedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingNotifications");
        }
    }
}
