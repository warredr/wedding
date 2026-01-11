using WeddingApi.Domain;

namespace wedding_api.Tests;

public sealed class DomainValidationTests
{
    private static GroupDefinition CreateSampleGroup(IReadOnlyList<EventType> invitedTo)
    {
        return new GroupDefinition(
            GroupId: "g1",
            GroupLabelFirstNames: "Alice & Bob",
            InvitedTo: invitedTo,
            Members: new[]
            {
                new PersonDefinition("p1", "Alice Example"),
                new PersonDefinition("p2", "Bob Example"),
            });
    }

    [Fact]
    public void ValidateSubmission_ReturnsErrors_ForMissingAndExtraPeople()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.None,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: false, AllergiesText: null),
                ["p3"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);

        Assert.Contains(errors, e => e.Code == "missing_person_response");
        Assert.Contains(errors, e => e.Code == "unknown_person_response");
    }

    [Fact]
    public void ValidateSubmission_RejectsAllergies_WhenNotAttending()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.None,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.No, HasAllergies: true, AllergiesText: "Peanuts"),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);

        Assert.Contains(errors, e => e.Code == "allergies_not_allowed_when_not_attending");
        Assert.Contains(errors, e => e.Code == "allergies_text_not_allowed_when_not_attending");
    }

    [Fact]
    public void ValidateSubmission_RequiresHasAllergies_WhenAttendingYes()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.All,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: null, AllergiesText: null),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);
        Assert.Contains(errors, e => e.Code == "missing_has_allergies");
    }

    [Fact]
    public void ValidateSubmission_RequiresAllergiesText_WhenHasAllergiesTrue()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.All,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: true, AllergiesText: "  "),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);
        Assert.Contains(errors, e => e.Code == "missing_allergies_text");
    }

    [Fact]
    public void ValidateSubmission_RejectsAllergiesText_WhenHasAllergiesFalse()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.All,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: false, AllergiesText: "Nope"),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);
        Assert.Contains(errors, e => e.Code == "allergies_text_not_allowed_when_no_allergies");
    }

    [Fact]
    public void ValidateSubmission_RejectsAllergiesText_WhenTooLong()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var tooLong = new string('A', 6);
        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.All,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: true, AllergiesText: tooLong),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 5);
        Assert.Contains(errors, e => e.Code == "allergies_text_too_long");
    }

    [Fact]
    public void ValidateSubmission_RejectsEveningParty_WhenNotInvited()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.None,
                EveningPartyAttendance: EventAttendance.All,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);
        Assert.Contains(errors, e => e.Code == "event_not_invited");
    }

    [Fact]
    public void ValidateSubmission_RequiresAttendanceSelection_WhenInvited()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: null,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);
        Assert.Contains(errors, e => e.Code == "missing_event_attendance");
    }

    [Fact]
    public void ValidateSubmission_RejectsSingleAttendee_WhenAttendanceIsNotOne()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.All,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: "p1",
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: false, AllergiesText: null),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);
        Assert.Contains(errors, e => e.Code == "single_attendee_not_allowed");
    }

    [Fact]
    public void ValidateSubmission_RequiresSingleAttendee_WhenAttendanceIsOne()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.One,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: false, AllergiesText: null),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);
        Assert.Contains(errors, e => e.Code == "missing_single_attendee");
    }

    [Fact]
    public void ValidateSubmission_RejectsSingleAttendee_WhenSelectedPersonNotAttending()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.One,
                EveningPartyAttendance: null,
                DinnerSingleAttendeePersonId: "p2",
                EveningPartySingleAttendeePersonId: null),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
                ["p2"] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null),
            });

        var errors = Validation.ValidateSubmission(group, submission, allergiesTextMaxLength: 200);
        Assert.Contains(errors, e => e.Code == "single_attendee_must_be_attending");
        Assert.Contains(errors, e => e.Code == "no_attending_people");
    }

    [Fact]
    public void ToSheetRows_DerivesEventX_Correctly_ForDinnerAllAndPartyOne()
    {
        var group = CreateSampleGroup(new[] { EventType.Dinner, EventType.EveningParty });

        var submission = new GroupSubmission(
            EventResponse: new GroupEventResponse(
                DinnerAttendance: EventAttendance.All,
                EveningPartyAttendance: EventAttendance.One,
                DinnerSingleAttendeePersonId: null,
                EveningPartySingleAttendeePersonId: "p1"),
            PersonResponses: new Dictionary<string, PersonResponse>
            {
                ["p1"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: false, AllergiesText: null),
                ["p2"] = new PersonResponse(AttendingStatus.Yes, HasAllergies: false, AllergiesText: null),
            });

        var rows = Validation.ToSheetRows(group, submission).ToDictionary(r => r.PersonId, r => r);

        Assert.Equal("X", rows["p1"].Dinner);
        Assert.Equal("X", rows["p2"].Dinner);
        Assert.Equal("X", rows["p1"].EveningParty);
        Assert.Equal(string.Empty, rows["p2"].EveningParty);
    }
}
