import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';

/** Shape of the RFC 9457 problem details the API returns for every error. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

const UNREACHABLE_STATUS = 0;

/** An API failure normalised into something the UI can show without inspecting HTTP details. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly fieldErrors: Readonly<Record<string, string[]>> = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isNotFound(): boolean {
    return this.status === HttpStatusCode.NotFound;
  }

  static from(error: unknown): ApiError {
    if (error instanceof ApiError) {
      return error;
    }

    if (!(error instanceof HttpErrorResponse)) {
      return new ApiError(UNREACHABLE_STATUS, 'Something went wrong. Please try again.');
    }

    if (error.status === UNREACHABLE_STATUS) {
      return new ApiError(UNREACHABLE_STATUS, 'Cannot reach the DevForge API. Check that it is running.');
    }

    const problem = (typeof error.error === 'object' && error.error !== null ? error.error : {}) as ProblemDetails;
    const fieldErrors = problem.errors ?? {};
    const message =
      problem.detail ??
      Object.values(fieldErrors).flat()[0] ??
      problem.title ??
      `The request failed with status ${error.status}.`;

    return new ApiError(error.status, message, fieldErrors);
  }
}
