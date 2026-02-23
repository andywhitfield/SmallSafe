using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmallSafe.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SafeDb",
                table: "UserAccounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SafeDb",
                table: "UserAccounts",
                type: "TEXT",
                nullable: true);
        }
    }
}
