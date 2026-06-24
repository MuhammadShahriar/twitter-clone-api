using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TwitterClone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedHandle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Handle",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedHandle",
                table: "AspNetUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows so the unique index below has real values to enforce. Mirrors
            // HandleNormalizer.Normalize: trim, drop a single leading '@', upper-case. (No-op on the
            // in-memory provider, which never runs migrations.)
            migrationBuilder.Sql(
                "UPDATE \"AspNetUsers\" SET \"NormalizedHandle\" = " +
                "UPPER(TRIM(CASE WHEN TRIM(\"Handle\") LIKE '@%' " +
                "THEN SUBSTRING(TRIM(\"Handle\") FROM 2) ELSE TRIM(\"Handle\") END));");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NormalizedHandle",
                table: "AspNetUsers",
                column: "NormalizedHandle",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NormalizedHandle",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedHandle",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Handle",
                table: "AspNetUsers",
                column: "Handle",
                unique: true);
        }
    }
}
