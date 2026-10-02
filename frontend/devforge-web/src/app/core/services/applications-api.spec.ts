import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { anApplication } from '../../testing/fixtures';
import { apiErrorInterceptor } from '../interceptors/api-error.interceptor';
import { ApiError } from '../models/api-error';
import { ApplicationsApi } from './applications-api';

describe('ApplicationsApi', () => {
  let api: ApplicationsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    api = TestBed.inject(ApplicationsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists applications', () => {
    let result: unknown;
    api.list().subscribe((applications) => (result = applications));

    http.expectOne({ method: 'GET', url: '/api/applications' }).flush([anApplication()]);

    expect(result).toEqual([anApplication()]);
  });

  it('gets one application', () => {
    api.get('app-1').subscribe();

    http.expectOne({ method: 'GET', url: '/api/applications/app-1' }).flush(anApplication());
  });

  it('creates an application', () => {
    const request = { name: 'Todo API', repositoryUrl: 'github.com/vijay/todo-api', branch: 'main', runtime: 'dotnet-10', description: null };
    api.create(request).subscribe();

    const call = http.expectOne({ method: 'POST', url: '/api/applications' });
    expect(call.request.body).toEqual(request);
    call.flush(anApplication());
  });

  it('updates an application', () => {
    const request = { name: 'Renamed', repositoryUrl: 'github.com/vijay/todo-api', branch: 'develop', description: 'x' };
    api.update('app-1', request).subscribe();

    const call = http.expectOne({ method: 'PUT', url: '/api/applications/app-1' });
    expect(call.request.body).toEqual(request);
    call.flush(anApplication());
  });

  it('deletes an application', () => {
    let completed = false;
    api.delete('app-1').subscribe({ complete: () => (completed = true) });

    http.expectOne({ method: 'DELETE', url: '/api/applications/app-1' }).flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBe(true);
  });

  it('surfaces failures as ApiError', () => {
    let error: unknown;
    api.get('missing').subscribe({ error: (e: unknown) => (error = e) });

    http
      .expectOne('/api/applications/missing')
      .flush({ status: 404, detail: "Application 'missing' was not found." }, { status: 404, statusText: 'Not Found' });

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).isNotFound).toBe(true);
    expect((error as ApiError).message).toBe("Application 'missing' was not found.");
  });
});
