import { Injectable, computed, effect, signal } from '@angular/core';
import type {
  AttendingStatus,
  ClaimGroupResponseDto,
  EventAttendance,
  GroupDto,
  GroupEventResponseDto,
  PersonResponseDto,
  SubmitRequestDto,
} from '../../../api/types';
import { isPersonCompleted, type PersonDraft } from '../utils/rsvp-completion';

export interface RsvpEventDraft {
  attendance: EventAttendance | null;
  singleAttendeePersonId: string | null;
}

export interface RsvpDraftState {
  groupId: string;
  sessionId: string;
  lockExpiresAtUtc: string;
  members: GroupDto['members'];
  memberOrder: string[];
  currentPersonId: string;
  personDrafts: Record<string, PersonDraft>;
  dinner: RsvpEventDraft;
  eveningParty: RsvpEventDraft;
  invitedToEvents: GroupDto['invitedToEvents'];
  initialSnapshot: string;
}

@Injectable({ providedIn: 'root' })
export class RsvpDraftService {
  readonly state = signal<RsvpDraftState | null>(null);

  constructor() {
    effect(() => {
      const s = this.state();
      if (!s) {
        return;
      }

      // Best-effort persistence so a reload mid-wizard keeps the local draft.
      try {
        sessionStorage.setItem(storageKey(s.groupId), JSON.stringify(toPersistedDraft(s)));
      } catch {
        // ignore
      }
    });
  }

  readonly currentPerson = computed(() => {
    const s = this.state();
    if (!s) return null;
    return s.members.find((m) => m.personId === s.currentPersonId) ?? null;
  });

  readonly currentPersonDraft = computed(() => {
    const s = this.state();
    if (!s) return null;
    return s.personDrafts[s.currentPersonId] ?? null;
  });

  readonly completionByPersonId = computed(() => {
    const s = this.state();
    if (!s) return {} as Record<string, boolean>;
    const map: Record<string, boolean> = {};
    const required = {
      dinner: s.invitedToEvents.includes('Dinner'),
      eveningParty: s.invitedToEvents.includes('EveningParty'),
    };
    for (const id of s.memberOrder) {
      map[id] = isPersonCompleted(s.personDrafts[id]!, required);
    }
    return map;
  });

  readonly isDirty = computed(() => {
    const s = this.state();
    if (!s) return false;
    return snapshotForDirtyCheck(s) !== s.initialSnapshot;
  });

  initialize(groupId: string, claim: ClaimGroupResponseDto, group: GroupDto, selectedPersonId: string | null): void {
    const memberOrder = reorderMembers(group.members.map((m) => m.personId), selectedPersonId);
    const currentPersonId = memberOrder[0] ?? group.members[0]?.personId;
    if (!currentPersonId) {
      throw new Error('Group has no members');
    }

    let personDrafts: Record<string, PersonDraft> = {};
    for (const m of group.members) {
      personDrafts[m.personId] = {
        attending: m.attending,
        dinnerAttending: null,
        eveningPartyAttending: null,
        hasAllergies: m.attending === 'Yes' ? m.hasAllergies : null,
        allergiesText: m.attending === 'Yes' && m.hasAllergies ? m.allergiesText ?? '' : '',
      };
    }

    const eventResponse = group.eventAttendance ?? defaultEventResponse();

    // If we have a stored eventResponse (e.g. lock refresh), map it back into per-person booleans
    // so the wizard and overview stay consistent.
    personDrafts = applyEventResponseToPersonDrafts(group, personDrafts, eventResponse);

    let state: RsvpDraftState = {
      groupId,
      sessionId: claim.sessionId,
      lockExpiresAtUtc: claim.lockExpiresAtUtc,
      members: group.members,
      memberOrder,
      currentPersonId,
      personDrafts,
      dinner: {
        attendance: null,
        singleAttendeePersonId: null,
      },
      eveningParty: {
        attendance: null,
        singleAttendeePersonId: null,
      },
      invitedToEvents: group.invitedToEvents,
      initialSnapshot: '',
    };

    const restored = readPersistedDraft(groupId);
    if (restored) {
      state = applyRestoredDraft(state, restored);
    }

    // Always derive event attendance from person-level answers.
    state = recomputeEventDrafts(state);

    state.initialSnapshot = snapshotForDirtyCheck(state);
    this.state.set(state);
  }

  clear(groupId?: string): void {
    const gid = groupId ?? this.state()?.groupId;
    if (gid) {
      try {
        sessionStorage.removeItem(storageKey(gid));
      } catch {
        // ignore
      }
    }
    this.state.set(null);
  }

  setCurrentPerson(personId: string): void {
    const s = this.state();
    if (!s) return;
    if (!s.memberOrder.includes(personId)) return;
    this.state.set({ ...s, currentPersonId: personId });
  }

  setAttending(personId: string, attending: AttendingStatus): void {
    const s = this.state();
    if (!s) return;
    const d = s.personDrafts[personId];
    if (!d) return;

    const next: PersonDraft =
      attending === 'No'
        ? {
            attending,
            dinnerAttending: false,
            eveningPartyAttending: false,
            hasAllergies: null,
            allergiesText: '',
          }
        : {
            attending,
            dinnerAttending: d.dinnerAttending,
            eveningPartyAttending: d.eveningPartyAttending,
            hasAllergies: d.hasAllergies,
            allergiesText: d.allergiesText,
          };

    this.state.set(recomputeEventDrafts({ ...s, personDrafts: { ...s.personDrafts, [personId]: next } }));
  }

  setDinnerAttending(personId: string, value: boolean): void {
    const s = this.state();
    if (!s) return;
    const d = s.personDrafts[personId];
    if (!d) return;

    const next = this.withAttendingDerivedFromEvents(s, { ...d, dinnerAttending: value });
    this.state.set(recomputeEventDrafts({ ...s, personDrafts: { ...s.personDrafts, [personId]: next } }));
  }

  setEveningPartyAttending(personId: string, value: boolean): void {
    const s = this.state();
    if (!s) return;
    const d = s.personDrafts[personId];
    if (!d) return;

    const next = this.withAttendingDerivedFromEvents(s, { ...d, eveningPartyAttending: value });
    this.state.set(recomputeEventDrafts({ ...s, personDrafts: { ...s.personDrafts, [personId]: next } }));
  }

  private withAttendingDerivedFromEvents(s: RsvpDraftState, d: PersonDraft): PersonDraft {
    const statuses: Array<boolean | null> = [];
    if (s.invitedToEvents.includes('Dinner')) statuses.push(d.dinnerAttending);
    if (s.invitedToEvents.includes('EveningParty')) statuses.push(d.eveningPartyAttending);

    // If the group isn't invited to any events, keep the explicit attending toggle.
    if (statuses.length === 0) {
      return d;
    }

    const allNull = statuses.every((v) => v === null);
    if (allNull) {
      return {
        ...d,
        attending: null,
        hasAllergies: null,
        allergiesText: '',
      };
    }

    const anyYes = statuses.some((v) => v === true);
    if (anyYes) {
      return {
        ...d,
        attending: 'Yes',
        hasAllergies: d.hasAllergies,
        allergiesText: d.allergiesText,
      };
    }

    const anyNull = statuses.some((v) => v === null);
    if (anyNull) {
      return {
        ...d,
        attending: null,
        hasAllergies: null,
        allergiesText: '',
      };
    }

    // All answered, and none are true.
    return {
      ...d,
      attending: 'No',
      hasAllergies: null,
      allergiesText: '',
    };
  }

  setHasAllergies(personId: string, hasAllergies: boolean): void {
    const s = this.state();
    if (!s) return;
    const d = s.personDrafts[personId];
    if (!d) return;

    const next: PersonDraft = {
      ...d,
      hasAllergies,
      allergiesText: hasAllergies ? d.allergiesText : '',
    };

    this.state.set(recomputeEventDrafts({ ...s, personDrafts: { ...s.personDrafts, [personId]: next } }));
  }

  setAllergiesText(personId: string, text: string): void {
    const s = this.state();
    if (!s) return;
    const d = s.personDrafts[personId];
    if (!d) return;

    this.state.set(recomputeEventDrafts({ ...s, personDrafts: { ...s.personDrafts, [personId]: { ...d, allergiesText: text } } }));
  }

  setDinnerAttendance(attendance: EventAttendance): void {
    this.setEvent('dinner', attendance);
  }

  setEveningPartyAttendance(attendance: EventAttendance): void {
    this.setEvent('eveningParty', attendance);
  }

  setDinnerSingleAttendee(personId: string | null): void {
    this.setSingle('dinner', personId);
  }

  setEveningPartySingleAttendee(personId: string | null): void {
    this.setSingle('eveningParty', personId);
  }

  private setEvent(which: 'dinner' | 'eveningParty', attendance: EventAttendance): void {
    const s = this.state();
    if (!s) return;

    const nextPersonDrafts = applyBulkEventSelection(s, which, attendance, null);
    this.state.set(recomputeEventDrafts({ ...s, personDrafts: nextPersonDrafts }));
  }

  private setSingle(which: 'dinner' | 'eveningParty', personId: string | null): void {
    const s = this.state();
    if (!s) return;

    const nextPersonDrafts = applyBulkEventSelection(s, which, 'One', personId);
    this.state.set(recomputeEventDrafts({ ...s, personDrafts: nextPersonDrafts }));
  }

  buildSubmitRequest(): SubmitRequestDto {
    const s = this.state();
    if (!s) throw new Error('Draft not initialized');

    const personResponses: Record<string, PersonResponseDto> = {};
    for (const member of s.members) {
      const d = s.personDrafts[member.personId];
      if (!d || d.attending === null) {
        continue;
      }

      const base: PersonResponseDto = {
        attending: d.attending,
        hasAllergies: null,
        allergiesText: null,
      };

      if (d.attending === 'Yes') {
        base.hasAllergies = d.hasAllergies;
        base.allergiesText = d.hasAllergies ? d.allergiesText.trim() : null;
      }

      personResponses[member.personId] = base;
    }

    const eventResponse = deriveGroupEventResponseFromPersons(s);

    return {
      eventResponse,
      personResponses,
    };
  }
}

type PersistedDraft = {
  groupId: string;
  memberOrder: string[];
  currentPersonId: string;
  personDrafts: Record<string, PersonDraft>;
};

const STORAGE_PREFIX = 'wv_rsvp_draft_v1:';

function storageKey(groupId: string): string {
  return `${STORAGE_PREFIX}${groupId}`;
}

function toPersistedDraft(s: RsvpDraftState): PersistedDraft {
  return {
    groupId: s.groupId,
    memberOrder: s.memberOrder,
    currentPersonId: s.currentPersonId,
    personDrafts: s.personDrafts,
  };
}

function readPersistedDraft(groupId: string): PersistedDraft | null {
  try {
    const raw = sessionStorage.getItem(storageKey(groupId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as PersistedDraft;
    if (!parsed || parsed.groupId !== groupId) return null;
    if (!parsed.personDrafts || typeof parsed.personDrafts !== 'object') return null;
    return parsed;
  } catch {
    return null;
  }
}

function applyRestoredDraft(state: RsvpDraftState, restored: PersistedDraft): RsvpDraftState {
  const memberIds = new Set(state.members.map((m) => m.personId));

  // Only restore if the shape matches the current group.
  for (const id of Object.keys(restored.personDrafts)) {
    if (!memberIds.has(id)) {
      return state;
    }
  }

  const nextPersonDrafts: Record<string, PersonDraft> = { ...state.personDrafts };
  for (const m of state.members) {
    const saved = restored.personDrafts[m.personId];
    if (!saved) continue;
    nextPersonDrafts[m.personId] = {
      attending: saved.attending,
      dinnerAttending: saved.dinnerAttending,
      eveningPartyAttending: saved.eveningPartyAttending,
      hasAllergies: saved.hasAllergies,
      allergiesText: saved.allergiesText ?? '',
    };
  }

  const canUseSavedOrder =
    Array.isArray(restored.memberOrder) &&
    restored.memberOrder.length === state.memberOrder.length &&
    restored.memberOrder.every((id) => memberIds.has(id));

  const nextMemberOrder = canUseSavedOrder ? restored.memberOrder : state.memberOrder;
  const nextCurrentPersonId = memberIds.has(restored.currentPersonId) ? restored.currentPersonId : state.currentPersonId;

  return {
    ...state,
    memberOrder: nextMemberOrder,
    currentPersonId: nextCurrentPersonId,
    personDrafts: nextPersonDrafts,
  };
}

function applyEventResponseToPersonDrafts(
  group: GroupDto,
  drafts: Record<string, PersonDraft>,
  event: GroupEventResponseDto
): Record<string, PersonDraft> {
  const next: Record<string, PersonDraft> = { ...drafts };

  for (const member of group.members) {
    const d = next[member.personId];
    if (!d) continue;

    // Only set event booleans for confirmed "attending yes" people; otherwise keep as-is.
    if (d.attending !== 'Yes') {
      continue;
    }

    if (group.invitedToEvents.includes('Dinner')) {
      d.dinnerAttending = isPersonIncludedInEvent(member.personId, event.dinnerAttendance, event.dinnerSingleAttendeePersonId, event.dinnerAttendeePersonIds);
    }

    if (group.invitedToEvents.includes('EveningParty')) {
      d.eveningPartyAttending = isPersonIncludedInEvent(
        member.personId,
        event.eveningPartyAttendance,
        event.eveningPartySingleAttendeePersonId,
        event.eveningPartyAttendeePersonIds
      );
    }

    next[member.personId] = { ...d };
  }

  return next;
}

function isPersonIncludedInEvent(
  personId: string,
  attendance: EventAttendance | null,
  singleId: string | null,
  ids: string[] | null
): boolean | null {
  if (attendance === null) return null;
  if (attendance === 'None') return false;
  if (attendance === 'All') return true;
  if (attendance === 'One') return singleId ? singleId === personId : false;
  // Some
  return ids ? ids.includes(personId) : false;
}

function applyBulkEventSelection(
  s: RsvpDraftState,
  which: 'dinner' | 'eveningParty',
  attendance: EventAttendance,
  singlePersonId: string | null
): Record<string, PersonDraft> {
  const next: Record<string, PersonDraft> = { ...s.personDrafts };
  const memberIds = s.members.map((m) => m.personId);

  const setFor = (personId: string, value: boolean): void => {
    const d = next[personId];
    if (!d) return;
    if (d.attending !== 'Yes') return;
    next[personId] = { ...d, [which === 'dinner' ? 'dinnerAttending' : 'eveningPartyAttending']: value } as PersonDraft;
  };

  if (attendance === 'Some') {
    // User will adjust via checkboxes.
    return next;
  }

  if (attendance === 'None') {
    for (const id of memberIds) setFor(id, false);
    return next;
  }

  if (attendance === 'All') {
    for (const id of memberIds) setFor(id, true);
    return next;
  }

  // One
  for (const id of memberIds) {
    setFor(id, singlePersonId ? id === singlePersonId : false);
  }

  return next;
}

function recomputeEventDrafts(s: RsvpDraftState): RsvpDraftState {
  const attendingYesIds = s.members
    .map((m) => m.personId)
    .filter((id) => s.personDrafts[id]?.attending === 'Yes');

  const dinner = deriveEvent('Dinner', attendingYesIds, (id) => s.personDrafts[id]?.dinnerAttending === true, s);
  const evening = deriveEvent(
    'EveningParty',
    attendingYesIds,
    (id) => s.personDrafts[id]?.eveningPartyAttending === true,
    s
  );

  return {
    ...s,
    dinner: { attendance: dinner.attendance, singleAttendeePersonId: dinner.singleAttendeePersonId },
    eveningParty: { attendance: evening.attendance, singleAttendeePersonId: evening.singleAttendeePersonId },
  };
}

function deriveGroupEventResponseFromPersons(s: RsvpDraftState): GroupEventResponseDto {
  const attendingYesIds = s.members
    .map((m) => m.personId)
    .filter((id) => s.personDrafts[id]?.attending === 'Yes');

  const dinner = deriveEvent('Dinner', attendingYesIds, (id) => s.personDrafts[id]?.dinnerAttending === true, s);
  const evening = deriveEvent(
    'EveningParty',
    attendingYesIds,
    (id) => s.personDrafts[id]?.eveningPartyAttending === true,
    s
  );

  return {
    dinnerAttendance: dinner.attendance,
    dinnerSingleAttendeePersonId: dinner.singleAttendeePersonId,
    dinnerAttendeePersonIds: dinner.attendeePersonIds,
    eveningPartyAttendance: evening.attendance,
    eveningPartySingleAttendeePersonId: evening.singleAttendeePersonId,
    eveningPartyAttendeePersonIds: evening.attendeePersonIds,
  };
}

function deriveEvent(
  type: 'Dinner' | 'EveningParty',
  attendingYesIds: string[],
  isAttendingEvent: (personId: string) => boolean,
  s: RsvpDraftState
): { attendance: EventAttendance | null; singleAttendeePersonId: string | null; attendeePersonIds: string[] | null } {
  if (!s.invitedToEvents.includes(type)) {
		return { attendance: null, singleAttendeePersonId: null, attendeePersonIds: null };
  }

  if (attendingYesIds.length === 0) {
    return { attendance: 'None', singleAttendeePersonId: null, attendeePersonIds: null };
  }

  const yesIds = attendingYesIds.filter(isAttendingEvent);
  if (yesIds.length === 0) {
    return { attendance: 'None', singleAttendeePersonId: null, attendeePersonIds: null };
  }
  if (yesIds.length === attendingYesIds.length) {
    return { attendance: 'All', singleAttendeePersonId: null, attendeePersonIds: null };
  }
  if (yesIds.length === 1) {
    return { attendance: 'One', singleAttendeePersonId: yesIds[0] ?? null, attendeePersonIds: null };
  }

  // Partial multi-person attendance.
  return { attendance: 'Some', singleAttendeePersonId: null, attendeePersonIds: yesIds };
}

function defaultEventResponse(): GroupEventResponseDto {
  return {
    dinnerAttendance: null,
    eveningPartyAttendance: null,
    dinnerSingleAttendeePersonId: null,
    eveningPartySingleAttendeePersonId: null,
    dinnerAttendeePersonIds: null,
    eveningPartyAttendeePersonIds: null,
  };
}

function reorderMembers(ids: string[], selectedPersonId: string | null): string[] {
  if (!selectedPersonId) return ids;
  const idx = ids.indexOf(selectedPersonId);
  if (idx <= 0) return ids;
  const copy = [...ids];
  copy.splice(idx, 1);
  copy.unshift(selectedPersonId);
  return copy;
}

function snapshotForDirtyCheck(s: RsvpDraftState): string {
  const personIds = [...s.memberOrder].sort();
  const persons = personIds.map((id) => {
    const d = s.personDrafts[id]!;
    return {
      id,
      a: d.attending,
      d: d.dinnerAttending,
      e: d.eveningPartyAttending,
      h: d.hasAllergies,
      t: d.allergiesText,
    };
  });

  const snapshot = {
    persons,
    dinner: s.dinner,
    eveningParty: s.eveningParty,
  };

  return JSON.stringify(snapshot);
}
