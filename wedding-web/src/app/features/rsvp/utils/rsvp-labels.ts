import type { AttendingStatus, EventAttendance, EventType } from '../../../api/types';

export function labelAttending(v: AttendingStatus): string {
  return v === 'Yes' ? 'Ja' : 'Nee';
}

export function labelEventType(v: EventType): string {
  return v === 'Dinner' ? 'Diner' : 'Avondfeest';
}

export function labelEventAttendance(v: EventAttendance): string {
  switch (v) {
    case 'None':
      return 'Niemand';
    case 'All':
      return 'Iedereen';
    case 'One':
      return '1 persoon';
    case 'Some':
      return 'Meerdere';
  }

  // Should be unreachable if EventAttendance is exhaustive.
  return 'Onbekend';
}
