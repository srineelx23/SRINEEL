using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTransfer",
                table: "VehicleApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PolicyTransfers",
                columns: table => new
                {
                    PolicyTransferId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PolicyId = table.Column<int>(type: "int", nullable: false),
                    SenderCustomerId = table.Column<int>(type: "int", nullable: false),
                    RecipientCustomerId = table.Column<int>(type: "int", nullable: false),
                    NewVehicleApplicationId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyTransfers", x => x.PolicyTransferId);
                    table.ForeignKey(
                        name: "FK_PolicyTransfers_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "PolicyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PolicyTransfers_Users_RecipientCustomerId",
                        column: x => x.RecipientCustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PolicyTransfers_Users_SenderCustomerId",
                        column: x => x.SenderCustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PolicyTransfers_VehicleApplications_NewVehicleApplicationId",
                        column: x => x.NewVehicleApplicationId,
                        principalTable: "VehicleApplications",
                        principalColumn: "VehicleApplicationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyTransfers_NewVehicleApplicationId",
                table: "PolicyTransfers",
                column: "NewVehicleApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyTransfers_PolicyId",
                table: "PolicyTransfers",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyTransfers_RecipientCustomerId",
                table: "PolicyTransfers",
                column: "RecipientCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyTransfers_SenderCustomerId",
                table: "PolicyTransfers",
                column: "SenderCustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyTransfers");

            migrationBuilder.DropColumn(
                name: "IsTransfer",
                table: "VehicleApplications");
        }
    }
}
