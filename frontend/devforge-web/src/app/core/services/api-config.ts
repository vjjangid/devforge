import { InjectionToken } from '@angular/core';

/**
 * Root of the DevForge API. Relative on purpose: the dev server and the production nginx
 * both proxy `/api` to the backend, so the same build works everywhere without CORS.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => '/api',
});
