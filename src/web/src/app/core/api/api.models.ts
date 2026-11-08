/** Shapes of the tenant API (/api/{tenant}/...). Dates are ISO-8601 UTC strings ("...Z"). */

export interface PagedResult<T> {
  items: T[];
  /** 1-based. */
  page: number;
  pageSize: number;
  totalCount: number;
}

export type UserRole = 'Admin' | 'User';
export type UserStatus = 'active' | 'inactive';
export type UserSortField = 'displayName' | 'email' | 'lastLogin' | 'createdAt';
export type SortDirection = 'asc' | 'desc';

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
}

export interface UserListQuery {
  search: string;
  role: UserRole | null;
  status: UserStatus | null;
  sort: UserSortField;
  dir: SortDirection;
  /** 1-based. */
  page: number;
  pageSize: number;
}

export interface DashboardSummary {
  totalUsers: number;
  activeUsers: number;
  admins: number;
  signInsLast7Days: number;
}

export type AuditAction =
  | 'UserSignedIn'
  | 'UserDeactivated'
  | 'UserReactivated'
  | 'TenantSettingsUpdated'
  | 'UserProvisioned';

export interface AuditEntry {
  id: string;
  occurredAtUtc: string;
  action: AuditAction;
  actorUserId: string | null;
  actorDisplayName: string;
  targetUserId: string | null;
  targetDisplayName: string | null;
  details: string | null;
}

export interface SignInEvent {
  occurredAtUtc: string;
}

export interface TenantSettings {
  name: string;
}
