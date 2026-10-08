using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleCourseClassesFromProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Resolve legacy-only classes before removing their product reference.
            // A class without a valid course stops the migration instead of losing history.
            migrationBuilder.Sql("""
                UPDATE "CourseClasses" AS cc
                SET "CourseId" = c."Id"
                FROM "Courses" AS c
                WHERE cc."CourseId" IS NULL
                  AND cc."ProdutoId" = c."LegacyProductId";

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "CourseClasses" AS cc
                        LEFT JOIN "Courses" AS c ON c."Id" = cc."CourseId"
                        WHERE c."Id" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'CourseClasses contains classes without a valid CourseId. Resolve them before migrating.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "CourseClasses" AS cc
                        JOIN "Courses" AS c ON c."Id" = cc."CourseId"
                        WHERE cc."ProdutoId" IS NOT NULL
                          AND c."LegacyProductId" IS NOT NULL
                          AND cc."ProdutoId" <> c."LegacyProductId"
                    ) THEN
                        RAISE EXCEPTION 'CourseClasses contains conflicting course and product references. Resolve them before migrating.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CourseClasses_Courses_CourseId",
                table: "CourseClasses");

            migrationBuilder.DropForeignKey(
                name: "FK_CourseClasses_Products_ProdutoId",
                table: "CourseClasses");

            migrationBuilder.DropIndex(
                name: "IX_CourseClasses_ProdutoId",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "ProdutoId",
                table: "CourseClasses");

            migrationBuilder.AlterColumn<int>(
                name: "CourseId",
                table: "CourseClasses",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseClasses_Courses_CourseId",
                table: "CourseClasses",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseClasses_Courses_CourseId",
                table: "CourseClasses");

            migrationBuilder.AlterColumn<int>(
                name: "CourseId",
                table: "CourseClasses",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ProdutoId",
                table: "CourseClasses",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "CourseClasses" AS cc
                SET "ProdutoId" = c."LegacyProductId"
                FROM "Courses" AS c
                WHERE cc."CourseId" = c."Id";
                """);
            migrationBuilder.CreateIndex(
                name: "IX_CourseClasses_ProdutoId",
                table: "CourseClasses",
                column: "ProdutoId");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseClasses_Courses_CourseId",
                table: "CourseClasses",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseClasses_Products_ProdutoId",
                table: "CourseClasses",
                column: "ProdutoId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
