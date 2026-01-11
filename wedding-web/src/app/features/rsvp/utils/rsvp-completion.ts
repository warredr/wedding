import type { AttendingStatus } from '../../../api/types';

export interface PersonDraft {
  attending: AttendingStatus | null;
  dinnerAttending: boolean | null;
  eveningPartyAttending: boolean | null;
  hasAllergies: boolean | null;
  allergiesText: string;
}

export function isPersonCompleted(
  draft: PersonDraft,
  required?: { dinner?: boolean; eveningParty?: boolean }
): boolean {
  if (draft.attending === null) {
    return false;
  }

  if (draft.attending === 'No') {
    return true;
  }

  if (required?.dinner && draft.dinnerAttending === null) {
    return false;
  }

  if (required?.eveningParty && draft.eveningPartyAttending === null) {
    return false;
  }

  if (draft.hasAllergies === null) {
    return false;
  }

  if (draft.hasAllergies === false) {
    return true;
  }

  return draft.allergiesText.trim().length > 0;
}
