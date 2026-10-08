using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacyCoursesAndAddCourseInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseInterests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseInterests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseInterests_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseInterests_CourseId_Email",
                table: "CourseInterests",
                columns: new[] { "CourseId", "Email" },
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO ""Courses"" (""Nome"", ""Description"", ""Preco"", ""Image"", ""Category"", ""DeliveryMode"", ""WorkloadHours"", ""IsActive"", ""LegacyProductId"")
                SELECT product.""Nome"", product.""Description"", product.""Preco"", product.""Image"", product.""Category"", 'presencial', 0, TRUE, product.""Id""
                FROM ""Products"" AS product
                WHERE product.""TipoProduto"" = 'course'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""Courses"" AS course
                      WHERE course.""LegacyProductId"" = product.""Id""
                  );

                UPDATE ""CourseClasses"" AS courseClass
                SET ""CourseId"" = course.""Id""
                FROM ""Courses"" AS course
                WHERE courseClass.""CourseId"" IS NULL
                  AND courseClass.""ProdutoId"" = course.""LegacyProductId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseInterests");
        }
    }
}
