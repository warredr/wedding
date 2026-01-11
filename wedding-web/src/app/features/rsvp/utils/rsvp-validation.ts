import type { EventAttendance } from '../../../api/types';
import { isPersonCompleted, type PersonDraft } from './rsvp-completion';

export interface EventDraft {
  attendance: EventAttendance | null;
  singleAttendeePersonId: string | null;
}

export interface OverviewDraftState {
  personDrafts: Record<string, PersonDraft>;
  dinner: EventDraft;
  eveningParty: EventDraft;
}

export interface OverviewValidationResult {
  ok: boolean;
  errors: string[];
}

export function validateOverview(state: OverviewDraftState): OverviewValidationResult {
  const errors: string[] = [];

  const personIds = Object.keys(state.personDrafts);
  const anyoneAttendingYes = personIds.some((id) => state.personDrafts[id]?.attending === 'Yes');

  if (!personIds.every((id) => isPersonCompleted(state.personDrafts[id]!))) {
    errors.push('Vul alle personen in voor je bevestigt.');
  }

  validateEvent('Diner', state.dinner, state.personDrafts, anyoneAttendingYes, errors);
  validateEvent('Avondfeest', state.eveningParty, state.personDrafts, anyoneAttendingYes, errors);

  return { ok: errors.length === 0, errors };
}

function validateEvent(
  label: string,
  draft: EventDraft,
  personDrafts: Record<string, PersonDraft>,
  anyoneAttendingYes: boolean,
  errors: string[]
): void {
  if (draft.attendance !== 'One') {
    return;
  }

  if (!anyoneAttendingYes) {
    errors.push(`${label}: kies minstens één persoon met Ja.`);
    return;
  }

  if (!draft.singleAttendeePersonId) {
    errors.push(`${label}: kies welke persoon gaat.`);
    return;
  }

  const selected = personDrafts[draft.singleAttendeePersonId];
  if (!selected || selected.attending !== 'Yes') {
    errors.push(`${label}: de gekozen persoon moet op Ja staan.`);
  }
}
