using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DxPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationDocumentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentsUrl",
                table: "ApplicationAssets",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RunbookUrl",
                table: "ApplicationAssets",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentsUrl",
                table: "ApplicationAssets");

            migrationBuilder.DropColumn(
                name: "RunbookUrl",
                table: "ApplicationAssets");
        }
    }
}
