using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentScout.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignSystemPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "Campaigns");
        }
    }
}
