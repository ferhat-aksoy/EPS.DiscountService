using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPS.Discount.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add composite index for common query patterns
            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_Length_CreatedAt",
                table: "DiscountCodes",
                columns: new[] { "Length", "CreatedAt" });

            // Add partial index for unused codes (most common query)
            migrationBuilder.Sql(
                @"CREATE INDEX IX_DiscountCodes_Code_Unused 
                  ON ""DiscountCodes"" (""Code"") 
                  WHERE ""UsedAt"" IS NULL;");

            // Add index for UsedAt queries

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_UsedAt",
                table: "DiscountCodes",
                column: "UsedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_DiscountCodes_Length_CreatedAt", table: "DiscountCodes");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_DiscountCodes_Code_Unused;");
            migrationBuilder.DropIndex(name: "IX_DiscountCodes_UsedAt", table: "DiscountCodes");
        }
    }
}
