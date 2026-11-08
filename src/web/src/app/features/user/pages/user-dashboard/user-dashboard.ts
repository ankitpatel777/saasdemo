import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { AuditApi } from '../../../../core/api/audit.api';
import { SessionService } from '../../../../core/services/session.service';
import { EmptyState, ErrorState, Skeleton } from '../../../../shared/components/page-states/page-states';
import { ProfileCard } from '../../components/profile-card/profile-card';
import { SignInList } from '../../components/sign-in-list/sign-in-list';

/** Smart page: my profile (from the session) and my recent sign-ins (from the API). */
@Component({
  selector: 'app-user-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, ProfileCard, SignInList, Skeleton, EmptyState, ErrorState],
  template: `
    <h1>Overview</h1>
    <div class="grid">
      @if (session.me(); as me) {
        <app-profile-card [me]="me" />
      } @else {
        <app-skeleton [lines]="1" [height]="96" />
      }

      <mat-card appearance="outlined">
        <mat-card-header>
          <mat-card-title><h2>My recent sign-ins</h2></mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (activity.isLoading()) {
            <app-skeleton [lines]="4" />
          } @else if (activity.error()) {
            <app-error-state message="We couldn't load your sign-ins." (retry)="activity.reload()" />
          } @else if (activity.value()?.length) {
            <app-sign-in-list [signIns]="activity.value()!" />
          } @else {
            <app-empty-state message="No sign-ins recorded yet." icon="login" />
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); align-items: start; }
    h2 { margin: 0; font: var(--mat-sys-title-large); }
  `,
})
export class UserDashboard {
  private readonly auditApi = inject(AuditApi);
  protected readonly session = inject(SessionService);

  protected readonly activity = rxResource({ stream: () => this.auditApi.myActivity() });
}
