using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TwitterClone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsQuoteFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQuote",
                table: "Tweets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill any quotes created before this column existed: a row that still points at a quoted
            // tweet (QuotedTweetId not yet nulled by SET NULL) was created as a quote. Quotes whose target
            // was already deleted can't be recovered (the id is gone), but in practice there are none — the
            // feature hasn't shipped.
            migrationBuilder.Sql(
                "UPDATE \"Tweets\" SET \"IsQuote\" = true WHERE \"QuotedTweetId\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsQuote",
                table: "Tweets");
        }
    }
}
