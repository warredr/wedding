import { isPersonCompleted, type PersonDraft } from './rsvp-completion';

describe('isPersonCompleted', () => {
  it('returns false when attending is null', () => {
    const d: PersonDraft = {
      attending: null,
      dinnerAttending: null,
      eveningPartyAttending: null,
      hasAllergies: null,
      allergiesText: '',
    };
    expect(isPersonCompleted(d)).toBeFalse();
  });

  it('returns true when attending is No', () => {
    const d: PersonDraft = {
      attending: 'No',
      dinnerAttending: null,
      eveningPartyAttending: null,
      hasAllergies: null,
      allergiesText: '',
    };
    expect(isPersonCompleted(d)).toBeTrue();
  });

  it('returns false when attending Yes but allergies unanswered', () => {
    const d: PersonDraft = {
      attending: 'Yes',
      dinnerAttending: null,
      eveningPartyAttending: null,
      hasAllergies: null,
      allergiesText: '',
    };
    expect(isPersonCompleted(d)).toBeFalse();
  });

  it('returns true when attending Yes and allergies No', () => {
    const d: PersonDraft = {
      attending: 'Yes',
      dinnerAttending: null,
      eveningPartyAttending: null,
      hasAllergies: false,
      allergiesText: '',
    };
    expect(isPersonCompleted(d)).toBeTrue();
  });

  it('returns false when allergies Yes but text empty', () => {
    const d: PersonDraft = {
      attending: 'Yes',
      dinnerAttending: null,
      eveningPartyAttending: null,
      hasAllergies: true,
      allergiesText: '  ',
    };
    expect(isPersonCompleted(d)).toBeFalse();
  });

  it('returns true when allergies Yes with text', () => {
    const d: PersonDraft = {
      attending: 'Yes',
      dinnerAttending: null,
      eveningPartyAttending: null,
      hasAllergies: true,
      allergiesText: 'Noten',
    };
    expect(isPersonCompleted(d)).toBeTrue();
  });

  it('returns false when dinner is required but unanswered', () => {
    const d: PersonDraft = {
      attending: 'Yes',
      dinnerAttending: null,
      eveningPartyAttending: true,
      hasAllergies: false,
      allergiesText: '',
    };

    expect(isPersonCompleted(d, { dinner: true })).toBeFalse();
  });

  it('returns false when evening party is required but unanswered', () => {
    const d: PersonDraft = {
      attending: 'Yes',
      dinnerAttending: false,
      eveningPartyAttending: null,
      hasAllergies: false,
      allergiesText: '',
    };

    expect(isPersonCompleted(d, { eveningParty: true })).toBeFalse();
  });
});
