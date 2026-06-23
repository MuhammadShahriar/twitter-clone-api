using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TwitterClone.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTweetReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Tweets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tweets_ParentId",
                table: "Tweets",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tweets_Tweets_ParentId",
                table: "Tweets",
                column: "ParentId",
                principalTable: "Tweets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tweets_Tweets_ParentId",
                table: "Tweets");

            migrationBuilder.DropIndex(
                name: "IX_Tweets_ParentId",
                table: "Tweets");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Tweets");
        }
    }
}
