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
                    sex character varying(32) NOT NULL DEFAULT 'other',
                    weight double precision NOT NULL DEFAULT 70,
                    age integer NOT NULL DEFAULT 30,
                    isf double precision NOT NULL DEFAULT 50,
                    dia double precision NOT NULL DEFAULT 4,
                    carbs_abs_time integer NOT NULL DEFAULT 180,
                    insulin_peak integer NOT NULL DEFAULT 75,
                    cr double precision NOT NULL DEFAULT 10,
                    is_initialized boolean NOT NULL DEFAULT false,
                    noise_points jsonb,
                    noise_generated_at timestamp with time zone,
                    created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    updated_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT "PK_cgmsim_settings" PRIMARY KEY (id),
                    CONSTRAINT "FK_cgmsim_settings_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
                );

                ALTER TABLE cgmsim_settings ADD COLUMN IF NOT EXISTS is_initialized boolean NOT NULL DEFAULT false;
                ALTER TABLE cgmsim_settings ADD COLUMN IF NOT EXISTS noise_points jsonb;
                ALTER TABLE cgmsim_settings ADD COLUMN IF NOT EXISTS noise_generated_at timestamp with time zone;
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
            migrationBuilder.Sql("DROP TABLE IF EXISTS cgmsim_settings;");
        }
    }
}
