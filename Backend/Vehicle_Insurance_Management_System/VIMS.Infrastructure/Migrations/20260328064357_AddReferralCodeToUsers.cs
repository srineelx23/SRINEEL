using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralCodeToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE [Users]
                SET [ReferralCode] = UPPER(
                    LEFT(
                        LTRIM(RTRIM(ISNULL([FullName], 'USER'))),
                        CASE
                            WHEN CHARINDEX(' ', LTRIM(RTRIM(ISNULL([FullName], 'USER')))) > 0
                                THEN CHARINDEX(' ', LTRIM(RTRIM(ISNULL([FullName], 'USER')))) - 1
                            ELSE LEN(LTRIM(RTRIM(ISNULL([FullName], 'USER'))))
                        END
                    )
                ) + CAST([UserId] AS nvarchar(20))
                WHERE [Role] = 3
                  AND ([ReferralCode] IS NULL OR LTRIM(RTRIM([ReferralCode])) = '');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Users");
        }
    }
}
