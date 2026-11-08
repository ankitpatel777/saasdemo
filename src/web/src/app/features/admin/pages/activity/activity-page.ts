import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { AuditApi } from '../../../../core/api/audit.api';
import { ActivityList } from '../../../../shared/components/activity-list/activity-list';
import { EmptyState, ErrorState, Skeleton } from '../../../../shared/components/page-states/page-states';

/** Smart page: the organization's audit log, newest first, one server page at a time. */
@Component({
  selector: 'app-activity-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatPaginatorModule, ActivityList, Skeleton, EmptyState, ErrorState],
  template: `
    <h1>Activity</h1>
    <mat-card appearance="outlined">
      <mat-card-content>
        @if (log.isLoading()) {
          <app-skeleton [lines]="8" />
        } @else if (log.error()) {
          <app-error-state message="We couldn't load the activity log." (retry)="log.reload()" />
        } @else if (log.value(); as result) {
          @if (result.items.length) {
            <app-activity-list [entries]="result.items" />
          } @else {
            <app-empty-state message="No activity yet." icon="history" />
          }
          <mat-paginator
            [length]="result.totalCount"
            [pageIndex]="page() - 1"
            [pageSize]="pageSize()"
            [pageSizeOptions]="[20, 50]"
            (page)="onPage($event)"
            aria-label="Choose a page of activity"
          />
        }
      </mat-card-content>
    </mat-card>
  `,
})
export class ActivityPage {
  private readonly auditApi = inject(AuditApi);

  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);

  protected readonly log = rxResource({
    params: () => ({ page: this.page(), pageSize: this.pageSize() }),
    stream: ({ params }) => this.auditApi.list(params.page, params.pageSize),
  });

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.page.set(event.pageIndex + 1);
  }
}
