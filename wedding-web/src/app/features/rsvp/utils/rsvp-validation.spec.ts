import { validateOverview } from './rsvp-validation';
import type { PersonDraft } from './rsvp-completion';

function person(attending: 'Yes' | 'No' | null, hasAllergies: boolean | null = null, allergiesText = ''): PersonDraft {
  return {
    attending,
    dinnerAttending: null,
    eveningPartyAttending: null,
    hasAllergies,
    allergiesText,
  };
}

describe('validateOverview', () => {
  it('fails when any person is incomplete', () => {
    const res = validateOverview({
      personDrafts: {
        a: person('Yes', null),
        b: person('No'),
      },
      dinner: { attendance: 'None', singleAttendeePersonId: null },
      eveningParty: { attendance: 'None', singleAttendeePersonId: null },
    });

    expect(res.ok).toBeFalse();
    expect(res.errors.length).toBeGreaterThan(0);
  });

  it('fails when attendance=One but no one is attending Yes', () => {
    const res = validateOverview({
      personDrafts: {
        a: person('No'),
      },
      dinner: { attendance: 'One', singleAttendeePersonId: null },
      eveningParty: { attendance: 'None', singleAttendeePersonId: null },
    });

    expect(res.ok).toBeFalse();
    expect(res.errors.join(' ')).toContain('Diner');
  });

  it('fails when attendance=One but no person selected', () => {
    const res = validateOverview({
      personDrafts: {
        a: person('Yes', false),
      },
      dinner: { attendance: 'One', singleAttendeePersonId: null },
      eveningParty: { attendance: 'None', singleAttendeePersonId: null },
    });

    expect(res.ok).toBeFalse();
    expect(res.errors.join(' ')).toContain('kies');
  });

  it('fails when selected person is not attending Yes', () => {
    const res = validateOverview({
      personDrafts: {
        a: person('No'),
        b: person('Yes', false),
      },
      dinner: { attendance: 'One', singleAttendeePersonId: 'a' },
      eveningParty: { attendance: 'None', singleAttendeePersonId: null },
    });

    expect(res.ok).toBeFalse();
    expect(res.errors.join(' ')).toContain('Ja');
  });

  it('passes for a complete happy path', () => {
    const res = validateOverview({
      personDrafts: {
        a: person('Yes', false),
        b: person('No'),
      },
      dinner: { attendance: 'All', singleAttendeePersonId: null },
      eveningParty: { attendance: 'One', singleAttendeePersonId: 'a' },
    });

    expect(res.ok).toBeTrue();
    expect(res.errors.length).toBe(0);
  });
});
