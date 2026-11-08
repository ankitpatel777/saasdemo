import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { SessionService } from './core/services/session.service';
import { mockSession, testMe } from './core/testing/session-mocks';

describe('App', () => {
  async function render(session: SessionService) {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), { provide: SessionService, useValue: session }],
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('hides the toolbar when signed out', async () => {
    const el = await render(mockSession('anonymous', null, null));
    expect(el.querySelector('[data-testid="tenant-name"]')).toBeNull();
expect(el.querySelector('main')).not.toBeNull(); // pages still render, without the shell
  });

  it('shows name, tenant and role in the header when the session is ready', async () => {
    const el = await render(mockSession('ready', testMe('Admin')));
    expect(el.querySelector('[data-testid="display-name"]')?.textContent).toContain('Me Myself');
    expect(el.querySelector('[data-testid="tenant-name"]')?.textContent).toContain('Demo Client');
    expect(el.querySelector('[data-testid="role-badge"]')?.textContent).toContain('Admin');
  });
});
