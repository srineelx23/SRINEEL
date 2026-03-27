using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimAnalysisFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FraudRiskAnalysisJson",
                table: "Claims",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Claims",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FraudRiskAnalysisJson",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Claims");
        }
    }
}
