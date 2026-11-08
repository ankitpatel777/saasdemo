import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../../environments/environment';
import { SessionService } from '../../../../core/services/session.service';
import { mockSession, testMe } from '../../../../core/testing/session-mocks';
import { AdminDashboard } from './admin-dashboard';

describe('AdminDashboard', () => {
  let fixture: ComponentFixture<AdminDashboard>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminDashboard],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: mockSession('ready', testMe('Admin')) },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AdminDashboard);
    fixture.componentRef.setInput('tenant', 'demo');
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('shows who is signed in', () => {
    http.expectOne(`${environment.apiBaseUrl}/api/demo/users`).flush([]);
    expect(text()).toContain('Tenant: demo');
    expect(text()).toContain('Signed in as Me Myself');
  });

  it("lists the tenant's users", async () => {
    http.expectOne(`${environment.apiBaseUrl}/api/demo/users`).flush([
      { id: '1', email: 'me@example.com', displayName: 'Me Myself', role: 'Admin' },
      { id: '2', email: 'bob@example.com', displayName: 'Bob', role: 'User' },
    ]);
    await fixture.whenStable();

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="users-table"] tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[1].textContent).toContain('bob@example.com');
  });
});
