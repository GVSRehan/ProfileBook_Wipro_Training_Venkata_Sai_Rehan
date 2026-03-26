import { Injectable } from '@angular/core';
import { Observable, defer, from } from 'rxjs';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

@Injectable({
  providedIn: 'root'
})
export class FetchClientService {
  get<T>(url: string, useAuth = true): Observable<T> {
    return this.request<T>('GET', url, undefined, useAuth);
  }

  post<T>(url: string, body?: unknown, useAuth = true): Observable<T> {
    return this.request<T>('POST', url, body, useAuth);
  }

  put<T>(url: string, body?: unknown, useAuth = true): Observable<T> {
    return this.request<T>('PUT', url, body, useAuth);
  }

  delete<T>(url: string, useAuth = true): Observable<T> {
    return this.request<T>('DELETE', url, undefined, useAuth);
  }

  private request<T>(method: HttpMethod, url: string, body?: unknown, useAuth = true): Observable<T> {
    return defer(() => from(this.requestPromise<T>(method, url, body, useAuth)));
  }

  private async requestPromise<T>(method: HttpMethod, url: string, body?: unknown, useAuth = true): Promise<T> {
    const headers = new Headers();

    if (useAuth) {
      const token = this.getToken();
      if (token) {
        headers.set('Authorization', `Bearer ${token}`);
      }
    }

    let requestBody: BodyInit | undefined;
    if (body instanceof FormData) {
      requestBody = body;
    } else if (body !== undefined) {
      headers.set('Content-Type', 'application/json');
      requestBody = JSON.stringify(body);
    }

    const response = await fetch(url, {
      method,
      headers,
      body: requestBody
    });

    const parsedBody = await this.parseResponseBody(response);

    if (!response.ok) {
      if (response.status === 401 && useAuth) {
        this.handleUnauthorized();
      }

      throw {
        status: response.status,
        error: parsedBody,
        message: this.extractErrorMessage(parsedBody, response.statusText)
      };
    }

    return parsedBody as T;
  }

  private async parseResponseBody(response: Response): Promise<unknown> {
    if (response.status === 204) {
      return null;
    }

    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('application/json')) {
      return response.json();
    }

    const text = await response.text();
    if (!text) {
      return null;
    }

    try {
      return JSON.parse(text);
    } catch {
      return text;
    }
  }

  private extractErrorMessage(parsedBody: unknown, fallback: string): string {
    if (typeof parsedBody === 'string' && parsedBody.trim()) {
      return parsedBody;
    }

    if (parsedBody && typeof parsedBody === 'object' && 'message' in parsedBody) {
      const value = (parsedBody as { message?: unknown }).message;
      if (typeof value === 'string' && value.trim()) {
        return value;
      }
    }

    return fallback || 'Request failed';
  }

  private getToken(): string | null {
    if (typeof window === 'undefined') {
      return null;
    }

    return window.sessionStorage.getItem('authToken');
  }

  private handleUnauthorized(): void {
    if (typeof window === 'undefined') {
      return;
    }

    window.sessionStorage.removeItem('authToken');
    window.sessionStorage.removeItem('userRole');
    window.sessionStorage.removeItem('isMainAdmin');
    window.location.href = '/login';
  }
}
