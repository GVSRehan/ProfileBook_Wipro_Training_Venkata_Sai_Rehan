import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.css'
})
export class ForgotPasswordComponent {
  form = {
    email: ''
  };
  isSubmitting = false;
  resetToken = '';
  expiresAt = '';

  constructor(
    private auth: AuthService,
    private router: Router,
    private toast: ToastService
  ) {}

  requestResetCode(): void {
    if (this.isSubmitting) {
      return;
    }

    const email = this.form.email.trim();
    if (!email) {
      this.toast.error('Enter your email address');
      return;
    }

    this.isSubmitting = true;

    this.auth.forgotPassword({ email }).subscribe({
      next: (response: any) => {
        this.resetToken = response?.resetToken ?? '';
        this.expiresAt = response?.expiresAt ?? '';
        this.toast.success(response?.message ?? 'Reset code generated successfully');
      },
      error: (error: any) => {
        this.toast.error(extractServerMessage(error) ?? 'Unable to generate reset code');
      },
      complete: () => {
        this.isSubmitting = false;
      }
    });
  }

  continueToReset(): void {
    this.router.navigate(['/reset-password'], {
      queryParams: {
        email: this.form.email.trim(),
        token: this.resetToken
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
