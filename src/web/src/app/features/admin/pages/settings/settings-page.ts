import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { SettingsApi } from '../../../../core/api/settings.api';
import { SessionService, problemDetail } from '../../../../core/services/session.service';
import { SettingsForm } from '../../components/settings-form/settings-form';

/** Smart page: saves the organization name, then refreshes the session so the toolbar shows it. */
@Component({
  selector: 'app-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, SettingsForm],
  template: `
    <h1>Settings</h1>
    <mat-card appearance="outlined">
      <mat-card-header>
        <mat-card-title>Organization</mat-card-title>
        <mat-card-subtitle>The address of this app ({{ slug() }}) stays the same.</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <app-settings-form [currentName]="name()" [saving]="saving()" (save)="save($event)" />
      </mat-card-content>
    </mat-card>
  `,
})
export class SettingsPage {
  private readonly settingsApi = inject(SettingsApi);
  private readonly session = inject(SessionService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly name = computed(() => this.session.me()?.tenant.name ?? '');
  protected readonly slug = computed(() => this.session.me()?.tenant.slug ?? '');
  protected readonly saving = signal(false);

  protected async save(name: string): Promise<void> {
    this.saving.set(true);
    try {
      const saved = await firstValueFrom(this.settingsApi.rename(name));
      await this.session.refresh(); // the header (and this form) now show the new name
      this.snackBar.open(`Organization renamed to ${saved.name}.`, 'OK', { duration: 4000 });
    } catch (error) {
      this.snackBar.open(problemDetail(error) ?? "Couldn't save the name. Please try again.", 'OK', {
        duration: 6000,
        politeness: 'assertive',
      });
    } finally {
      this.saving.set(false);
    }
  }
}
