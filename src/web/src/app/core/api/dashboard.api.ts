import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TenantService } from '../services/tenant.service';
import { DashboardSummary } from './api.models';

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);
  private readonly tenant = inject(TenantService);

  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(this.tenant.apiUrl('dashboard/summary'));
  }
}
