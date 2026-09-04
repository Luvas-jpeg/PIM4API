using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class IntroduceCoursesAndExplicitClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EnrolledAt",
                table: "Enrollments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "AvailableSeats",
                table: "CourseClasses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "CourseClasses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CourseId",
                table: "CourseClasses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "CourseClasses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CourseClasses",
                type: "text",
                nullable: false,
                defaultValue: "scheduled");

            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric", nullable: false),
                    Image = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LegacyProductId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Courses_Products_LegacyProductId",
                        column: x => x.LegacyProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Preserve the current product-backed courses and keep their ids stable.
            migrationBuilder.Sql("""
                INSERT INTO "Courses"
                    ("Id", "Nome", "Description", "Preco", "Image", "Category", "IsActive", "LegacyProductId")
                SELECT "Id", "Nome", "Description", "Preco", "Image", "Category", TRUE, "Id"
                FROM "Products"
                WHERE "TipoProduto" = 'course'
                ON CONFLICT ("Id") DO NOTHING;

                UPDATE "CourseClasses" AS cc
                SET "CourseId" = c."Id",
                    "Capacity" = CASE WHEN cc."VafasDisponiveis" < 0 THEN 0 ELSE cc."VafasDisponiveis" END,
                    "AvailableSeats" = CASE WHEN cc."VafasDisponiveis" < 0 THEN 0 ELSE cc."VafasDisponiveis" END
                FROM "Courses" AS c
                WHERE c."LegacyProductId" = cc."ProdutoId";

                SELECT setval(
                    pg_get_serial_sequence('"Courses"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "Courses"), 1),
                    TRUE
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CourseClasses_CourseId",
                table: "CourseClasses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_LegacyProductId",
                table: "Courses",
                column: "LegacyProductId",
                unique: true,
                filter: "\"LegacyProductId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseClasses_Courses_CourseId",
                table: "CourseClasses",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseClasses_Courses_CourseId",
                table: "CourseClasses");

            migrationBuilder.DropTable(
                name: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_CourseClasses_CourseId",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "EnrolledAt",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "AvailableSeats",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CourseClasses");
        }
    }
}
