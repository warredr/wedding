export type ConfirmationStatus = 'Open' | 'Locked' | 'Confirmed';

export type EventType = 'Dinner' | 'EveningParty';

export type EventAttendance = 'None' | 'All' | 'One' | 'Some';

export type AttendingStatus = 'Yes' | 'No';

export interface ConfigDto {
  deadlineDate: string;
  isClosed: boolean;
  sessionExpiresAtUtc: string;
}

export interface SearchResultDto {
  personId: string;
  fullName: string;
  groupId: string;
  groupLabelFirstNames: string;
  groupStatus: ConfirmationStatus;
}

export interface GroupMemberDto {
  personId: string;
  fullName: string;
  attending: AttendingStatus | null;
  hasAllergies: boolean | null;
  allergiesText: string | null;
}

export interface GroupEventResponseDto {
  dinnerAttendance: EventAttendance | null;
  eveningPartyAttendance: EventAttendance | null;
  dinnerSingleAttendeePersonId: string | null;
  eveningPartySingleAttendeePersonId: string | null;
  dinnerAttendeePersonIds: string[] | null;
  eveningPartyAttendeePersonIds: string[] | null;
}

export interface GroupDto {
  groupId: string;
  groupLabelFirstNames: string;
  invitedToEvents: EventType[];
  groupStatus: ConfirmationStatus;
  members: GroupMemberDto[];
  eventAttendance: GroupEventResponseDto | null;
}

export interface ClaimGroupResponseDto {
  groupStatus: ConfirmationStatus;
  sessionId: string;
  lockExpiresAtUtc: string;
}

export interface PersonResponseDto {
  attending: AttendingStatus;
  hasAllergies: boolean | null;
  allergiesText: string | null;
}

export interface SubmitRequestDto {
  eventResponse: GroupEventResponseDto;
  personResponses: Record<string, PersonResponseDto>;
}
