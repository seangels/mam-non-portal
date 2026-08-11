import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { SetupService } from './setup.service';

describe('SetupService', () => {
  let service: SetupService;
  let http: HttpTestingController;
  const baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(SetupService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('marks setup as required when the database has no users', async () => {
    const statePromise = service.loadStatus();
    const request = http.expectOne(`${baseUrl}/setup/status`);
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({ requiresInitialization: true });

    expect(await statePromise).toBe('required');
    expect(service.state).toBe('required');
  });

  it('marks setup complete after creating the first SuperAdmin', async () => {
    const createdPromise = service.createSuperAdmin({
      email: 'superadmin@example.com',
      fullName: 'Super Admin',
      password: 'StrongSetupPassword1!'
    });
    const request = http.expectOne(`${baseUrl}/setup/super-admin`);
    expect(request.request.method).toBe('POST');
    request.flush({ id: 'user-id', email: 'superadmin@example.com', fullName: 'Super Admin' });

    expect((await createdPromise).id).toBe('user-id');
    expect(service.state).toBe('complete');
  });

  it('keeps a retryable error state when setup status cannot be loaded', async () => {
    const statePromise = service.loadStatus();
    http.expectOne(`${baseUrl}/setup/status`).flush(
      { title: 'Database unavailable' },
      { status: 503, statusText: 'Service Unavailable' }
    );

    expect(await statePromise).toBe('error');
    expect(service.error?.status).toBe(503);
  });
});
