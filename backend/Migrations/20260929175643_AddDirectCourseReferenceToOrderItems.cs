using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectCourseReferenceToOrderItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseId",
                table: "OrderItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_CourseId",
                table: "OrderItems",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Courses_CourseId",
                table: "OrderItems",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
                UPDATE ""OrderItems"" AS item
                SET ""CourseId"" = course.""Id""
                FROM ""Courses"" AS course
                WHERE item.""CourseId"" IS NULL
                  AND course.""LegacyProductId"" = item.""ProdutoId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Courses_CourseId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_CourseId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "OrderItems");
        }
    }
}
