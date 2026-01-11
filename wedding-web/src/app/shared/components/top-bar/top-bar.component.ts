import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-top-bar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './top-bar.component.html',
})
export class TopBarComponent {
  @Input() title = '';
  @Input() backTo: any[] | string = '/welcome';
  @Input() backLabel = 'Terug';
  @Input() showBackArrow = false;
  @Input() preventDefaultBack = false;

  @Output() backPressed = new EventEmitter<void>();

  constructor(private readonly router: Router) {}

  back(): void {
    this.backPressed.emit();
    if (this.preventDefaultBack) {
      return;
    }

    void this.router.navigate(Array.isArray(this.backTo) ? this.backTo : [this.backTo]);
  }
}
