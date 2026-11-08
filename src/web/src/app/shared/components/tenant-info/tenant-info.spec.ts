import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../../../environments/environment';
import { TenantInfoCard } from './tenant-info';

describe('TenantInfoCard', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: ':tenant/admin', component: TenantInfoCard }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    harness = await RouterTestingHarness.create();
    http = TestBed.inject(HttpTestingController);
  });

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
