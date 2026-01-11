import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface SegmentedOption<T extends string> {
  value: T;
  label: string;
}

@Component({
  selector: 'app-segmented-control',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './segmented-control.component.html',
})
export class SegmentedControlComponent<T extends string> {
  @Input({ required: true }) options!: Array<SegmentedOption<T>>;
  @Input({ required: true }) value!: T | null;
  @Output() valueChange = new EventEmitter<T>();

  set(v: T): void {
    this.valueChange.emit(v);
  }
}
