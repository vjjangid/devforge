import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { PlatformInfo } from '../models/platform';
import { API_BASE_URL } from './api-config';

@Injectable({ providedIn: 'root' })
export class PlatformApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/platform`;

  /** Platform facts do not change while the app is open, so they are fetched once and shared. */
  private readonly info$ = this.http.get<PlatformInfo>(this.url).pipe(shareReplay({ bufferSize: 1, refCount: false }));

  info(): Observable<PlatformInfo> {
    return this.info$;
  }
}
