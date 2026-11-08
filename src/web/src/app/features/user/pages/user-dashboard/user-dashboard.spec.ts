import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../../../core/services/session.service';
import { mockSession, testMe } from '../../../../core/testing/session-mocks';
import { UserDashboard } from './user-dashboard';

describe('UserDashboard', () => {
  let fixture: ComponentFixture<UserDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserDashboard],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: mockSession('ready', testMe('User')) },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserDashboard);
    fixture.componentRef.setInput('tenant', 'demo');
    await fixture.whenStable();
  });

  it('renders the tenant from the route and who is signed in', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Tenant: demo');
    expect(text).toContain('Signed in as Me Myself');
    expect(text).toContain('User of Demo Client');
  });
});
