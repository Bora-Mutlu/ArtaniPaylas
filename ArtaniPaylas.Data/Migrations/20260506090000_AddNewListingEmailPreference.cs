using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace ArtaniPaylas.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260506090000_AddNewListingEmailPreference")]
    public partial class AddNewListingEmailPreference : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnNewListingsByEmail",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyOnNewListingsByEmail",
                table: "AspNetUsers");
        }
    }
}
