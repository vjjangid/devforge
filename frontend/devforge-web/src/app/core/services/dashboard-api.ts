import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DashboardSummary } from '../models/dashboard';
import { API_BASE_URL } from './api-config';

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/dashboard/summary`;

  /** "Deployments today" is counted from the viewer's local midnight rather than the server's. */
  summary(now: Date = new Date()): Observable<DashboardSummary> {
    const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).toISOString();
    return this.http.get<DashboardSummary>(this.url, { params: { todayStart } });
  }
}
