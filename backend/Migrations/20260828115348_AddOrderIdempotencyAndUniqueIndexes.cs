using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIdempotencyAndUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_UsuarioId",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_Code",
                table: "PromoCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UsuarioId_IdempotencyKey",
                table: "Orders",
                columns: new[] { "UsuarioId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PromoCodes_Code",
                table: "PromoCodes");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UsuarioId_IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UsuarioId",
                table: "Orders",
                column: "UsuarioId");
        }
    }
}
