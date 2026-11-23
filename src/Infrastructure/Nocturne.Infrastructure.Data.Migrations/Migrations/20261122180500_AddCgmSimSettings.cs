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
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS cgmsim_settings (
                    id uuid NOT NULL,
                    tenant_id uuid NOT NULL,
                    sex character varying(32) NOT NULL,
                    weight double precision NOT NULL,
                    age integer NOT NULL,
                    isf double precision NOT NULL,
                    dia double precision NOT NULL,
                    carbs_abs_time integer NOT NULL,
                    insulin_peak integer NOT NULL,
                    cr double precision NOT NULL,
                    is_initialized boolean NOT NULL DEFAULT FALSE,
                    noise_points jsonb NULL,
                    noise_generated_at timestamp with time zone NULL,
                    created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT "PK_cgmsim_settings" PRIMARY KEY (id),
                    CONSTRAINT "FK_cgmsim_settings_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
                );

                ALTER TABLE cgmsim_settings ADD COLUMN IF NOT EXISTS is_initialized boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE cgmsim_settings ADD COLUMN IF NOT EXISTS noise_points jsonb NULL;
                ALTER TABLE cgmsim_settings ADD COLUMN IF NOT EXISTS noise_generated_at timestamp with time zone NULL;
                ALTER TABLE cgmsim_settings DROP COLUMN IF EXISTS height;

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_cgmsim_settings_tenant_id_unique" ON cgmsim_settings (tenant_id);

                ALTER TABLE cgmsim_settings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cgmsim_settings FORCE ROW LEVEL SECURITY;
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

            migrationBuilder.DropTable(
                name: "cgmsim_settings");
        }
    }
}
