import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../../../environments/environment';
import { TENANT_INFO_ENABLED, TenantInfoCard } from './tenant-info';

async function setup(enabled: boolean) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: ':tenant/admin', component: TenantInfoCard }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: TENANT_INFO_ENABLED, useValue: enabled },
    ],
  });
  const harness = await RouterTestingHarness.create();
  return { harness, http: TestBed.inject(HttpTestingController) };
}

describe('TenantInfoCard (development)', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => ({ harness, http } = await setup(true)));
  afterEach(() => http.verify());

  it('shows the tenant name', async () => {
    await harness.navigateByUrl('/demo/admin');
    http
      .expectOne(`${environment.apiBaseUrl}/api/demo/info`)
      .flush({ tenantId: '1', slug: 'demo', name: 'Demo Client' });
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement?.textContent).toContain('Demo Client');
  });

  it('shows "Client not found" on 404', async () => {
    await harness.navigateByUrl('/unknown/admin');
    http
      .expectOne(`${environment.apiBaseUrl}/api/unknown/info`)
      .flush(null, { status: 404, statusText: 'Not Found' });
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement?.textContent).toContain('Client not found');
  });
});

describe('TenantInfoCard (production)', () => {
  it('renders nothing and makes no request', async () => {
    const { harness, http } = await setup(false);

    await harness.navigateByUrl('/demo/admin');
    await harness.fixture.whenStable();

    http.expectNone(() => true);
    http.verify();
    expect(harness.routeNativeElement?.textContent?.trim()).toBe('');
  });
});
