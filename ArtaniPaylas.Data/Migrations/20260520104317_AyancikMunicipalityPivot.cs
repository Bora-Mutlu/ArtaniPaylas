using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArtaniPaylas.Data.Migrations
{
    /// <inheritdoc />
    public partial class AyancikMunicipalityPivot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_ListingId",
                table: "Requests");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAppointmentAt",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipalityNote",
                table: "Requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAppointmentAt",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RequesterNote",
                table: "Requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgeGroup",
                table: "Listings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Listings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "Listings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactNote",
                table: "Listings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Listings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Listings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "Listings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContainerLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ListingPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListingId = table.Column<int>(type: "integer", nullable: false),
                    PhotoPath = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingPhotos_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ListingId_RequesterUserId",
                table: "Requests",
                columns: new[] { "ListingId", "RequesterUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerLocations_District",
                table: "ContainerLocations",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerLocations_IsActive",
                table: "ContainerLocations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ListingPhotos_ListingId",
                table: "ListingPhotos",
                column: "ListingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContainerLocations");

            migrationBuilder.DropTable(
                name: "ListingPhotos");

            migrationBuilder.DropIndex(
                name: "IX_Requests_ListingId_RequesterUserId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ConfirmedAppointmentAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "MunicipalityNote",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestedAppointmentAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequesterNote",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "AgeGroup",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ContactNote",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "Listings");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ListingId",
                table: "Requests",
                column: "ListingId");
        }
    }
}
