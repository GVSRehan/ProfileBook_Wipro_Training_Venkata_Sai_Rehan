import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class LoginComponent {

  loginData:any = {};

  constructor(
    private auth:AuthService,
    private router:Router
  ){}

  login(){

    this.auth.login(this.loginData).subscribe({

      next:(res:any)=>{

        if(res.role === "Admin"){
          alert("Welcome Admin 👑");
          this.router.navigate(['/admin-dashboard']);
        }

        else{
          alert("Welcome User 🎉");
          this.router.navigate(['/user-dashboard']);
        }

      },

      error:(err:any)=>{
        alert(err.error);
      }

    });

  }

}