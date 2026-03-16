import { Component } from '@angular/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-signup',
  standalone:true,
  templateUrl:'./signup.html',
  styleUrl:'./signup.css'
})
export class SignupComponent {

  user:any={};

  constructor(private auth:AuthService){}

  register(){

    this.auth.register(this.user).subscribe({
      next:(res)=>{
        alert("User registered successfully 👍 Please login");
      },
      error:(err)=>{
        alert(err.error);
      }
    });

  }

}