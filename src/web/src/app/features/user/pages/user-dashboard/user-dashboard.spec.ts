import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserDashboard } from './user-dashboard';

describe('UserDashboard', () => {
  let component: UserDashboard;
  let fixture: ComponentFixture<UserDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserDashboard],
    }).compileComponents();

    fixture = TestBed.createComponent(UserDashboard);
    fixture.componentRef.setInput('tenant', 'acme');
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('renders the tenant from the route', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Tenant: acme');
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
