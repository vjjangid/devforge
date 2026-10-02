import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './api-error';

describe('ApiError.from', () => {
  it('uses the problem detail as the message', () => {
    const error = ApiError.from(
      new HttpErrorResponse({ status: 409, error: { title: 'Conflict', detail: 'Name already exists.' } }),
    );

    expect(error.status).toBe(409);
    expect(error.message).toBe('Name already exists.');
  });

  it('exposes field errors and falls back to the first one for the message', () => {
    const error = ApiError.from(
      new HttpErrorResponse({
        status: 400,
        error: { title: 'One or more validation errors occurred.', errors: { name: ['Name is required.'] } },
      }),
    );

    expect(error.fieldErrors).toEqual({ name: ['Name is required.'] });
    expect(error.message).toBe('Name is required.');
  });

  it('explains that the API is unreachable when there is no response', () => {
    const error = ApiError.from(new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') }));

    expect(error.message).toContain('Cannot reach the DevForge API');
  });

  it('copes with a non-JSON error body', () => {
    const error = ApiError.from(new HttpErrorResponse({ status: 502, error: '<html>Bad Gateway</html>' }));

    expect(error.message).toBe('The request failed with status 502.');
  });

  it('flags not-found responses', () => {
    expect(ApiError.from(new HttpErrorResponse({ status: 404, error: {} })).isNotFound).toBe(true);
  });
});
