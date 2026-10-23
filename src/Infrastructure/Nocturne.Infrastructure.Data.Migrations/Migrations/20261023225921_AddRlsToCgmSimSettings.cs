using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsToCgmSimSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE cgmsim_settings ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE cgmsim_settings FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS tenant_isolation ON cgmsim_settings;
                CREATE POLICY tenant_isolation ON cgmsim_settings
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON cgmsim_settings;");
            migrationBuilder.Sql("ALTER TABLE cgmsim_settings NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE cgmsim_settings DISABLE ROW LEVEL SECURITY;");
        }
    }
}
