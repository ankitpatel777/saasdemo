import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { HealthResponse, HealthService } from '../../../../core/services/health';
import { Login } from './login';

describe('Login', () => {
  async function render(health$: Observable<HealthResponse>): Promise<ComponentFixture<Login>> {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [{ provide: HealthService, useValue: { getHealth: () => health$ } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(Login);
    await fixture.whenStable();
    return fixture;
  }

  const statusText = (f: ComponentFixture<Login>) =>
    (f.nativeElement as HTMLElement).querySelector('[data-testid="api-status"]')?.textContent;

  it('shows the API status', async () => {
    const f = await render(of({ status: 'ok', version: '1', timestampUtc: '' }));
    expect(statusText(f)).toContain('API status: ok');
  });

  it('shows unreachable when the API call fails', async () => {
    const f = await render(throwError(() => new Error('down')));
    expect(statusText(f)).toContain('API status: unreachable');
  });
});
