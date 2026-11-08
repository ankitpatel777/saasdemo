import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/** Presentational: grey placeholder lines while data loads. */
@Component({
  selector: 'app-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="skeleton" role="status" aria-live="polite" data-testid="skeleton">
      <span class="visually-hidden">Loading…</span>
      @for (line of lineArray(); track $index) {
        <div class="line" [style.height.px]="height()"></div>
      }
    </div>
  `,
  styles: `
    .line { border-radius: 8px; margin: 0.5rem 0; background: var(--mat-sys-surface-container-high);
      animation: pulse 1.4s ease-in-out infinite; }
    @keyframes pulse { 50% { opacity: 0.5; } }
    @media (prefers-reduced-motion: reduce) { .line { animation: none; } }
  `,
})
export class Skeleton {
  readonly lines = input(3);
  readonly height = input(20);
  protected readonly lineArray = computed(() => Array.from({ length: this.lines() }));
}

/** Presentational: nothing to show (yet). */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="state" data-testid="empty-state">
      <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
      <p>{{ message() }}</p>
    </div>
  `,
  styles: `
    .state { text-align: center; padding: 2rem 1rem; color: var(--mat-sys-on-surface-variant); }
    mat-icon { font-size: 40px; width: 40px; height: 40px; }
  `,
})
export class EmptyState {
  readonly message = input.required<string>();
  readonly icon = input('inbox');
}

/** Presentational: loading failed, with a retry button. */
@Component({
  selector: 'app-error-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatButtonModule],
  template: `
    <div class="state" role="alert" data-testid="error-state">
      <mat-icon aria-hidden="true">error</mat-icon>
      <p>{{ message() }}</p>
      <button mat-stroked-button type="button" (click)="retry.emit()">Try again</button>
    </div>
  `,
  styles: `
    .state { text-align: center; padding: 2rem 1rem; color: var(--mat-sys-error); }
    mat-icon { font-size: 40px; width: 40px; height: 40px; }
  `,
})
export class ErrorState {
  readonly message = input('Something went wrong while loading this page.');
  readonly retry = output<void>();
}
