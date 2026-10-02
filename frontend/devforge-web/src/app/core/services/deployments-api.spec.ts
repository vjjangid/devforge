import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { aDeployment } from '../../testing/fixtures';
import { DeploymentsApi } from './deployments-api';

describe('DeploymentsApi', () => {
  let api: DeploymentsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(DeploymentsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('queues a deployment for an application, passing the failure switch', () => {
    api.create('app-1', { simulateFailure: true }).subscribe();

    const call = http.expectOne({ method: 'POST', url: '/api/applications/app-1/deployments' });
    expect(call.request.body).toEqual({ simulateFailure: true });
    call.flush(aDeployment('Queued'));
  });

  it('lists the deployments of an application', () => {
    api.listForApplication('app-1').subscribe();

    http.expectOne({ method: 'GET', url: '/api/applications/app-1/deployments' }).flush([]);
  });

  it('gets a deployment', () => {
    api.get('dep-1').subscribe();

    http.expectOne({ method: 'GET', url: '/api/deployments/dep-1' }).flush(aDeployment('Running'));
  });

  it('fetches all logs when no cursor is given', () => {
    api.logs('dep-1').subscribe();

    const call = http.expectOne((request) => request.url === '/api/deployments/dep-1/logs');
    expect(call.request.params.has('afterId')).toBe(false);
    call.flush([]);
  });

  it('fetches only newer logs when a cursor is given', () => {
    api.logs('dep-1', 12).subscribe();

    const call = http.expectOne((request) => request.url === '/api/deployments/dep-1/logs');
    expect(call.request.params.get('afterId')).toBe('12');
    call.flush([]);
  });
});
