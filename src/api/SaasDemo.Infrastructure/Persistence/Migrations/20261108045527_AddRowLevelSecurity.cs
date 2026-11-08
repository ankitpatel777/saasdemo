using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaasDemo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// SQL Server Row-Level Security for tenant-owned tables (isolation layer 2 of 2).
    /// The app sets SESSION_CONTEXT('TenantId') on every connection (TenantSessionContextInterceptor);
    /// the policy then filters reads and blocks writes for any other tenant, even if app code forgets.
    /// Each CREATE runs through EXEC(N'...') because CREATE SCHEMA/FUNCTION/SECURITY POLICY must be alone
    /// in their batch, and idempotent migration scripts (CI/CD) wrap every statement in IF ... BEGIN/END.
    /// Single quotes inside EXEC strings are doubled.
    /// </summary>
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC(N'CREATE SCHEMA [Security];');");

            // Returns a row (= access allowed) when the row's tenant matches the session's tenant.
            // db_owner bypass: lets the developer/Entra admin inspect all data in SSMS. The app's managed
            // identity must NEVER be db_owner, or it would bypass isolation too.
            migrationBuilder.Sql("""
                EXEC(N'
                CREATE FUNCTION [Security].[fn_TenantAccessPredicate](@TenantId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN
                    SELECT 1 AS fn_TenantAccessPredicate_result
                    WHERE @TenantId = CAST(SESSION_CONTEXT(N''TenantId'') AS uniqueidentifier)
                       OR IS_MEMBER(''db_owner'') = 1;
                ');
                """);

            // FILTER: SELECT/UPDATE/DELETE only see the current tenant's rows.
            // BLOCK AFTER INSERT/UPDATE: a row can't be written (or moved) into another tenant.
            migrationBuilder.Sql("""
                EXEC(N'
                CREATE SECURITY POLICY [Security].[TenantIsolationPolicy]
                    ADD FILTER PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON [dbo].[Users],
                    ADD BLOCK PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON [dbo].[Users] AFTER INSERT,
                    ADD BLOCK PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON [dbo].[Users] AFTER UPDATE
                WITH (STATE = ON);
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse order: the policy depends on the function, the function on the schema.
            migrationBuilder.Sql("DROP SECURITY POLICY IF EXISTS [Security].[TenantIsolationPolicy];");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS [Security].[fn_TenantAccessPredicate];");
            migrationBuilder.Sql("EXEC(N'DROP SCHEMA IF EXISTS [Security];');");
        }
    }
}
