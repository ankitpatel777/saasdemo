import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TenantService } from '../services/tenant.service';
import { AuditEntry, PagedResult, SignInEvent } from './api.models';

@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);
  private readonly tenant = inject(TenantService);

  /** The tenant's audit log, newest first (Admin only). */
  list(page: number, pageSize: number): Observable<PagedResult<AuditEntry>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<AuditEntry>>(this.tenant.apiUrl('audit'), { params });
  }

  /** The signed-in user's own last sign-ins, newest first. */
  myActivity(): Observable<SignInEvent[]> {
    return this.http.get<SignInEvent[]>(this.tenant.apiUrl('me/activity'));
  }
}
