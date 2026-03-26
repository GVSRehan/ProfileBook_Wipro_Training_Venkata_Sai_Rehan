import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [FormsModule, RouterModule],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.css'
})
export class ResetPasswordComponent implements OnInit {
  form = {
    email: '',
    token: '',
    newPassword: '',
    confirmPassword: ''
  };
  showNewPassword = false;
  showConfirmPassword = false;
  isSubmitting = false;

  constructor(
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.form.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.form.token = this.route.snapshot.queryParamMap.get('token') ?? '';
  }

  hasUppercase(): boolean {
    return /[A-Z]/.test(this.form.newPassword);
  }

  hasSpecial(): boolean {
    return /[!@#$%^&*(),.?":{}|<>]/.test(this.form.newPassword);
  }

  hasLength(): boolean {
    return this.form.newPassword.length >= 8;
  }

  passwordsMatch(): boolean {
    return this.form.newPassword === this.form.confirmPassword && this.form.confirmPassword.length > 0;
  }

  resetPassword(): void {
    if (this.isSubmitting) {
      return;
    }

    if (!this.form.email.trim() || !this.form.token.trim()) {
      this.toast.error('Enter your email and reset code');
      return;
    }

    if (!this.hasLength() || !this.hasUppercase() || !this.hasSpecial()) {
      this.toast.error('New password does not meet the project password rules');
      return;
    }

    if (this.form.newPassword !== this.form.confirmPassword) {
      this.toast.error('Passwords do not match');
      return;
    }

    this.isSubmitting = true;

    this.auth.resetPassword({
      email: this.form.email.trim(),
      token: this.form.token.trim(),
      newPassword: this.form.newPassword,
      confirmPassword: this.form.confirmPassword
    }).subscribe({
      next: (response: any) => {
        this.toast.success(response?.message ?? 'Password reset successfully');
        this.router.navigate(['/login']);
      },
      error: (error: any) => {
        this.toast.error(extractServerMessage(error) ?? 'Unable to reset password');
      },
      complete: () => {
        this.isSubmitting = false;
      }
    });
  }
}

function extractServerMessage(error: any): string | null {
  if (typeof error?.error === 'string') {
    return error.error;
  }

  if (typeof error?.error?.message === 'string') {
    return error.error.message;
  }

  if (typeof error?.message === 'string') {
    return error.message;
  }

  return null;
}
