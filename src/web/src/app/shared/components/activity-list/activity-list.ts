import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatList, MatListItem, MatListItemIcon, MatListItemLine, MatListItemTitle } from '@angular/material/list';
import { AuditEntry } from '../../../core/api/api.models';
import { auditIcon, describeAudit } from '../../audit/describe-audit';
import { RelativeTimePipe } from '../../pipes/relative-time.pipe';

/** Presentational: audit entries as readable sentences with relative times (absolute on hover). */
@Component({
  selector: 'app-activity-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatList, MatListItem, MatListItemIcon, MatListItemTitle, MatListItemLine, MatIconModule, RelativeTimePipe, DatePipe],
  template: `
    <mat-list data-testid="activity-list">
      @for (entry of entries(); track entry.id) {
        <mat-list-item>
          <mat-icon matListItemIcon aria-hidden="true">{{ icon(entry) }}</mat-icon>
          <span matListItemTitle>{{ describe(entry) }}</span>
          <time matListItemLine [attr.datetime]="entry.occurredAtUtc" [title]="entry.occurredAtUtc | date: 'medium'">
            {{ entry.occurredAtUtc | relativeTime }}
          </time>
        </mat-list-item>
      }
    </mat-list>
  `,
})
export class ActivityList {
  readonly entries = input.required<AuditEntry[]>();

  protected describe = describeAudit;
  protected icon = (entry: AuditEntry) => auditIcon(entry.action);
}
