import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateDeploymentRequest, Deployment, DeploymentLog } from '../models/deployment';
import { API_BASE_URL } from './api-config';

@Injectable({ providedIn: 'root' })
export class DeploymentsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  listForApplication(applicationId: string): Observable<Deployment[]> {
    return this.http.get<Deployment[]>(`${this.baseUrl}/applications/${applicationId}/deployments`);
  }

  create(applicationId: string, request: CreateDeploymentRequest): Observable<Deployment> {
    return this.http.post<Deployment>(`${this.baseUrl}/applications/${applicationId}/deployments`, request);
  }

  get(id: string): Observable<Deployment> {
    return this.http.get<Deployment>(`${this.baseUrl}/deployments/${id}`);
  }

  /** @param afterId Only fetch entries newer than this log id. */
  logs(id: string, afterId?: number): Observable<DeploymentLog[]> {
    const params = afterId === undefined ? new HttpParams() : new HttpParams().set('afterId', afterId);
    return this.http.get<DeploymentLog[]>(`${this.baseUrl}/deployments/${id}/logs`, { params });
  }
}
