import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';   // 👈 ADD
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [FormsModule, RouterModule],   // 👈 ADD
  templateUrl: './signup.html',
  styleUrl: './signup.css'
})
export class SignupComponent {

  user:any = {};

  constructor(private auth:AuthService){}

  register(){

    this.auth.register(this.user).subscribe({
      next:(res)=>{
        alert(res);
      },
      error:(err)=>{
        alert(err.error);
      }

    });

  }

}