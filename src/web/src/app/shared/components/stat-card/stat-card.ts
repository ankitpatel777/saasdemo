import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

/** Presentational: one labelled number. */
@Component({
  selector: 'app-stat-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule],
  template: `
    <mat-card appearance="outlined" class="stat">
      <mat-card-content>
        <mat-icon aria-hidden="true" class="icon">{{ icon() }}</mat-icon>
        <div>
          <p class="label">{{ label() }}</p>
          <p class="value" data-testid="stat-value">{{ value() }}</p>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    mat-card-content { display: flex; align-items: center; gap: 1rem; }
    .icon { color: var(--mat-sys-primary); background: var(--mat-sys-primary-container); border-radius: 50%;
      padding: 0.6rem; box-sizing: content-box; }
    .label { margin: 0; font: var(--mat-sys-label-large); color: var(--mat-sys-on-surface-variant); }
    .value { margin: 0; font: var(--mat-sys-headline-medium); }
  `,
})
export class StatCard {
  readonly label = input.required<string>();
  readonly value = input.required<number>();
  readonly icon = input('insights');
}
