import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterModule], // RouterModule added
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class LoginComponent {

  loginData:any = {};
  isSubmitting = false;

  showPassword:boolean = false;

  constructor(
    private auth:AuthService,
    private router:Router,
    private toast: ToastService
  ){}

  togglePassword(){
    this.showPassword = !this.showPassword;
  }

  login(){
  if (this.isSubmitting) {
    return;
  }

  const identifier = `${this.loginData.identifier ?? this.loginData.email ?? ''}`.trim();
  const password = `${this.loginData.password ?? ''}`;

  if (!identifier || !password) {
    this.toast.error('Enter your email, username, or mobile number and password');
    return;
  }

  this.isSubmitting = true;

  this.auth.login({
    email: identifier,
    identifier,
    password
  }).subscribe({

    next:(res:any)=>{

      // backend may return camelCase `token` and `role` or PascalCase.
      const token = res.token ?? res.Token;
      const role = (res.role ?? res.Role) as string | undefined;
      const isMainAdmin = Boolean(res.isMainAdmin ?? res.IsMainAdmin);

      if (token) {
        this.auth.saveToken(token);
      }

      this.auth.saveSessionMeta(role ?? null, isMainAdmin);

      if (role && role.toLowerCase() === 'admin') {
        this.toast.success('Welcome Admin');
        this.router.navigate(['/admin/dashboard']);
      } else {
        this.toast.success('Welcome User');
        this.router.navigate(['/user-dashboard']);
      }

    },

    error:(err:any)=>{
      this.isSubmitting = false;
      this.toast.error(extractServerMessage(err) ?? 'Unable to login');
    },

    complete: () => {
      this.isSubmitting = false;
    }

  });

}
}

function extractServerMessage(err: any): string | null {
  if (typeof err?.error === 'string') {
    return err.error;
  }

  if (typeof err?.error?.message === 'string') {
    return err.error.message;
  }

  const validationErrors = err?.error?.errors;
  if (validationErrors && typeof validationErrors === 'object') {
    const firstEntry = Object.values(validationErrors).find((value) => Array.isArray(value) && value.length > 0) as string[] | undefined;
    if (firstEntry?.length) {
      return firstEntry[0];
    }
  }

  if (typeof err?.message === 'string') {
    return err.message;
  }

  return null;
}
