import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { UserRole, UserStatus } from '../../../../core/api/api.models';

/** Presentational: search box and role/status filters. Emits every keystroke; the page debounces. */
@Component({
  selector: 'app-users-filters',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule],
  template: `
    <div class="filters" role="search">
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
        <mat-label>Search name or email</mat-label>
        <mat-icon matPrefix aria-hidden="true">search</mat-icon>
        <input
          matInput
          type="search"
          data-testid="search"
          [value]="search()"
          (input)="searchChange.emit($any($event.target).value)"
        />
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Role</mat-label>
        <mat-select data-testid="role-filter" [value]="role()" (valueChange)="roleChange.emit($event)">
          <mat-option [value]="null">All roles</mat-option>
          <mat-option value="Admin">Admin</mat-option>
          <mat-option value="User">User</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select data-testid="status-filter" [value]="status()" (valueChange)="statusChange.emit($event)">
          <mat-option [value]="null">All statuses</mat-option>
          <mat-option value="active">Active</mat-option>
          <mat-option value="inactive">Inactive</mat-option>
        </mat-select>
      </mat-form-field>
    </div>
  `,
  styles: `
    .filters { display: flex; flex-wrap: wrap; gap: 0.75rem; margin-bottom: 1rem; }
    .search { flex: 1 1 260px; }
    mat-form-field:not(.search) { flex: 0 1 180px; }
  `,
})
export class UsersFilters {
  readonly search = input('');
  readonly role = input<UserRole | null>(null);
  readonly status = input<UserStatus | null>(null);

  readonly searchChange = output<string>();
  readonly roleChange = output<UserRole | null>();
  readonly statusChange = output<UserStatus | null>();
}
