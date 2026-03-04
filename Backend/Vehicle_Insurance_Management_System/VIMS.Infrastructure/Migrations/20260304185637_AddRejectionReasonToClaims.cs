using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionReasonToClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Claims",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Claims");
        }
    }
}
