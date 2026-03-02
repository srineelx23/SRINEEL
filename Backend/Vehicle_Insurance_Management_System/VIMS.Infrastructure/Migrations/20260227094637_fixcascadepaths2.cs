using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class fixcascadepaths2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VehicleApplications_Users_AssignedAgentUserId",
                table: "VehicleApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_VehicleApplications_Users_CustomerUserId",
                table: "VehicleApplications");

            migrationBuilder.DropIndex(
                name: "IX_VehicleApplications_AssignedAgentUserId",
                table: "VehicleApplications");

            migrationBuilder.DropIndex(
                name: "IX_VehicleApplications_CustomerUserId",
                table: "VehicleApplications");

            migrationBuilder.DropColumn(
                name: "AssignedAgentUserId",
                table: "VehicleApplications");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "VehicleApplications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedAgentUserId",
                table: "VehicleApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerUserId",
                table: "VehicleApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleApplications_AssignedAgentUserId",
                table: "VehicleApplications",
                column: "AssignedAgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleApplications_CustomerUserId",
                table: "VehicleApplications",
                column: "CustomerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleApplications_Users_AssignedAgentUserId",
                table: "VehicleApplications",
                column: "AssignedAgentUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleApplications_Users_CustomerUserId",
                table: "VehicleApplications",
                column: "CustomerUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
