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

  onSelect(item: DropdownListItem<T>): void {
    if (item.disabled) {
      return;
    }

    this.selectItem.emit(item);
  }
}
