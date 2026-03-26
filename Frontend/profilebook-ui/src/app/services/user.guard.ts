import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class UserGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(): boolean {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return false;
    }

    const role = this.authService.getUserRole()?.toLowerCase();
    if (role === 'user') {
      return true;
    }

    if (role === 'admin') {
      this.router.navigate(['/admin/dashboard']);
      return false;
    }

    this.authService.logout();
    return false;
  }
}
