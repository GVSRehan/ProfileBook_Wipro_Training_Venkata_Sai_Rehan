import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';  // ✅ ADD THIS

@Component({
  selector: 'app-welcome',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule      // ✅ REQUIRED for routerLink
  ],
  templateUrl: './welcome.html',
  styleUrls: ['./welcome.css']
})
export class WelcomeComponent {}