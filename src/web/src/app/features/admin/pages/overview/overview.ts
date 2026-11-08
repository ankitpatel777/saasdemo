import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';
import { AuditApi } from '../../../../core/api/audit.api';
import { DashboardApi } from '../../../../core/api/dashboard.api';
import { SessionService } from '../../../../core/services/session.service';
import { ActivityList } from '../../../../shared/components/activity-list/activity-list';
import { EmptyState, ErrorState, Skeleton } from '../../../../shared/components/page-states/page-states';
import { StatCard } from '../../../../shared/components/stat-card/stat-card';

/** Smart page: loads the summary and a preview of recent activity; presentational children render them. */
@Component({
  selector: 'app-admin-overview',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatButtonModule, RouterLink, StatCard, ActivityList, Skeleton, EmptyState, ErrorState],
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
})
export class AdminOverview {
  private readonly dashboardApi = inject(DashboardApi);
  private readonly auditApi = inject(AuditApi);
  protected readonly session = inject(SessionService);

  protected readonly summary = rxResource({ stream: () => this.dashboardApi.getSummary() });
  protected readonly recent = rxResource({ stream: () => this.auditApi.list(1, 5) });
}
