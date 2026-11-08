import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../../environments/environment';
import { TenantService } from './tenant.service';

@Component({ template: '' })
class Blank {}

describe('TenantService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: ':tenant/admin', component: Blank },
          { path: '**', component: Blank },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
  });

  it('tracks the :tenant route param', async () => {
    const harness = await RouterTestingHarness.create();
    const service = TestBed.inject(TenantService);

    await harness.navigateByUrl('/demo/admin');
    expect(service.currentTenant()).toBe('demo');

    await harness.navigateByUrl('/login');
    expect(service.currentTenant()).toBeNull();
  });

  it('builds tenant-scoped API URLs', async () => {
    const harness = await RouterTestingHarness.create();
    const service = TestBed.inject(TenantService);

    await harness.navigateByUrl('/demo/admin');

    expect(service.apiUrl('/info')).toBe(`${environment.apiBaseUrl}/api/demo/info`);
  });

  it('throws when there is no tenant', () => {
    expect(() => TestBed.inject(TenantService).apiUrl('info')).toThrow();
  });
});
