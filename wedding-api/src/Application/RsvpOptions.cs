namespace WeddingApi.Application;

public sealed class RsvpOptions
{
    public const string SectionName = "Rsvp";

    public required string QrAccessKey { get; init; }
    public required string SixDigitAccessCode { get; init; }
    public int SessionTtlMinutes { get; init; } = 15;
    public required string SessionSigningKey { get; init; }

    public required string AdminKey { get; init; }

    public DateOnly DeadlineDate { get; init; } = new DateOnly(2026, 5, 1);
    public int LockMinutes { get; init; } = 2;

    public int AllergiesTextMaxLength { get; init; } = 200;
}
