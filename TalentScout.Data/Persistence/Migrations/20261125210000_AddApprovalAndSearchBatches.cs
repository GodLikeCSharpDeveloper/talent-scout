using Microsoft.EntityFrameworkCore.Migrations;

namespace TalentScout.Data.Persistence.Migrations;

public partial class AddApprovalAndSearchBatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "AutoApproveCandidates", table: "Campaigns", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(name: "ApprovalStatus", table: "Enrollments", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "ApprovedAt", table: "Enrollments", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "OutreachStartedAt", table: "Enrollments", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "SearchBatchId", table: "Enrollments", type: "uuid", nullable: true);
        migrationBuilder.CreateTable(
            name: "SearchBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestedCount = table.Column<int>(type: "integer", nullable: false),
                AddedCount = table.Column<int>(type: "integer", nullable: false),
                SkippedCount = table.Column<int>(type: "integer", nullable: false),
                ScannedCount = table.Column<int>(type: "integer", nullable: false),
                PagesScanned = table.Column<int>(type: "integer", nullable: false),
                NextCursor = table.Column<string>(type: "text", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                StopReason = table.Column<int>(type: "integer", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SearchBatches", x => x.Id);
                table.ForeignKey(name: "FK_SearchBatches_Campaigns_CampaignId", column: x => x.CampaignId,
                    principalTable: "Campaigns", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_SearchBatches_CampaignId_CreatedAt", table: "SearchBatches", columns: new[] { "CampaignId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_Enrollments_SearchBatchId", table: "Enrollments", column: "SearchBatchId");
        migrationBuilder.CreateIndex(name: "IX_Enrollments_CampaignId_ApprovalStatus_Status", table: "Enrollments", columns: new[] { "CampaignId", "ApprovalStatus", "Status" });
        migrationBuilder.AddForeignKey(name: "FK_Enrollments_SearchBatches_SearchBatchId", table: "Enrollments", column: "SearchBatchId",
            principalTable: "SearchBatches", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        // Preserve previously started conversations; uncontacted candidates require explicit approval.
        migrationBuilder.Sql(@"
            UPDATE ""Enrollments"" AS enrollment
            SET ""ApprovalStatus"" = 1,
                ""ApprovedAt"" = enrollment.""CreatedAt"",
                ""OutreachStartedAt"" = COALESCE(
                    (SELECT MIN(message.""SentAt"") FROM ""Messages"" AS message WHERE message.""EnrollmentId"" = enrollment.""Id""),
                    enrollment.""CreatedAt"")
            WHERE enrollment.""HasSentInitialMessage"" OR enrollment.""HasSentInvitation"";");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Enrollments_SearchBatches_SearchBatchId", table: "Enrollments");
        migrationBuilder.DropIndex(name: "IX_Enrollments_SearchBatchId", table: "Enrollments");
        migrationBuilder.DropIndex(name: "IX_Enrollments_CampaignId_ApprovalStatus_Status", table: "Enrollments");
        migrationBuilder.DropTable(name: "SearchBatches");
        migrationBuilder.DropColumn(name: "SearchBatchId", table: "Enrollments");
        migrationBuilder.DropColumn(name: "ApprovalStatus", table: "Enrollments");
        migrationBuilder.DropColumn(name: "ApprovedAt", table: "Enrollments");
        migrationBuilder.DropColumn(name: "OutreachStartedAt", table: "Enrollments");
        migrationBuilder.DropColumn(name: "AutoApproveCandidates", table: "Campaigns");
    }
}
