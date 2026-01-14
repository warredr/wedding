import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class ApiClient {
  private readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');
  private static readonly SESSION_TOKEN_KEY = 'wv_rsvp_session_token_v1';
  private static readonly SESSION_TOKEN_HEADER = 'X-Rsvp-Session';

  constructor(private readonly http: HttpClient) { }

  get<T>(path: string, params?: Record<string, string>, headers?: Record<string, string>): Observable<T> {
    const token = ApiClient.tryGetSessionToken();
    const mergedHeaders = token
      ? { ...(headers ?? {}), [ApiClient.SESSION_TOKEN_HEADER]: token }
      : headers;

    return this.http.get<T>(this.url(path), {
      withCredentials: true,
      params: params ? new HttpParams({ fromObject: params }) : undefined,
      headers: mergedHeaders,
    });
  }

  post<T>(path: string, body?: unknown, params?: Record<string, string>, headers?: Record<string, string>): Observable<T> {
    const token = ApiClient.tryGetSessionToken();
    const mergedHeaders = token
      ? { ...(headers ?? {}), [ApiClient.SESSION_TOKEN_HEADER]: token }
      : headers;

    return this.http.post<T>(this.url(path), body ?? {}, {
      withCredentials: true,
      params: params ? new HttpParams({ fromObject: params }) : undefined,
      headers: mergedHeaders,
    });
  }

  static setSessionToken(token: string | null | undefined): void {
    try {
      if (!token) {
        sessionStorage.removeItem(ApiClient.SESSION_TOKEN_KEY);
        return;
      }

      sessionStorage.setItem(ApiClient.SESSION_TOKEN_KEY, token);
    } catch {
      // ignore
    }
  }

  private static tryGetSessionToken(): string | null {
    try {
      const raw = sessionStorage.getItem(ApiClient.SESSION_TOKEN_KEY);
      return raw && raw.trim().length > 0 ? raw.trim() : null;
    } catch {
      return null;
    }
  }

  // Helper for consistent error parsing if needed later.
  static toError(err: unknown): HttpErrorResponse | null {
    return err instanceof HttpErrorResponse ? err : null;
  }

  private url(path: string): string {
    if (path.startsWith('/')) {
      return `${this.baseUrl}${path}`;
    }

    return `${this.baseUrl}/${path}`;
  }
}
