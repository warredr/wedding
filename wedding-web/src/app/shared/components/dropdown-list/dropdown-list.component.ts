import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface DropdownListItem<T = unknown> {
  id: string;
  primary: string;
  secondary?: string;
  disabled?: boolean;
  class?: string;
  isConfirmed?: boolean;
  data?: T;
}

@Component({
  selector: 'app-dropdown-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dropdown-list.component.html',
})
export class DropdownListComponent<T = unknown> {
  @Input({ required: true }) items!: Array<DropdownListItem<T>>;

  @Output() selectItem = new EventEmitter<DropdownListItem<T>>();

  private lastPointerUpAt = 0;

  onActivate(item: DropdownListItem<T>, ev?: Event): void {
    if (item.disabled) {
      return;
    }

    // Some browsers can fire a synthetic click after pointerup; ignore the click in that case.
    if (ev instanceof MouseEvent && Date.now() - this.lastPointerUpAt < 700) {
      return;
    }

    if (ev instanceof PointerEvent) {
      this.lastPointerUpAt = Date.now();
    }

    this.selectItem.emit(item);
  }
}
