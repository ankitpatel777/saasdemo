import { AuditAction, AuditEntry } from '../../core/api/api.models';

/** One readable sentence per audit event, e.g. "Alice deactivated Bob". */
export function describeAudit(entry: AuditEntry): string {
  const actor = entry.actorDisplayName;
  const target = entry.targetDisplayName ?? 'a user';
  switch (entry.action) {
    case 'UserSignedIn':
      return `${actor} signed in`;
    case 'UserProvisioned':
      return `${actor} signed in for the first time`;
    case 'UserDeactivated':
      return `${actor} deactivated ${target}`;
    case 'UserReactivated':
      return `${actor} reactivated ${target}`;
    case 'TenantSettingsUpdated':
      return entry.details ? `${actor} updated the organization: ${entry.details}` : `${actor} updated the organization`;
    default:
      return `${actor}: ${String(entry.action)}`;
  }
}

/** Material Symbols icon name per action. */
export function auditIcon(action: AuditAction): string {
  switch (action) {
    case 'UserSignedIn':
      return 'login';
    case 'UserProvisioned':
      return 'person_add';
    case 'UserDeactivated':
      return 'person_off';
    case 'UserReactivated':
      return 'how_to_reg';
    case 'TenantSettingsUpdated':
      return 'settings';
    default:
      return 'history';
  }
}
