import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './signup.html',
  styleUrl: './signup.css'
})
export class SignupComponent {
  user:any = {};
  showPassword:boolean = false;
  showConfirmPassword:boolean = false;
  isLoading:boolean = false;

  constructor(
    private auth:AuthService,
    private router:Router,
    private toast: ToastService
  ){}

  // Toggle password visibility
  togglePassword(){
    this.showPassword = !this.showPassword;
  }

  toggleConfirmPassword(){
    this.showConfirmPassword = !this.showConfirmPassword;
  }

  // Password validations
  hasUppercase():boolean{
    return /[A-Z]/.test(this.user.password || '');
  }

  hasSpecial():boolean{
    return /[!@#$%^&*(),.?":{}|<>]/.test(this.user.password || '');
  }

  hasLength():boolean{
    return (this.user.password || '').length >= 8;
  }

  passwordsMatch():boolean{
    return this.user.password === this.user.confirmPassword;
  }

  validMobile():boolean{
    return /^[0-9]{10}$/.test(this.user.mobileNumber || '');
  }

  isFormValid():boolean{
    return this.hasLength() &&
           this.hasUppercase() &&
           this.hasSpecial() &&
           this.passwordsMatch() &&
           this.validMobile();
  }

  register(){

    if(!this.isFormValid()){
      this.toast.error("Please fix validation errors");
      return;
    }

    this.isLoading = true;
    this.auth.register(this.user).subscribe({

      next:(res)=>{
        this.isLoading = false;
        this.toast.success("User registered successfully! Redirecting to login...");

        // Redirect to login after signup
        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 2000);
      },

      error:(err)=>{
        this.isLoading = false;
        const errorMsg = err.error?.message || err.error || "Registration failed";
        this.toast.error(errorMsg);
      }

    });

  }

}