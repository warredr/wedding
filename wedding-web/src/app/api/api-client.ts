import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class ApiClient {
  private readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  get<T>(path: string, params?: Record<string, string>): Observable<T> {
    return this.http.get<T>(this.url(path), {
      withCredentials: true,
      params: params ? new HttpParams({ fromObject: params }) : undefined,
    });
  }

  post<T>(path: string, body?: unknown, params?: Record<string, string>): Observable<T> {
    return this.http.post<T>(this.url(path), body ?? {}, {
      withCredentials: true,
      params: params ? new HttpParams({ fromObject: params }) : undefined,
    });
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
