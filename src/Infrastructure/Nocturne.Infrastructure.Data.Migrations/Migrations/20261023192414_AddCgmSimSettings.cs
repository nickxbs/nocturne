using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCgmSimSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cgmsim_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sex = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    age = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<double>(type: "double precision", nullable: false),
                    isf = table.Column<double>(type: "double precision", nullable: false),
                    dia = table.Column<double>(type: "double precision", nullable: false),
                    carbs_abs_time = table.Column<int>(type: "integer", nullable: false),
                    insulin_peak = table.Column<int>(type: "integer", nullable: false),
                    cr = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cgmsim_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_cgmsim_settings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cgmsim_settings_tenant_id_unique",
                table: "cgmsim_settings",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cgmsim_settings");
        }
    }
}
