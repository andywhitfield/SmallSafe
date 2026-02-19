using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmallSafe.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddedBinarySafeDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "EncyptedSafeDb",
                table: "UserAccounts",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncyptedSafeDb",
                table: "UserAccounts");
        }
    }
}
