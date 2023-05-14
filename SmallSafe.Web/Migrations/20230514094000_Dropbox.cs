using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmallSafe.Web.Migrations
{
    /// <inheritdoc />
    public partial class Dropbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DropboxAccessToken",
                table: "UserAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropboxRefreshToken",
                table: "UserAccounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropboxAccessToken",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "DropboxRefreshToken",
                table: "UserAccounts");
        }
    }
}
