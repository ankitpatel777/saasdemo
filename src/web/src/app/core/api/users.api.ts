import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TenantService } from '../services/tenant.service';
import { PagedResult, UserListItem, UserListQuery } from './api.models';

/** Users of the current tenant. Paging, filtering and sorting all happen on the server. */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly tenant = inject(TenantService);

  list(query: UserListQuery): Observable<PagedResult<UserListItem>> {
    let params = new HttpParams()
      .set('sort', query.sort)
      .set('dir', query.dir)
      .set('page', query.page)
      .set('pageSize', query.pageSize);
    const search = query.search.trim();
    if (search) {
      params = params.set('search', search);
    }
    if (query.role) {
      params = params.set('role', query.role);
    }
    if (query.status) {
      params = params.set('status', query.status);
    }
    return this.http.get<PagedResult<UserListItem>>(this.tenant.apiUrl('users'), { params });
  }

  deactivate(id: string): Observable<UserListItem> {
    return this.http.post<UserListItem>(this.tenant.apiUrl(`users/${encodeURIComponent(id)}/deactivate`), null);
  }

  reactivate(id: string): Observable<UserListItem> {
    return this.http.post<UserListItem>(this.tenant.apiUrl(`users/${encodeURIComponent(id)}/reactivate`), null);
  }
}
