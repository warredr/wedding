import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-toggle-yes-no',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toggle-yes-no.component.html',
})
export class ToggleYesNoComponent {
  @Input() label = '';
  @Input() value: boolean | null = null;
  @Output() valueChange = new EventEmitter<boolean>();

  set(v: boolean): void {
    this.valueChange.emit(v);
  }
}
