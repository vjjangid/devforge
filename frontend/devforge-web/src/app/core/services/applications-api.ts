import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Application, CreateApplicationRequest, UpdateApplicationRequest } from '../models/application';
import { API_BASE_URL } from './api-config';

@Injectable({ providedIn: 'root' })
export class ApplicationsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/applications`;

  list(): Observable<Application[]> {
    return this.http.get<Application[]>(this.url);
  }

  get(id: string): Observable<Application> {
    return this.http.get<Application>(`${this.url}/${id}`);
  }

  create(request: CreateApplicationRequest): Observable<Application> {
    return this.http.post<Application>(this.url, request);
  }

  update(id: string, request: UpdateApplicationRequest): Observable<Application> {
    return this.http.put<Application>(`${this.url}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
