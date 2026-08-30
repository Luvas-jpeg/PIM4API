using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations;

[Migration("20260828150000_DeleteEquipmentMockData")]
public partial class DeleteEquipmentMockData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "Products"
            WHERE "TipoProduto" = 'equipment';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
