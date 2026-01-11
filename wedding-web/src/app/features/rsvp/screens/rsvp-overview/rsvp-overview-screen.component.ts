import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { RsvpApi } from '../../../../api/rsvp-api';
import type { EventAttendance } from '../../../../api/types';
import { SegmentedControlComponent, type SegmentedOption } from '../../../../shared/components/segmented-control/segmented-control.component';
import { TopBarComponent } from '../../../../shared/components/top-bar/top-bar.component';
import { RsvpDraftService } from '../../services/rsvp-draft.service';
import { labelAttending } from '../../utils/rsvp-labels';
import { validateOverview } from '../../utils/rsvp-validation';

@Component({
  selector: 'app-rsvp-overview-screen',
  standalone: true,
  imports: [CommonModule, FormsModule, TopBarComponent, SegmentedControlComponent],
  templateUrl: './rsvp-overview-screen.component.html',
})
export class RsvpOverviewScreenComponent {
  saving = false;
  error: string | null = null;
  validationErrors: string[] = [];

  readonly attendanceOptions: Array<SegmentedOption<EventAttendance>> = [
    { value: 'None', label: 'Niemand' },
    { value: 'All', label: 'Iedereen' },
    { value: 'One', label: '1 persoon' },
    { value: 'Some', label: 'Sommigen' },
  ];

  constructor(
    private readonly api: RsvpApi,
    private readonly router: Router,
    readonly draft: RsvpDraftService
  ) {}

  hasUnsavedChanges(): boolean {
    return this.draft.isDirty();
  }

  labelAttending(v: 'Yes' | 'No'): string {
    return labelAttending(v);
  }

  dinnerAttendees(): Array<{ personId: string; fullName: string }> {
    const s = this.draft.state();
    if (!s) return [];
    return s.members
      .filter((m) => s.personDrafts[m.personId]?.attending === 'Yes')
      .map((m) => ({ personId: m.personId, fullName: m.fullName }));
  }

  isDinnerAttending(personId: string): boolean {
    const s = this.draft.state();
    if (!s) return false;
    return s.personDrafts[personId]?.dinnerAttending === true;
  }

  isEveningPartyAttending(personId: string): boolean {
    const s = this.draft.state();
    if (!s) return false;
    return s.personDrafts[personId]?.eveningPartyAttending === true;
  }

  setDinnerAttendance(v: string): void {
    if (!isEventAttendance(v)) {
      return;
    }
    this.draft.setDinnerAttendance(v);
  }

  setEveningPartyAttendance(v: string): void {
    if (!isEventAttendance(v)) {
      return;
    }
    this.draft.setEveningPartyAttendance(v);
  }

  confirm(): void {
    const s = this.draft.state();
    if (!s) return;

    this.validationErrors = [];
    this.error = null;

    const validation = validateOverview({
      personDrafts: s.personDrafts,
      dinner: { attendance: s.dinner.attendance, singleAttendeePersonId: s.dinner.singleAttendeePersonId },
      eveningParty: {
        attendance: s.eveningParty.attendance,
        singleAttendeePersonId: s.eveningParty.singleAttendeePersonId,
      },
    });

    if (!validation.ok) {
      this.validationErrors = validation.errors;
      return;
    }

    this.saving = true;
    const payload = this.draft.buildSubmitRequest();

    this.api.submitGroup(s.groupId, s.sessionId, payload).subscribe({
      next: () => {
        this.saving = false;
        void this.router.navigate(['/welcome'], { state: { showDoneModal: true } });
      },
      error: () => {
        this.saving = false;
        this.error = 'Bevestigen mislukt. Probeer opnieuw.';
      },
    });
  }
}

function isEventAttendance(v: string): v is EventAttendance {
  return v === 'None' || v === 'All' || v === 'One' || v === 'Some';
}
