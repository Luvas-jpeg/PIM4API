using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations;

/// <summary>
/// Allows new course classes to use the Course relation without requiring a
/// legacy Product row. Existing product-backed classes remain unchanged.
/// </summary>
[Migration("20260904090000_AllowCourseClassesWithoutLegacyProduct")]
public partial class AllowCourseClassesWithoutLegacyProduct : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "ProdutoId",
            table: "CourseClasses",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "ProdutoId",
            table: "CourseClasses",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
    }
}
