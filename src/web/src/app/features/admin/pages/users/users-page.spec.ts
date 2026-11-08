import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginator } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { environment } from '../../../../../environments/environment';
import { PagedResult, UserListItem } from '../../../../core/api/api.models';
import { SessionService } from '../../../../core/services/session.service';
import { TenantService } from '../../../../core/services/tenant.service';
import { mockSession, mockTenantService, testMe } from '../../../../core/testing/session-mocks';
import { UsersFilters } from '../../components/users-filters/users-filters';
import { UsersTable } from '../../components/users-table/users-table';
import { SEARCH_DEBOUNCE_MS, UsersPage } from './users-page';

const usersUrl = `${environment.apiBaseUrl}/api/demo/users`;
const tick = (ms = 0) => new Promise((resolve) => setTimeout(resolve, ms));

function user(id: string, displayName: string, isActive = true): UserListItem {
  return {
    id,
    email: `${displayName.toLowerCase()}@demo.example`,
    displayName,
    role: 'User',
    isActive,
    createdAtUtc: '2026-01-01T00:00:00Z',
    lastLoginAtUtc: null,
  };
}

function page(items: UserListItem[], totalCount = items.length, pageNumber = 1): PagedResult<UserListItem> {
  return { items, page: pageNumber, pageSize: 10, totalCount };
}

describe('UsersPage', () => {
  let fixture: ComponentFixture<UsersPage>;
  let http: HttpTestingController;
  let dialogResult: boolean;
  const dialog = { open: vi.fn(() => ({ afterClosed: () => of(dialogResult) })) };
  const snackBar = { open: vi.fn() };

  beforeEach(async () => {
    dialogResult = true;
    dialog.open.mockClear();
    snackBar.open.mockClear();

    await TestBed.configureTestingModule({
      imports: [UsersPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: mockSession('ready', testMe('Admin')) }, // my id is '1'
        { provide: TenantService, useValue: mockTenantService() },
        { provide: MatDialog, useValue: dialog },
        { provide: MatSnackBar, useValue: snackBar },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(UsersPage);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  async function settle(): Promise<void> {
    await tick();
    fixture.detectChanges();
  }

  /** The single pending GET /users; its query string is what the page asked the server for. */
  function expectList(): TestRequest {
    return http.expectOne((req) => req.method === 'GET' && req.url === usersUrl);
  }

  function params(req: TestRequest): Record<string, string> {
    return Object.fromEntries(req.request.params.keys().map((k) => [k, req.request.params.get(k)!]));
  }

  async function loadFirstPage(items = [user('2', 'Bob'), user('3', 'Cleo')], total = items.length): Promise<void> {
    expectList().flush(page(items, total));
    await settle();
  }

  const child = <T>(type: new (...args: never[]) => T): T => fixture.debugElement.query(By.directive(type)).componentInstance as T;

  it('asks the server for the first page, sorted by name', async () => {
    const req = expectList();
    expect(params(req)).toEqual({ sort: 'displayName', dir: 'asc', page: '1', pageSize: '10' });
    req.flush(page([user('2', 'Bob')]));
    await settle();

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="users-table"] tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('bob@demo.example');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="roles-note"] a')?.getAttribute('href')).toBe(
      'https://entra.microsoft.com',
    );
  });

  it('debounces the search box by 300 ms and goes back to page 1', async () => {
    await loadFirstPage([user('2', 'Bob')], 30);
    child(MatPaginator).page.emit({ pageIndex: 2, pageSize: 10, length: 30 });
    await settle();
    expectList().flush(page([user('2', 'Bob')], 30, 3));
    await settle();

    const input = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="search"]') as HTMLInputElement;
    for (const value of ['a', 'al', 'ali']) {
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    await tick(SEARCH_DEBOUNCE_MS / 2);
    http.expectNone((req) => req.url === usersUrl); // still typing

    await tick(SEARCH_DEBOUNCE_MS);
    fixture.detectChanges();
    const req = expectList();
    expect(params(req)).toMatchObject({ search: 'ali', page: '1' });
    req.flush(page([]));
    await settle();
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="empty-state"]')).not.toBeNull();
  });

  it('sends role and status filters to the server', async () => {
    await loadFirstPage();

    child(UsersFilters).roleChange.emit('Admin');
    await settle();
    let req = expectList();
    expect(params(req)).toMatchObject({ role: 'Admin', page: '1' });
    req.flush(page([]));
    await settle();

    child(UsersFilters).statusChange.emit('inactive');
    await settle();
    req = expectList();
    expect(params(req)).toMatchObject({ role: 'Admin', status: 'inactive' });
    req.flush(page([]));
  });

  it('pages and sorts on the server', async () => {
    await loadFirstPage([user('2', 'Bob')], 25);

    child(MatPaginator).page.emit({ pageIndex: 1, pageSize: 25, length: 25, previousPageIndex: 0 });
    await settle();
    let req = expectList();
    expect(params(req)).toMatchObject({ page: '2', pageSize: '25' });
    req.flush(page([user('4', 'Dev')], 25, 2));
    await settle();

    child(UsersTable).sortChange.emit({ sort: 'lastLogin', dir: 'desc' });
    await settle();
    req = expectList();
    expect(params(req)).toMatchObject({ sort: 'lastLogin', dir: 'desc', page: '1' });
    req.flush(page([]));
  });

  it('deactivates after confirmation, shows a snackbar and reloads', async () => {
    await loadFirstPage();

    child(UsersTable).deactivate.emit(user('2', 'Bob'));
    await settle();

    expect(dialog.open).toHaveBeenCalledOnce();
    const post = http.expectOne(`${usersUrl}/2/deactivate`);
    expect(post.request.method).toBe('POST');
    post.flush(user('2', 'Bob', false));
    await settle();

    expect(snackBar.open).toHaveBeenCalledWith('Bob was deactivated.', 'OK', expect.anything());
    expectList().flush(page([user('2', 'Bob', false), user('3', 'Cleo')]));
  });

  it('does nothing when the confirmation is cancelled', async () => {
    await loadFirstPage();
    dialogResult = false;

    child(UsersTable).deactivate.emit(user('2', 'Bob'));
    await settle();

    expect(dialog.open).toHaveBeenCalledOnce();
    http.expectNone(`${usersUrl}/2/deactivate`);
    expect(snackBar.open).not.toHaveBeenCalled();
  });

  it('shows the API reason when the change fails', async () => {
    await loadFirstPage();

    child(UsersTable).reactivate.emit(user('3', 'Cleo', false));
    await settle();
    http
      .expectOne(`${usersUrl}/3/reactivate`)
      .flush({ detail: 'No user with this id exists in your organization.' }, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(snackBar.open).toHaveBeenCalledWith('No user with this id exists in your organization.', 'OK', expect.anything());
  });

  it("disables the row menu for the signed-in admin's own row", async () => {
    await loadFirstPage([user('1', 'Me Myself'), user('2', 'Bob')]);

    const menus = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('[data-testid="row-menu"]');
    expect(menus[0].disabled).toBe(true);
    expect(menus[1].disabled).toBe(false);
  });
});
