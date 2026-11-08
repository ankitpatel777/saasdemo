import { BreakpointObserver } from '@angular/cdk/layout';
import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, map } from 'rxjs';
import { SessionService } from '../../../core/services/session.service';

interface NavItem {
  label: string;
  icon: string;
  link: string;
  exact: boolean;
}

/**
 * The signed-in layout: toolbar (organization, user, role, sign out) and, inside the Admin or User
 * area, a side navigation that collapses into a drawer on small screens. Page content is projected.
 */
@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgTemplateOutlet,
    MatToolbarModule,
    MatSidenavModule,
    MatIconModule,
    MatButtonModule,
    RouterLink,
    RouterLinkActive,
  ],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
})
export class AppShell {
  protected readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly sidenav = viewChild(MatSidenav);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 839.98px)')
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** Admin area: four sections. User area: overview only. Elsewhere (e.g. /forbidden): no navigation. */
  protected readonly navItems = computed<NavItem[]>(() => {
    const me = this.session.me();
    const [, slug, area] = this.url().split(/[/?#]/);
    if (!me || slug !== me.tenant.slug) {
      return [];
    }
    if (area === 'admin' && me.role === 'Admin') {
      const base = `/${slug}/admin`;
      return [
        { label: 'Overview', icon: 'dashboard', link: base, exact: true },
        { label: 'Users', icon: 'group', link: `${base}/users`, exact: false },
        { label: 'Activity', icon: 'history', link: `${base}/activity`, exact: false },
        { label: 'Settings', icon: 'settings', link: `${base}/settings`, exact: false },
      ];
    }
    if (area === 'user') {
      return [{ label: 'Overview', icon: 'dashboard', link: `/${slug}/user`, exact: true }];
    }
    return [];
  });

  /** On phones the drawer closes after picking a destination. */
  protected onNavigate(): void {
    if (this.isHandset()) {
      void this.sidenav()?.close();
    }
  }
}
