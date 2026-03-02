using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Plan_id_set_for_vehicleapp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlanId",
                table: "VehicleApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleApplications_PlanId",
                table: "VehicleApplications",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleApplications_PolicyPlans_PlanId",
                table: "VehicleApplications",
                column: "PlanId",
                principalTable: "PolicyPlans",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VehicleApplications_PolicyPlans_PlanId",
                table: "VehicleApplications");

            migrationBuilder.DropIndex(
                name: "IX_VehicleApplications_PlanId",
                table: "VehicleApplications");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "VehicleApplications");
        }
    }
}
