using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNaturalDisasterAndMaxCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoversNaturalDisaster",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "MaxCoverageAmount",
                table: "PolicyPlans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CoversNaturalDisaster",
                table: "PolicyPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxCoverageAmount",
                table: "PolicyPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
