using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTypeDiscriminator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

                        // Backfill existing records so financial aggregates remain accurate.
                        // 0 = Premium, 1 = ClaimPayout, 2 = TransferFee
                        migrationBuilder.Sql(@"
UPDATE p
SET p.PaymentType = 1
FROM Payments p
WHERE p.TransactionReference IS NOT NULL
    AND LOWER(p.TransactionReference) LIKE 'claim #%';

UPDATE p
SET p.PaymentType = 2
FROM Payments p
INNER JOIN Policies po ON p.PolicyId = po.PolicyId
INNER JOIN Vehicles v ON po.VehicleId = v.VehicleId
INNER JOIN VehicleApplications va ON v.VehicleApplicationId = va.VehicleApplicationId
WHERE p.PaymentType = 0
    AND va.IsTransfer = 1
    AND p.Amount = 500;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Payments");
        }
    }
}
