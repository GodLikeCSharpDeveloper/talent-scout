using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentScout.Data.Persistence.Migrations;

public partial class AddUnipileConversationsAndInbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LinkedInConversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<string>(type: "text", nullable: false),
                RecipientId = table.Column<string>(type: "text", nullable: false),
                ChatId = table.Column<string>(type: "text", nullable: true),
                ProviderChatId = table.Column<string>(type: "text", nullable: true),
                InvitationId = table.Column<string>(type: "text", nullable: true),
                ConnectionRequestDelivery = table.Column<int>(type: "integer", nullable: true),
                ConnectionRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ConnectionConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Version = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LinkedInConversations", x => x.Id);
                table.ForeignKey("FK_LinkedInConversations_Enrollments_EnrollmentId", x => x.EnrollmentId,
                    "Enrollments", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_LinkedInConversations_EnrollmentId", "LinkedInConversations", "EnrollmentId", unique: true);
        migrationBuilder.CreateIndex("IX_LinkedInConversations_AccountId_RecipientId", "LinkedInConversations", new[] { "AccountId", "RecipientId" }, unique: true);
        migrationBuilder.CreateIndex("IX_LinkedInConversations_AccountId_ChatId", "LinkedInConversations", new[] { "AccountId", "ChatId" }, unique: true, filter: "\"ChatId\" IS NOT NULL");
        migrationBuilder.CreateTable(
            name: "LinkedInWebhookEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "text", nullable: false),
                AccountId = table.Column<string>(type: "text", nullable: false),
                ExternalMessageId = table.Column<string>(type: "text", nullable: false),
                ChatId = table.Column<string>(type: "text", nullable: false),
                ProviderChatId = table.Column<string>(type: "text", nullable: true),
                SenderId = table.Column<string>(type: "text", nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                HasAttachments = table.Column<bool>(type: "boolean", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_LinkedInWebhookEvents", x => x.Id));
        migrationBuilder.CreateIndex("IX_LinkedInWebhookEvents_AccountId_ExternalMessageId", "LinkedInWebhookEvents", new[] { "AccountId", "ExternalMessageId" }, unique: true);
        migrationBuilder.CreateIndex("IX_LinkedInWebhookEvents_ProcessedAt_NextAttemptAt", "LinkedInWebhookEvents", new[] { "ProcessedAt", "NextAttemptAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("LinkedInWebhookEvents");
        migrationBuilder.DropTable("LinkedInConversations");
    }
}
