using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Agent_commission_added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AgentCommissionPercentage",
                table: "PolicyPlans",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ApplicableVehicleType",
                table: "PolicyPlans",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CoversNaturalDisaster",
                table: "PolicyPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CoversOwnDamage",
                table: "PolicyPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CoversTheft",
                table: "PolicyPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CoversThirdParty",
                table: "PolicyPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DeductibleAmount",
                table: "PolicyPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "EngineProtectionAvailable",
                table: "PolicyPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RoadsideAssistanceAvailable",
                table: "PolicyPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ZeroDepreciationAvailable",
                table: "PolicyPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AgentCommissions",
                columns: table => new
                {
                    CommissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PolicyId = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EarnedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentCommissions", x => x.CommissionId);
                    table.ForeignKey(
                        name: "FK_AgentCommissions_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "PolicyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentCommissions_Users_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCommissions_AgentId",
                table: "AgentCommissions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCommissions_PolicyId",
                table: "AgentCommissions",
                column: "PolicyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentCommissions");

            migrationBuilder.DropColumn(
                name: "AgentCommissionPercentage",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "ApplicableVehicleType",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "CoversNaturalDisaster",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "CoversOwnDamage",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "CoversTheft",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "CoversThirdParty",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "DeductibleAmount",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "EngineProtectionAvailable",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "RoadsideAssistanceAvailable",
                table: "PolicyPlans");

            migrationBuilder.DropColumn(
                name: "ZeroDepreciationAvailable",
                table: "PolicyPlans");
        }
    }
}
