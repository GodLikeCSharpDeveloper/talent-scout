using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentScout.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedInSynchronization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_EnrollmentId_Kind",
                table: "Messages");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionState",
                table: "LinkedInConversations",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSynchronizedAt",
                table: "LinkedInConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextSynchronizationAt",
                table: "LinkedInConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SynchronizationError",
                table: "LinkedInConversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SynchronizationLeaseUntil",
                table: "LinkedInConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_EnrollmentId_Kind",
                table: "Messages",
                columns: new[] { "EnrollmentId", "Kind" },
                unique: true,
                filter: "\"Kind\" IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_EnrollmentId_Kind",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ConnectionState",
                table: "LinkedInConversations");

            migrationBuilder.DropColumn(
                name: "LastSynchronizedAt",
                table: "LinkedInConversations");

            migrationBuilder.DropColumn(
                name: "NextSynchronizationAt",
                table: "LinkedInConversations");

            migrationBuilder.DropColumn(
                name: "SynchronizationError",
                table: "LinkedInConversations");

            migrationBuilder.DropColumn(
                name: "SynchronizationLeaseUntil",
                table: "LinkedInConversations");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_EnrollmentId_Kind",
                table: "Messages",
                columns: new[] { "EnrollmentId", "Kind" },
                unique: true,
                filter: "\"Kind\" <> 2");
        }
    }
}
