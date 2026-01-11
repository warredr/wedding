import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import type { GroupMemberDto } from '../../../../api/types';

@Component({
  selector: 'app-wizard-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './wizard-header.component.html',
})
export class WizardHeaderComponent {
  @Input({ required: true }) members!: GroupMemberDto[];
  @Input({ required: true }) order!: string[];
  @Input({ required: true }) currentPersonId!: string;
  @Input({ required: true }) completedByPersonId!: Record<string, boolean>;

  @Output() selectPerson = new EventEmitter<string>();

  personName(personId: string): string {
    return this.members.find((m) => m.personId === personId)?.fullName ?? '';
  }
}
