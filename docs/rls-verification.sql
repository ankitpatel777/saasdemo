/*
  Manual Row-Level Security check for sqldb-saasdemo-dev.

  IMPORTANT
  - Real RLS can only be verified on Azure SQL / SQL Server. The automated tests use SQLite, which has
    no RLS: they cover the EF Core query filter and SaveChanges guard (layer 1), not this (layer 2).
  - Run it as the Entra admin AFTER `dotnet ef database update`, in SSMS or Azure Data Studio,
    as ONE batch (no GO).
  - You are db_owner, and the policy deliberately lets db_owner see everything. So the checks run
    as a temporary user WITHOUT db_owner (EXECUTE AS), exactly like the app identity.
  - Everything happens inside a transaction that is rolled back: no test data or user is left behind.
  - The app sets the tenant with @read_only = 1. This script uses read-only = 0 only so it can switch
    tenants in one session.
*/
SET NOCOUNT ON;
SET XACT_ABORT OFF;  -- let the expected "blocked" error be caught instead of aborting the batch

BEGIN TRANSACTION;

DECLARE @TenantA uniqueidentifier = NEWID();
DECLARE @TenantB uniqueidentifier = NEWID();

-- Test data, inserted as yourself (db_owner bypass).
INSERT INTO dbo.Tenants (Id, Slug, Name, EntraTenantId, IsActive, CreatedAtUtc)
VALUES (@TenantA, 'rls-test-a', 'RLS Test A', NEWID(), 1, SYSUTCDATETIME()),
       (@TenantB, 'rls-test-b', 'RLS Test B', NEWID(), 1, SYSUTCDATETIME());

INSERT INTO dbo.Users (Id, TenantId, EntraObjectId, Email, DisplayName, Role, IsActive, CreatedAtUtc, LastLoginAtUtc)
VALUES (NEWID(), @TenantA, NEWID(), 'a@rls.test', 'Tenant A user', 'User', 1, SYSUTCDATETIME(), NULL),
       (NEWID(), @TenantB, NEWID(), 'b@rls.test', 'Tenant B user', 'User', 1, SYSUTCDATETIME(), NULL);

-- Audit events (migration AddAuditEventsAndRls), one per tenant.
INSERT INTO dbo.AuditEvents (Id, TenantId, OccurredAtUtc, ActorUserId, ActorDisplayName, Action, TargetUserId, Details)
VALUES (NEWID(), @TenantA, SYSUTCDATETIME(), NULL, 'Tenant A admin', 'UserDeactivated', NULL, 'rls test A'),
       (NEWID(), @TenantB, SYSUTCDATETIME(), NULL, 'Tenant B admin', 'UserDeactivated', NULL, 'rls test B');

-- A user like the app's managed identity: can read/write the tenant tables, is NOT db_owner.
CREATE USER rls_tester WITHOUT LOGIN;
GRANT SELECT, INSERT, UPDATE ON dbo.Users TO rls_tester;
GRANT SELECT, INSERT, UPDATE ON dbo.AuditEvents TO rls_tester;

EXECUTE AS USER = 'rls_tester';

-- 1) No tenant in session context: expect 0 rows.
SELECT 'no tenant set' AS [check], COUNT(*) AS visible_users FROM dbo.Users;

-- 2) Tenant A: expect only a@rls.test.
EXEC sp_set_session_context @key = N'TenantId', @value = @TenantA;
SELECT 'as tenant A' AS [check], Email FROM dbo.Users;

-- 3) Tenant B: expect only b@rls.test.
EXEC sp_set_session_context @key = N'TenantId', @value = @TenantB;
SELECT 'as tenant B' AS [check], Email FROM dbo.Users;

-- 4) Still tenant B, try to insert a row for tenant A: expect the BLOCK predicate to reject it.
BEGIN TRY
    INSERT INTO dbo.Users (Id, TenantId, EntraObjectId, Email, DisplayName, Role, IsActive, CreatedAtUtc, LastLoginAtUtc)
    VALUES (NEWID(), @TenantA, NEWID(), 'sneaky@rls.test', 'Cross-tenant insert', 'Admin', 1, SYSUTCDATETIME(), NULL);
    PRINT 'FAIL: cross-tenant insert was NOT blocked.';
END TRY
BEGIN CATCH
    PRINT 'OK: cross-tenant insert blocked (error ' + CAST(ERROR_NUMBER() AS varchar(10)) + '): ' + ERROR_MESSAGE();
END CATCH;

/* ---------- dbo.AuditEvents (added by migration AddAuditEventsAndRls) ---------- */

-- 5) Policy coverage: expect 6 rows, FILTER + 2 BLOCK predicates for each of Users and AuditEvents.
SELECT 'policy predicates' AS [check], OBJECT_NAME(p.target_object_id) AS [table], p.predicate_type_desc, p.operation_desc
FROM sys.security_predicates AS p
WHERE p.object_id = OBJECT_ID(N'Security.TenantIsolationPolicy')
ORDER BY [table], p.predicate_type_desc, p.operation_desc;

-- 6) Still tenant B: expect only 'rls test B'.
SELECT 'audit as tenant B' AS [check], Details FROM dbo.AuditEvents;

-- 7) No tenant: expect 0 audit rows.
EXEC sp_set_session_context @key = N'TenantId', @value = NULL;
SELECT 'audit, no tenant set' AS [check], COUNT(*) AS visible_events FROM dbo.AuditEvents;

-- 8) As tenant B, forge an audit event for tenant A: expect the BLOCK predicate to reject it.
EXEC sp_set_session_context @key = N'TenantId', @value = @TenantB;
BEGIN TRY
    INSERT INTO dbo.AuditEvents (Id, TenantId, OccurredAtUtc, ActorUserId, ActorDisplayName, Action, TargetUserId, Details)
    VALUES (NEWID(), @TenantA, SYSUTCDATETIME(), NULL, 'Forger', 'TenantSettingsUpdated', NULL, 'cross-tenant');
    PRINT 'FAIL: cross-tenant audit insert was NOT blocked.';
END TRY
BEGIN CATCH
    PRINT 'OK: cross-tenant audit insert blocked (error ' + CAST(ERROR_NUMBER() AS varchar(10)) + '): ' + ERROR_MESSAGE();
END CATCH;

-- 9) As tenant B, move B's own event to tenant A: expect BLOCK AFTER UPDATE to reject it.
BEGIN TRY
    UPDATE dbo.AuditEvents SET TenantId = @TenantA WHERE Details = 'rls test B';
    PRINT 'FAIL: moving an audit event to another tenant was NOT blocked.';
END TRY
BEGIN CATCH
    PRINT 'OK: audit update to another tenant blocked (error ' + CAST(ERROR_NUMBER() AS varchar(10)) + '): ' + ERROR_MESSAGE();
END CATCH;

REVERT;

ROLLBACK TRANSACTION;  -- removes the test tenants, users, audit events and rls_tester
