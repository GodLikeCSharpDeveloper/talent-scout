using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentScout.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewInvitationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InvitationApprovalStatus",
                table: "Enrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InvitationApprovedAt",
                table: "Enrollments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoApproveInterviewInvitations",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE ""Enrollments"" AS enrollment
                SET ""InvitationApprovalStatus"" = 1,
                    ""InvitationApprovedAt"" = COALESCE(
                        (SELECT MIN(message.""SentAt"") FROM ""Messages"" AS message
                         WHERE message.""EnrollmentId"" = enrollment.""Id"" AND message.""Kind"" = 1),
                        enrollment.""CreatedAt"")
                WHERE enrollment.""HasSentInvitation"";");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CampaignId_InvitationApprovalStatus_Status",
                table: "Enrollments",
                columns: new[] { "CampaignId", "InvitationApprovalStatus", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_CampaignId_InvitationApprovalStatus_Status",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "InvitationApprovalStatus",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "InvitationApprovedAt",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "AutoApproveInterviewInvitations",
                table: "Campaigns");
        }
    }
}
