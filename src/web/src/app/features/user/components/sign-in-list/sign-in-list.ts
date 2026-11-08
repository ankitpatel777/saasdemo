import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatList, MatListItem, MatListItemIcon, MatListItemLine, MatListItemTitle } from '@angular/material/list';
import { SignInEvent } from '../../../../core/api/api.models';
import { RelativeTimePipe } from '../../../../shared/pipes/relative-time.pipe';

/** Presentational: my recent sign-ins. */
@Component({
  selector: 'app-sign-in-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatList, MatListItem, MatListItemIcon, MatListItemTitle, MatListItemLine, MatIconModule, DatePipe, RelativeTimePipe],
  template: `
    <mat-list data-testid="sign-ins">
      @for (signIn of signIns(); track signIn.occurredAtUtc) {
        <mat-list-item>
          <mat-icon matListItemIcon aria-hidden="true">login</mat-icon>
          <span matListItemTitle>{{ signIn.occurredAtUtc | date: 'medium' }}</span>
          <time matListItemLine [attr.datetime]="signIn.occurredAtUtc">{{ signIn.occurredAtUtc | relativeTime }}</time>
        </mat-list-item>
      }
    </mat-list>
  `,
})
export class SignInList {
  readonly signIns = input.required<SignInEvent[]>();
}
