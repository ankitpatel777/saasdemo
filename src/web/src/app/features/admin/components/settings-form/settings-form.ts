import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export const NAME_MIN = 2;
export const NAME_MAX = 100;

/** Presentational: the organization name form. Emits the trimmed name; the page saves it. */
@Component({
  selector: 'app-settings-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <form (submit)="$event.preventDefault(); submit()" novalidate data-testid="settings-form">
      <mat-form-field appearance="outline" class="field">
        <mat-label>Organization name</mat-label>
        <input matInput [formControl]="name" data-testid="name" autocomplete="organization" [maxlength]="max" />
        <mat-hint align="end">{{ name.value.length }}/{{ max }}</mat-hint>
        @if (name.hasError('required') || name.hasError('minlength') || name.hasError('blank')) {
          <mat-error>Enter at least {{ min }} characters.</mat-error>
        }
        @if (name.hasError('maxlength')) {
          <mat-error>Use at most {{ max }} characters.</mat-error>
        }
      </mat-form-field>
      <div class="actions">
        <button mat-flat-button type="submit" [disabled]="saving() || name.invalid || unchanged()" data-testid="save">
          {{ saving() ? 'Saving…' : 'Save' }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .field { width: 100%; max-width: 480px; }
    .actions { margin-top: 0.5rem; }
  `,
})
export class SettingsForm {
  readonly currentName = input.required<string>();
  readonly saving = input(false);
  readonly save = output<string>();

  protected readonly min = NAME_MIN;
  protected readonly max = NAME_MAX;

  protected readonly name = new FormControl('', {
    nonNullable: true,
    validators: [
      Validators.required,
      Validators.minLength(NAME_MIN),
      Validators.maxLength(NAME_MAX),
      (c) => ((c.value as string).trim().length < NAME_MIN ? { blank: true } : null),
    ],
  });

  constructor() {
    // Show the stored name, and again after each successful save.
    effect(() => this.name.setValue(this.currentName()));
  }

  protected unchanged(): boolean {
    return this.name.value.trim() === this.currentName();
  }

  protected submit(): void {
    if (this.name.valid && !this.unchanged()) {
      this.save.emit(this.name.value.trim());
    }
  }
}
