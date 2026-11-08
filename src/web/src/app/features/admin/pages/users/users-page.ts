import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { debounceTime, distinctUntilChanged, firstValueFrom, skip, tap } from 'rxjs';
import {
  PagedResult,
  SortDirection,
  UserListItem,
  UserListQuery,
  UserRole,
  UserSortField,
  UserStatus,
} from '../../../../core/api/api.models';
import { UsersApi } from '../../../../core/api/users.api';
import { SessionService, problemDetail } from '../../../../core/services/session.service';
import { ConfirmDialog, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { EmptyState, ErrorState, Skeleton } from '../../../../shared/components/page-states/page-states';
import { UsersFilters } from '../../components/users-filters/users-filters';
import { UsersTable } from '../../components/users-table/users-table';

export const SEARCH_DEBOUNCE_MS = 300;

type UserPage = PagedResult<UserListItem>;

/**
 * Smart page: owns the query (search, filters, sort, page) and talks to UsersApi. Every change asks the
 * server for exactly one page, so only that page ever reaches the browser.
 */
@Component({
  selector: 'app-users-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    UsersFilters,
    UsersTable,
    Skeleton,
    EmptyState,
    ErrorState,
  ],
  templateUrl: './users-page.html',
  styleUrl: './users-page.scss',
})
export class UsersPage {
  private readonly usersApi = inject(UsersApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly session = inject(SessionService);

  /** What is typed in the box (immediate) vs. what is sent (debounced). */
  protected readonly searchText = signal('');
  protected readonly role = signal<UserRole | null>(null);
  protected readonly status = signal<UserStatus | null>(null);
  protected readonly sort = signal<UserSortField>('displayName');
  protected readonly dir = signal<SortDirection>('asc');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly busyId = signal<string | null>(null);

  private readonly search = toSignal(
    toObservable(this.searchText).pipe(
      skip(1), // the initial '' is already the initial value
      debounceTime(SEARCH_DEBOUNCE_MS),
      distinctUntilChanged(),
      tap(() => this.page.set(1)),
    ),
    { initialValue: '' },
  );

  private readonly query = computed<UserListQuery>(() => ({
    search: this.search(),
    role: this.role(),
    status: this.status(),
    sort: this.sort(),
    dir: this.dir(),
    page: this.page(),
    pageSize: this.pageSize(),
  }));

  protected readonly users = rxResource({
    params: () => this.query(),
    stream: ({ params }) => this.usersApi.list(params),
  });

  /** The last page received: stays on screen (under a progress bar) while the next one loads. */
  protected readonly shown = linkedSignal<UserPage | undefined, UserPage | undefined>({
    source: this.users.value,
    computation: (value, previous) => value ?? previous?.value,
  });

  protected readonly currentUserId = computed(() => this.session.me()?.id ?? null);

  protected onRole(role: UserRole | null): void {
    this.role.set(role);
    this.page.set(1);
  }

  protected onStatus(status: UserStatus | null): void {
    this.status.set(status);
    this.page.set(1);
  }

  protected onSort(change: { sort: UserSortField; dir: SortDirection }): void {
    this.sort.set(change.sort);
    this.dir.set(change.dir);
    this.page.set(1);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.page.set(event.pageIndex + 1);
  }

  protected deactivate(user: UserListItem): Promise<void> {
    return this.changeStatus(user, false);
  }

  protected reactivate(user: UserListItem): Promise<void> {
    return this.changeStatus(user, true);
  }

  private async changeStatus(user: UserListItem, active: boolean): Promise<void> {
    const data: ConfirmDialogData = active
      ? {
          title: `Reactivate ${user.displayName}?`,
          message: 'They will be able to use the app again straight away.',
          confirmLabel: 'Reactivate',
        }
      : {
          title: `Deactivate ${user.displayName}?`,
          message:
            'They will lose access to this app immediately. Their Microsoft account and role in Entra are not changed.',
          confirmLabel: 'Deactivate',
          danger: true,
        };

    const confirmed = await firstValueFrom(
      this.dialog.open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data, autoFocus: 'dialog' }).afterClosed(),
    );
    if (!confirmed) {
      return;
    }

    this.busyId.set(user.id);
    try {
      await firstValueFrom(active ? this.usersApi.reactivate(user.id) : this.usersApi.deactivate(user.id));
      this.snackBar.open(`${user.displayName} was ${active ? 'reactivated' : 'deactivated'}.`, 'OK', { duration: 4000 });
      this.users.reload();
    } catch (error) {
      this.snackBar.open(problemDetail(error) ?? `Couldn't update ${user.displayName}. Please try again.`, 'OK', {
        duration: 6000,
        politeness: 'assertive',
      });
    } finally {
      this.busyId.set(null);
    }
  }
}
