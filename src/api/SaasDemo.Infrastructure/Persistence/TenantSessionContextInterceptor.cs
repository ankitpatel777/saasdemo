using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SaasDemo.Application.Tenancy;

namespace SaasDemo.Infrastructure.Persistence;

/// <summary>
/// Tenant isolation layer 2 of 2: tells SQL Server who the tenant is, so the Row-Level Security
/// policy (see the AddRowLevelSecurity migration) filters and blocks rows on the server.
/// Runs every time EF opens a connection. Pooled connections are safe: sp_reset_connection
/// clears SESSION_CONTEXT when a pooled connection is handed out again.
/// </summary>
public sealed class TenantSessionContextInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    // @read_only = 1: nothing else on this connection can change the tenant until it is reset.
    private const string SetTenantSql =
        "EXEC sp_set_session_context @key = N'TenantId', @value = @TenantId, @read_only = 1;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (!tenantContext.IsResolved)
        {
            return; // e.g. the tenant lookup itself. RLS then returns no tenant-owned rows.
        }

        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = SetTenantSql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@TenantId";
        parameter.DbType = DbType.Guid;
        parameter.Value = tenantContext.TenantId;
        command.Parameters.Add(parameter);

        return command;
    }
}
