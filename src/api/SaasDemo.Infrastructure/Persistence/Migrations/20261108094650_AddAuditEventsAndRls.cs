using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaasDemo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// dbo.AuditEvents (tenant-owned) and its Row-Level Security predicates, added to the existing
    /// Security.TenantIsolationPolicy. Verify on Azure SQL with docs/rls-verification.sql.
    /// </summary>
    public partial class AddAuditEventsAndRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_OccurredAtUtc",
                table: "AuditEvents",
                columns: new[] { "TenantId", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_TargetUserId_OccurredAtUtc",
                table: "AuditEvents",
                columns: new[] { "TenantId", "TargetUserId", "OccurredAtUtc" });

            // Extend the existing policy (created in AddRowLevelSecurity) to the new table, with the SAME
            // predicate function: FILTER hides other tenants' rows from SELECT/UPDATE/DELETE, BLOCK stops
            // rows being written into (or moved to) another tenant. The policy stays ON throughout.
            // EXEC(N'...') keeps idempotent migration scripts valid, as in AddRowLevelSecurity.
            migrationBuilder.Sql("""
                EXEC(N'
                ALTER SECURITY POLICY [Security].[TenantIsolationPolicy]
                    ADD FILTER PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON [dbo].[AuditEvents],
                    ADD BLOCK PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON [dbo].[AuditEvents] AFTER INSERT,
                    ADD BLOCK PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON [dbo].[AuditEvents] AFTER UPDATE;
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Predicates first: the schema-bound policy references the table, so it can't be dropped before.
            // dbo.Users keeps its predicates; only this table's are removed.
            migrationBuilder.Sql("""
                EXEC(N'
                ALTER SECURITY POLICY [Security].[TenantIsolationPolicy]
                    DROP FILTER PREDICATE ON [dbo].[AuditEvents],
                    DROP BLOCK PREDICATE ON [dbo].[AuditEvents] AFTER INSERT,
                    DROP BLOCK PREDICATE ON [dbo].[AuditEvents] AFTER UPDATE;
                ');
                """);

            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
