using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentScout.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Messages_EnrollmentId_CreatedAt_Id",
                table: "Messages",
                columns: new[] { "EnrollmentId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkedInConversations_AccountId_NextSynchronizationAt",
                table: "LinkedInConversations",
                columns: new[] { "AccountId", "NextSynchronizationAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_Status_NextContactAt",
                table: "Enrollments",
                columns: new[] { "Status", "NextContactAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EnrollmentId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "EnrollmentId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_EnrollmentId_CreatedAt_Id",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_LinkedInConversations_AccountId_NextSynchronizationAt",
                table: "LinkedInConversations");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_Status_NextContactAt",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_EnrollmentId_OccurredAt",
                table: "AuditEvents");
        }
    }
}
