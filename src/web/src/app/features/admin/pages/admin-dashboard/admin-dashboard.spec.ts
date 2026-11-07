import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminDashboard } from './admin-dashboard';

describe('AdminDashboard', () => {
  let component: AdminDashboard;
  let fixture: ComponentFixture<AdminDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminDashboard],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminDashboard);
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
