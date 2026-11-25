using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCgmSimHeightAndActivityLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE cgmsim_settings ADD COLUMN IF NOT EXISTS height double precision NOT NULL DEFAULT 170.0;
                ALTER TABLE cgmsim_settings ADD COLUMN IF NOT EXISTS activity_level character varying(32) NOT NULL DEFAULT 'moderate';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE cgmsim_settings DROP COLUMN IF EXISTS activity_level;
                ALTER TABLE cgmsim_settings DROP COLUMN IF EXISTS height;
                """);
        }
    }
}
