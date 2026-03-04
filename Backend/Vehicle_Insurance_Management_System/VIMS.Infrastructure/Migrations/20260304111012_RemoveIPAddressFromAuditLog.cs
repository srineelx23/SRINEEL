using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIPAddressFromAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IPAddress",
                table: "AuditLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IPAddress",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
