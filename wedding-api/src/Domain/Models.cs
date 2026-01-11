namespace WeddingApi.Domain;

public sealed record PersonDefinition(
    string PersonId,
    string FullName
);

public sealed record GroupDefinition(
    string GroupId,
    string GroupLabelFirstNames,
    IReadOnlyList<EventType> InvitedTo,
    IReadOnlyList<PersonDefinition> Members
);

public sealed record PersonResponse(
    AttendingStatus Attending,
    bool? HasAllergies,
    string? AllergiesText
);

public sealed record GroupEventResponse(
    EventAttendance? DinnerAttendance,
    EventAttendance? EveningPartyAttendance,
    string? DinnerSingleAttendeePersonId,
    string? EveningPartySingleAttendeePersonId,
    IReadOnlyList<string>? DinnerAttendeePersonIds = null,
    IReadOnlyList<string>? EveningPartyAttendeePersonIds = null
);

public sealed record GroupSubmission(
    GroupEventResponse EventResponse,
    IReadOnlyDictionary<string, PersonResponse> PersonResponses
);

public sealed record SheetRow(
    string FullName,
    string Dinner,
    string EveningParty,
    string Allergies,
    string GroupId,
    string PersonId
);
