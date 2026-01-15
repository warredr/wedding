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

  private lastPointerUpAt = 0;

  constructor(private readonly router: Router) {}

  back(ev?: Event): void {
    // Support both pointer taps and keyboard activation.
    // Some browsers (notably iOS) can fire a synthetic click after pointerup; ignore the click in that case.
    if (ev instanceof MouseEvent && Date.now() - this.lastPointerUpAt < 700) {
      return;
    }

    if (ev instanceof PointerEvent) {
      this.lastPointerUpAt = Date.now();
    }

    this.backPressed.emit();
    if (this.preventDefaultBack) {
      return;
    }

    void this.router.navigate(Array.isArray(this.backTo) ? this.backTo : [this.backTo]);
  }
}
