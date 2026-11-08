import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { vi } from 'vitest';
import { environment } from '../../../../../environments/environment';
import { SessionService } from '../../../../core/services/session.service';
import { TenantService } from '../../../../core/services/tenant.service';
import { mockSession, mockTenantService, testMe } from '../../../../core/testing/session-mocks';
import { SettingsPage } from './settings-page';

const settingsUrl = `${environment.apiBaseUrl}/api/demo/settings`;
const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('SettingsPage', () => {
  let fixture: ComponentFixture<SettingsPage>;
  let http: HttpTestingController;
  let session: SessionService;
  const snackBar = { open: vi.fn() };

  beforeEach(async () => {
    snackBar.open.mockClear();
    session = mockSession('ready', testMe('Admin'));

    await TestBed.configureTestingModule({
      imports: [SettingsPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: session },
        { provide: TenantService, useValue: mockTenantService() },
        { provide: MatSnackBar, useValue: snackBar },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SettingsPage);
    fixture.detectChanges();
    await tick();
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const input = () => el().querySelector('[data-testid="name"]') as HTMLInputElement;
  const save = () => el().querySelector('[data-testid="save"]') as HTMLButtonElement;

  /** The fixture isn't attached to the document, where jsdom submits a form on button click: submit directly. */
  function submit(): void {
    el().querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  function type(value: string): void {
    input().value = value;
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('starts with the current name and nothing to save', () => {
    expect(input().value).toBe('Demo Client');
    expect(save().disabled).toBe(true);
  });

  it('rejects names shorter than 2 or only spaces', () => {
    type('x');
    expect(save().disabled).toBe(true);
    type('     ');
    expect(save().disabled).toBe(true);
  });

  it('saves the trimmed name, refreshes the session and confirms', async () => {
    type('  Demo Holdings  ');
    expect(save().disabled).toBe(false);

    submit();
    expect(save().textContent).toContain('Saving');

    const req = http.expectOne(settingsUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ name: 'Demo Holdings' });
    req.flush({ name: 'Demo Holdings' });
    await tick();
    await tick();

    expect(session.refresh).toHaveBeenCalledOnce();
    expect(snackBar.open).toHaveBeenCalledWith('Organization renamed to Demo Holdings.', 'OK', expect.anything());
  });

  it('shows the API reason when saving fails', async () => {
    type('Demo Holdings');
    submit();

    http
      .expectOne(settingsUrl)
      .flush({ detail: 'The organization name must be 2–100 characters.' }, { status: 400, statusText: 'Bad Request' });
    await tick();

    expect(session.refresh).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('The organization name must be 2–100 characters.', 'OK', expect.anything());
  });
});
