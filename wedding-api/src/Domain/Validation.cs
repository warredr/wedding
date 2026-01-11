using System.Collections.ObjectModel;

namespace WeddingApi.Domain;

public static class Validation
{
    public sealed record ValidationError(string Code, string Message, string? Field = null);

    public static IReadOnlyList<ValidationError> ValidateSubmission(
        GroupDefinition group,
        GroupSubmission submission,
        int allergiesTextMaxLength)
    {
        var errors = new List<ValidationError>();

        // Ensure response contains all members (and only members)
        var memberIds = group.Members.Select(m => m.PersonId).ToHashSet(StringComparer.Ordinal);
        var submittedIds = submission.PersonResponses.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var missing in memberIds.Except(submittedIds))
        {
            errors.Add(new ValidationError(
                Code: "missing_person_response",
                Message: $"Missing response for person '{missing}'.",
                Field: "personResponses"));
        }

        foreach (var extra in submittedIds.Except(memberIds))
        {
            errors.Add(new ValidationError(
                Code: "unknown_person_response",
                Message: $"Unknown person '{extra}' in responses.",
                Field: "personResponses"));
        }

        // Person-level validation
        foreach (var personId in memberIds.Intersect(submittedIds))
        {
            var response = submission.PersonResponses[personId];
            ValidatePersonResponse(errors, personId, response, allergiesTextMaxLength);
        }

        // Event-level validation
        errors.AddRange(ValidateGroupEventResponse(group, submission));

        return new ReadOnlyCollection<ValidationError>(errors);
    }

    private static void ValidatePersonResponse(
        List<ValidationError> errors,
        string personId,
        PersonResponse response,
        int allergiesTextMaxLength)
    {
        var allergiesText = response.AllergiesText;
        var trimmed = allergiesText?.Trim();

        if (response.Attending == AttendingStatus.No)
        {
            if (response.HasAllergies is not null)
            {
                errors.Add(new ValidationError(
                    Code: "allergies_not_allowed_when_not_attending",
                    Message: $"Person '{personId}' is not attending; allergies must be empty.",
                    Field: $"personResponses.{personId}"));
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                errors.Add(new ValidationError(
                    Code: "allergies_text_not_allowed_when_not_attending",
                    Message: $"Person '{personId}' is not attending; allergies text must be empty.",
                    Field: $"personResponses.{personId}.allergiesText"));
            }

            return;
        }

        // Attending == Yes
        if (response.HasAllergies is null)
        {
            errors.Add(new ValidationError(
                Code: "missing_has_allergies",
                Message: $"Person '{personId}' must answer whether they have allergies.",
                Field: $"personResponses.{personId}.hasAllergies"));
            return;
        }

        if (response.HasAllergies.Value)
        {
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                errors.Add(new ValidationError(
                    Code: "missing_allergies_text",
                    Message: $"Person '{personId}' indicated allergies; text is required.",
                    Field: $"personResponses.{personId}.allergiesText"));
                return;
            }

            if (trimmed!.Length > allergiesTextMaxLength)
            {
                errors.Add(new ValidationError(
                    Code: "allergies_text_too_long",
                    Message: $"Allergies text for person '{personId}' exceeds max length {allergiesTextMaxLength}.",
                    Field: $"personResponses.{personId}.allergiesText"));
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                errors.Add(new ValidationError(
                    Code: "allergies_text_not_allowed_when_no_allergies",
                    Message: $"Person '{personId}' indicated no allergies; text must be empty.",
                    Field: $"personResponses.{personId}.allergiesText"));
            }
        }
    }

    public static IReadOnlyList<ValidationError> ValidateGroupEventResponse(
        GroupDefinition group,
        GroupSubmission submission)
    {
        var errors = new List<ValidationError>();

        var invited = group.InvitedTo.ToHashSet();

        ValidateEvent(
            errors,
            eventType: EventType.Dinner,
            isInvited: invited.Contains(EventType.Dinner),
            attendance: submission.EventResponse.DinnerAttendance,
            singleAttendeePersonId: submission.EventResponse.DinnerSingleAttendeePersonId,
            attendeePersonIds: submission.EventResponse.DinnerAttendeePersonIds,
            personResponses: submission.PersonResponses,
            fieldPrefix: "eventResponse.dinner");

        ValidateEvent(
            errors,
            eventType: EventType.EveningParty,
            isInvited: invited.Contains(EventType.EveningParty),
            attendance: submission.EventResponse.EveningPartyAttendance,
            singleAttendeePersonId: submission.EventResponse.EveningPartySingleAttendeePersonId,
            attendeePersonIds: submission.EventResponse.EveningPartyAttendeePersonIds,
            personResponses: submission.PersonResponses,
            fieldPrefix: "eventResponse.eveningParty");

        return errors;
    }

    private static void ValidateEvent(
        List<ValidationError> errors,
        EventType eventType,
        bool isInvited,
        EventAttendance? attendance,
        string? singleAttendeePersonId,
        IReadOnlyList<string>? attendeePersonIds,
        IReadOnlyDictionary<string, PersonResponse> personResponses,
        string fieldPrefix)
    {
        if (!isInvited)
        {
            if (attendance is not null || singleAttendeePersonId is not null || attendeePersonIds is not null)
            {
                errors.Add(new ValidationError(
                    Code: "event_not_invited",
                    Message: $"Group is not invited to {eventType}.",
                    Field: fieldPrefix));
            }

            return;
        }

        if (attendance is null)
        {
            errors.Add(new ValidationError(
                Code: "missing_event_attendance",
                Message: $"Missing attendance selection for {eventType}.",
                Field: fieldPrefix + ".attendance"));
            return;
        }

        if (attendance == EventAttendance.None || attendance == EventAttendance.All)
        {
            if (singleAttendeePersonId is not null)
            {
                errors.Add(new ValidationError(
                    Code: "single_attendee_not_allowed",
                    Message: $"Single attendee not allowed unless attendance is One for {eventType}.",
                    Field: fieldPrefix + ".singleAttendeePersonId"));
            }

            if (attendeePersonIds is not null)
            {
                errors.Add(new ValidationError(
                    Code: "attendees_not_allowed",
                    Message: $"Attendee list not allowed unless attendance is Some for {eventType}.",
                    Field: fieldPrefix + ".attendeePersonIds"));
            }

            return;
        }

        if (attendance == EventAttendance.One)
        {
            if (attendeePersonIds is not null)
            {
                errors.Add(new ValidationError(
                    Code: "attendees_not_allowed",
                    Message: $"Attendee list not allowed unless attendance is Some for {eventType}.",
                    Field: fieldPrefix + ".attendeePersonIds"));
            }

            // attendance == One
        if (string.IsNullOrWhiteSpace(singleAttendeePersonId))
        {
            errors.Add(new ValidationError(
                Code: "missing_single_attendee",
                Message: $"Must select single attendee for {eventType}.",
                Field: fieldPrefix + ".singleAttendeePersonId"));
            return;
        }

        if (!personResponses.TryGetValue(singleAttendeePersonId, out var singleAttendeeResponse))
        {
            errors.Add(new ValidationError(
                Code: "invalid_single_attendee",
                Message: $"Selected single attendee '{singleAttendeePersonId}' is not a group member.",
                Field: fieldPrefix + ".singleAttendeePersonId"));
            return;
        }

        if (singleAttendeeResponse.Attending != AttendingStatus.Yes)
        {
            errors.Add(new ValidationError(
                Code: "single_attendee_must_be_attending",
                Message: $"Selected single attendee '{singleAttendeePersonId}' must have Attending=Yes.",
                Field: fieldPrefix + ".singleAttendeePersonId"));
        }

        if (!personResponses.Values.Any(r => r.Attending == AttendingStatus.Yes))
        {
            errors.Add(new ValidationError(
                Code: "no_attending_people",
                Message: $"At least one person must be attending to select One for {eventType}.",
                Field: fieldPrefix + ".attendance"));
        }

            return;
        }

        if (attendance == EventAttendance.Some)
        {
            if (singleAttendeePersonId is not null)
            {
                errors.Add(new ValidationError(
                    Code: "single_attendee_not_allowed",
                    Message: $"Single attendee not allowed unless attendance is One for {eventType}.",
                    Field: fieldPrefix + ".singleAttendeePersonId"));
            }

            if (attendeePersonIds is null || attendeePersonIds.Count == 0)
            {
                errors.Add(new ValidationError(
                    Code: "missing_attendees",
                    Message: $"Must select at least one attendee for {eventType} when attendance is Some.",
                    Field: fieldPrefix + ".attendeePersonIds"));
                return;
            }

            if (!personResponses.Values.Any(r => r.Attending == AttendingStatus.Yes))
            {
                errors.Add(new ValidationError(
                    Code: "no_attending_people",
                    Message: $"At least one person must be attending to select Some for {eventType}.",
                    Field: fieldPrefix + ".attendance"));
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in attendeePersonIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!seen.Add(id))
                {
                    continue;
                }

                if (!personResponses.TryGetValue(id, out var attendeeResponse))
                {
                    errors.Add(new ValidationError(
                        Code: "invalid_attendee",
                        Message: $"Selected attendee '{id}' is not a group member.",
                        Field: fieldPrefix + ".attendeePersonIds"));
                    continue;
                }

                if (attendeeResponse.Attending != AttendingStatus.Yes)
                {
                    errors.Add(new ValidationError(
                        Code: "attendee_must_be_attending",
                        Message: $"Selected attendee '{id}' must have Attending=Yes.",
                        Field: fieldPrefix + ".attendeePersonIds"));
                }
            }

            return;
        }
    }

    public static IReadOnlyList<SheetRow> ToSheetRows(
        GroupDefinition group,
        GroupSubmission submission)
    {
        var invited = group.InvitedTo.ToHashSet();

        var rows = new List<SheetRow>(group.Members.Count);
        foreach (var member in group.Members)
        {
            submission.PersonResponses.TryGetValue(member.PersonId, out var response);
            response ??= new PersonResponse(AttendingStatus.No, null, null);

            var dinnerX = invited.Contains(EventType.Dinner)
                ? ComputeEventX(
                    response,
                    submission.EventResponse.DinnerAttendance,
                    submission.EventResponse.DinnerSingleAttendeePersonId,
                    submission.EventResponse.DinnerAttendeePersonIds,
                    member.PersonId)
                : string.Empty;

            var partyX = invited.Contains(EventType.EveningParty)
                ? ComputeEventX(
                    response,
                    submission.EventResponse.EveningPartyAttendance,
                    submission.EventResponse.EveningPartySingleAttendeePersonId,
                    submission.EventResponse.EveningPartyAttendeePersonIds,
                    member.PersonId)
                : string.Empty;

            var allergies = (response.Attending == AttendingStatus.Yes && response.HasAllergies == true)
                ? (response.AllergiesText ?? string.Empty)
                : string.Empty;

            rows.Add(new SheetRow(
                FullName: member.FullName,
                Dinner: dinnerX,
                EveningParty: partyX,
                Allergies: allergies,
                GroupId: group.GroupId,
                PersonId: member.PersonId));
        }

        return new ReadOnlyCollection<SheetRow>(rows);
    }

    private static string ComputeEventX(
        PersonResponse response,
        EventAttendance? attendance,
        string? singleAttendeePersonId,
        IReadOnlyList<string>? attendeePersonIds,
        string personId)
    {
        if (response.Attending != AttendingStatus.Yes)
        {
            return string.Empty;
        }

        return attendance switch
        {
            EventAttendance.None => string.Empty,
            EventAttendance.All => "X",
            EventAttendance.One => string.Equals(singleAttendeePersonId, personId, StringComparison.Ordinal)
                ? "X"
                : string.Empty,
            EventAttendance.Some => attendeePersonIds is not null && attendeePersonIds.Contains(personId, StringComparer.Ordinal)
                ? "X"
                : string.Empty,
            _ => string.Empty,
        };
    }
}
