import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TenantService } from '../services/tenant.service';
import { TenantSettings } from './api.models';

@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private readonly http = inject(HttpClient);
  private readonly tenant = inject(TenantService);

  /** Renames the organization; returns the stored (trimmed) name. */
  rename(name: string): Observable<TenantSettings> {
    return this.http.put<TenantSettings>(this.tenant.apiUrl('settings'), { name });
  }
}
