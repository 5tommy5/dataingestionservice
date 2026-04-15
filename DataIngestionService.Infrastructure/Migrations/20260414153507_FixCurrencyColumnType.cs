using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataIngestionService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCurrencyColumnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "currency",
                table: "transactions",
                type: "varchar(3)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "currency",
                table: "transactions",
                type: "char(3)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(3)");
        }
    }
}
