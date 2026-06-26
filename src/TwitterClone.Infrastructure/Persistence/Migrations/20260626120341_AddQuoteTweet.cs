using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TwitterClone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteTweet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuotedTweetId",
                table: "Tweets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tweets_QuotedTweetId",
                table: "Tweets",
                column: "QuotedTweetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tweets_Tweets_QuotedTweetId",
                table: "Tweets",
                column: "QuotedTweetId",
                principalTable: "Tweets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tweets_Tweets_QuotedTweetId",
                table: "Tweets");

            migrationBuilder.DropIndex(
                name: "IX_Tweets_QuotedTweetId",
                table: "Tweets");

            migrationBuilder.DropColumn(
                name: "QuotedTweetId",
                table: "Tweets");
        }
    }
}
