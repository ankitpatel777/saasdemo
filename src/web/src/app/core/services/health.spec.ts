import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { HealthResponse, HealthService } from './health';

describe('HealthService', () => {
  let service: HealthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(HealthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('calls GET {apiBaseUrl}/api/health', () => {
    const body: HealthResponse = { status: 'ok', version: '1.0.0', timestampUtc: '2026-01-01T00:00:00Z' };
    let result: HealthResponse | undefined;

    service.getHealth().subscribe((r) => (result = r));

    const req = http.expectOne(`${environment.apiBaseUrl}/api/health`);
    expect(req.request.method).toBe('GET');
    req.flush(body);
    expect(result).toEqual(body);
  });
});
