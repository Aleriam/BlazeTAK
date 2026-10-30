using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazeTAK.Migrations
{
    /// <inheritdoc />
    public partial class roles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LeaderId",
                table: "Groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_LeaderId",
                table: "Groups",
                column: "LeaderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Profiles_LeaderId",
                table: "Groups",
                column: "LeaderId",
                principalTable: "Profiles",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Profiles_LeaderId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Groups_LeaderId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "LeaderId",
                table: "Groups");
        }
    }
}
