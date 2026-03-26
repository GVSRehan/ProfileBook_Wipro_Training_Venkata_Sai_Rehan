
import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,              // ✅ REQUIRED
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrls: ['./app.css']       // ✅ FIX (plural)
})
export class App {
  protected readonly title = signal('profilebook-ui');
}