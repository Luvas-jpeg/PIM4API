using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations;

[Migration("20260828150000_DeleteEquipmentMockData")]
public partial class DeleteEquipmentMockData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Remove only unreferenced equipment fixtures. Historical order lines
            -- and legacy class links must remain valid during the migration.
            DELETE FROM "Products" AS product
            WHERE product."TipoProduto" = 'equipment'
              AND NOT EXISTS (
                  SELECT 1
                  FROM "OrderItems" AS order_item
                  WHERE order_item."ProdutoId" = product."Id"
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM "CourseClasses" AS course_class
                  WHERE course_class."ProdutoId" = product."Id"
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
