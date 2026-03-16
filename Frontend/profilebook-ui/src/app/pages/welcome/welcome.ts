import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-welcome',
  standalone: true,
  imports: [RouterModule],   // IMPORTANT
  templateUrl: './welcome.html',
  styleUrl: './welcome.css'
})
export class WelcomeComponent {

}