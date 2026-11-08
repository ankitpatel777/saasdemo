import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SortDirection, UserListItem, UserSortField } from '../../../../core/api/api.models';
import { RelativeTimePipe } from '../../../../shared/pipes/relative-time.pipe';

/**
 * Presentational: one page of users. Sorting is only reported (sortChange); the page asks the server.
 * The row menu is disabled for the signed-in user (you can't deactivate yourself).
 */
@Component({
  selector: 'app-users-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTableModule, MatSortModule, MatMenuModule, MatButtonModule, MatIconModule, MatTooltipModule, DatePipe, RelativeTimePipe],
  templateUrl: './users-table.html',
  styleUrl: './users-table.scss',
})
export class UsersTable {
  readonly users = input.required<UserListItem[]>();
  readonly sort = input.required<UserSortField>();
  readonly direction = input.required<SortDirection>();
  readonly currentUserId = input<string | null>(null);
  /** Id of the row with a request in flight; its menu is disabled. */
  readonly busyId = input<string | null>(null);

  readonly sortChange = output<{ sort: UserSortField; dir: SortDirection }>();
  readonly deactivate = output<UserListItem>();
  readonly reactivate = output<UserListItem>();

  protected readonly columns = ['displayName', 'role', 'status', 'lastLogin', 'createdAt', 'actions'];

  protected onSort(event: Sort): void {
    this.sortChange.emit({
      sort: event.active as UserSortField,
      dir: event.direction === 'desc' ? 'desc' : 'asc',
    });
  }
}
