using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations;

public partial class RemoveProducts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ItemType", table: "OrderItems", type: "text",
            nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(
            name: "NameSnapshot", table: "OrderItems", type: "text",
            nullable: false, defaultValue: "");

        // Preserve display names and financial history before deleting the catalog.
        // Legacy IDs can differ from Course IDs; always use the explicit mapping.
        migrationBuilder.Sql("""
            UPDATE "OrderItems" AS item
            SET "NameSnapshot" = product."Nome", "ItemType" = product."TipoProduto"
            FROM "Products" AS product
            WHERE item."ProdutoId" = product."Id";

            UPDATE "OrderItems" AS item
            SET "CourseId" = course."Id"
            FROM "Courses" AS course
            WHERE item."CourseId" IS NULL
              AND item."ProdutoId" = course."LegacyProductId";

            UPDATE "Students" AS student
            SET "CourseId" = course."Id"::text
            FROM "Courses" AS course
            WHERE student."CourseId" = course."LegacyProductId"::text;

            UPDATE "Enrollments" AS enrollment
            SET "CourseId" = class."CourseId"
            FROM "CourseClasses" AS class
            WHERE enrollment."CourseId" IS NULL
              AND enrollment."ClassId" = class."Id";

            UPDATE "OrderItems" AS item
            SET "TurmaId" = existing."ClassId"
            FROM (
                SELECT "OrderId", "CourseId", MIN("ClassId") AS "ClassId"
                FROM "Enrollments"
                WHERE "ClassId" IS NOT NULL
                GROUP BY "OrderId", "CourseId"
                HAVING COUNT(DISTINCT "ClassId") = 1
            ) AS existing
            WHERE item."TurmaId" IS NULL
              AND item."PedidoId" = existing."OrderId"
              AND item."CourseId" = existing."CourseId";

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM "OrderItems"
                    WHERE "ItemType" = 'course' AND "CourseId" IS NULL
                ) THEN
                    RAISE EXCEPTION 'Some historical course items have no CourseId. Resolve them before removing Products.';
                END IF;
                IF EXISTS (
                    SELECT 1 FROM "OrderItems" AS item
                    JOIN "Courses" AS course ON course."Id" = item."CourseId"
                    WHERE course."LegacyProductId" IS NOT NULL
                      AND course."LegacyProductId" <> item."ProdutoId"
                ) THEN
                    RAISE EXCEPTION 'Some order items have conflicting course and product references.';
                END IF;
                IF EXISTS (
                    SELECT 1 FROM "OrderItems" AS item
                    JOIN "Courses" AS course ON course."Id" = item."CourseId"
                    JOIN "Orders" AS orders ON orders."Id" = item."PedidoId"
                    WHERE orders."PaymentStatus" = 'pending'
                      AND orders."Status" = 'pending'
                      AND course."DeliveryMode" = 'presencial'
                      AND item."TurmaId" IS NULL
                ) THEN
                    RAISE EXCEPTION 'Pending legacy orders need an explicit class or cancellation before removing Products.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_Courses_Products_LegacyProductId", table: "Courses");
        migrationBuilder.DropForeignKey(
            name: "FK_OrderItems_Products_ProdutoId", table: "OrderItems");
        migrationBuilder.DropIndex(name: "IX_OrderItems_ProdutoId", table: "OrderItems");
        migrationBuilder.DropIndex(name: "IX_Courses_LegacyProductId", table: "Courses");
        migrationBuilder.DropColumn(name: "ProdutoId", table: "OrderItems");
        migrationBuilder.DropColumn(name: "LegacyProductId", table: "Courses");
        migrationBuilder.DropTable(name: "Products");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Removed product rows cannot be reconstructed from course/order snapshots.
        throw new NotSupportedException(
            "Removing Products is irreversible. Restore the pre-migration database backup to roll back.");
    }
}