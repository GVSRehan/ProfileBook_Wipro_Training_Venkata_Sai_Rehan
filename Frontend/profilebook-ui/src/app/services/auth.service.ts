import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { FetchClientService } from './fetch-client.service';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private api = `${environment.apiBaseUrl}/api/User`;
  private readonly tokenKey = 'authToken';
  private readonly roleKey = 'userRole';
  private readonly mainAdminKey = 'isMainAdmin';

  constructor(private fetchClient: FetchClientService, private router: Router) {}

  register(data: any): Observable<any> {
    return this.fetchClient.post(`${this.api}/register`, data, false);
  }

  login(data: any): Observable<any> {
    return this.fetchClient.post(`${this.api}/login`, data, false);
  }

  forgotPassword(data: any): Observable<any> {
    return this.fetchClient.post(`${this.api}/forgot-password`, data, false);
  }

  resetPassword(data: any): Observable<any> {
    return this.fetchClient.post(`${this.api}/reset-password`, data, false);
  }

  getUsers(): Observable<any> {
    return this.fetchClient.get(`${this.api}`);
  }

  deleteUser(id: number): Observable<any> {
    return this.fetchClient.delete(`${this.api}/${id}`);
  }

  // JWT token management
  saveToken(token: string): void {
    sessionStorage.setItem(this.tokenKey, token);
  }

  getToken(): string | null {
    return sessionStorage.getItem(this.tokenKey);
  }

  isLoggedIn(): boolean {
    return !!this.getValidTokenPayload();
  }

  logout(redirectToLogin = true): void {
    this.clearSession();
    if (redirectToLogin) {
      this.router.navigate(['/login']);
    }
  }

  clearSession(): void {
    sessionStorage.removeItem(this.tokenKey);
    sessionStorage.removeItem(this.roleKey);
    sessionStorage.removeItem(this.mainAdminKey);
  }

  handleUnauthorized(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }

  getUserId(): number | null {
    const payload = this.getValidTokenPayload();
    const rawUserId = payload?.['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
    if (typeof rawUserId === 'string') {
      const parsedUserId = Number.parseInt(rawUserId, 10);
      return Number.isNaN(parsedUserId) ? null : parsedUserId;
    }

    return null;
  }

  getUserRole(): string | null {
    const payload = this.getValidTokenPayload();
    const role = payload?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    return typeof role === 'string' ? role : null;
  }

  isMainAdmin(): boolean {
    const payload = this.getValidTokenPayload();
    const claim = payload?.['is_main_admin'];
    if (typeof claim === 'boolean') {
      return claim;
    }

    if (typeof claim === 'string') {
      return claim.toLowerCase() === 'true';
    }

    return false;
  }

  saveSessionMeta(role: string | null, isMainAdmin: boolean): void {
    if (role) {
      sessionStorage.setItem(this.roleKey, role);
    } else {
      sessionStorage.removeItem(this.roleKey);
    }

    sessionStorage.setItem(this.mainAdminKey, String(isMainAdmin));
  }

  private getValidTokenPayload(): Record<string, unknown> | null {
    const token = this.getToken();
    if (!token) {
      return null;
    }

    const payload = this.decodeTokenPayload(token);
    if (!payload) {
      this.clearSession();
      return null;
    }

    const expiry = payload['exp'];
    if (typeof expiry === 'number' && expiry * 1000 <= Date.now()) {
      this.clearSession();
      return null;
    }

    return payload;
  }

  private decodeTokenPayload(token: string): Record<string, unknown> | null {
    try {
      const payloadSegment = token.split('.')[1];
      if (!payloadSegment) {
        return null;
      }

      const normalizedBase64 = payloadSegment
        .replace(/-/g, '+')
        .replace(/_/g, '/')
        .padEnd(Math.ceil(payloadSegment.length / 4) * 4, '=');

      return JSON.parse(atob(normalizedBase64));
    } catch {
      return null;
    }
  }
}
