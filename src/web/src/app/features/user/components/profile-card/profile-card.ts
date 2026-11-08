import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MeResponse } from '../../../../core/services/session.service';

/** Presentational: who I am, in which organization, with which role. */
@Component({
  selector: 'app-profile-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule],
  template: `
    <mat-card appearance="outlined" data-testid="profile-card">
      <mat-card-content class="profile">
        <div class="avatar" aria-hidden="true">{{ initials() }}</div>
        <div>
          <h2 class="name">{{ me().displayName }}</h2>
          <p class="email">{{ me().email }}</p>
          <p class="meta">
            <span class="role">{{ me().role }}</span>
            <span>{{ me().tenant.name }}</span>
          </p>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .profile { display: flex; align-items: center; gap: 1.25rem; }
    .avatar { width: 64px; height: 64px; border-radius: 50%; display: grid; place-items: center; flex: none;
      background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); font: var(--mat-sys-title-large); }
    .name { margin: 0; font: var(--mat-sys-title-large); }
    .email { margin: 0.25rem 0; color: var(--mat-sys-on-surface-variant); word-break: break-all; }
    .meta { margin: 0; display: flex; gap: 0.75rem; align-items: center; flex-wrap: wrap; }
    .role { padding: 0.1rem 0.6rem; border-radius: 999px; font: var(--mat-sys-label-medium);
      background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
  `,
})
export class ProfileCard {
  readonly me = input.required<MeResponse>();

  protected readonly initials = computed(() =>
    this.me()
      .displayName.split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join(''),
  );
}
