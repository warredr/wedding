import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-done-screen',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './done-screen.component.html',
})
export class DoneScreenComponent {
  constructor(private readonly router: Router) {}

  backToWelcome(): void {
    void this.router.navigate(['/welcome']);
  }
}
