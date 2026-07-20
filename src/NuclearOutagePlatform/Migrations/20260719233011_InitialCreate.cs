using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NuclearOutagePlatform.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Outages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    Facility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FacilityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Generator = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Capacity = table.Column<decimal>(type: "numeric", nullable: true),
                    Outage = table.Column<decimal>(type: "numeric", nullable: true),
                    PercentOutage = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outages_Facility_Generator_Period",
                table: "Outages",
                columns: new[] { "Facility", "Generator", "Period" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Outages");
        }
    }
}
