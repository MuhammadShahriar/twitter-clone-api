using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TwitterClone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkTweetToAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tweets are now owned by a real user (AuthorId FK -> AspNetUsers). Any pre-existing rows
            // were created before auth and have no valid author, so there is no honest AuthorId to
            // backfill — we clear them rather than invent fake authors. This is safe pre-release demo
            // data; nothing else needs to be done beyond letting this migration run.
            migrationBuilder.Sql("DELETE FROM \"Tweets\";");

            migrationBuilder.DropColumn(
                name: "AuthorHandle",
                table: "Tweets");

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorId",
                table: "Tweets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Tweets_AuthorId",
                table: "Tweets",
                column: "AuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tweets_AspNetUsers_AuthorId",
                table: "Tweets",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tweets_AspNetUsers_AuthorId",
                table: "Tweets");

            migrationBuilder.DropIndex(
                name: "IX_Tweets_AuthorId",
                table: "Tweets");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "Tweets");

            migrationBuilder.AddColumn<string>(
                name: "AuthorHandle",
                table: "Tweets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
