using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class final_changes_db : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentMarketValue",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ClaimedAmount",
                table: "Claims");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "ClaimDocuments",
                newName: "Document2");

            migrationBuilder.AddColumn<int>(
                name: "claimType",
                table: "Claims",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Document1",
                table: "ClaimDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "claimType",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "Document1",
                table: "ClaimDocuments");

            migrationBuilder.RenameColumn(
                name: "Document2",
                table: "ClaimDocuments",
                newName: "FilePath");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentMarketValue",
                table: "Vehicles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ClaimedAmount",
                table: "Claims",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
