using Azure;
using Azure.Data.Tables;
using WeddingApi.Domain;

namespace WeddingApi.Storage;

public sealed class RsvpGroupEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // groupId
    public string RowKey { get; set; } = "meta";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Status { get; set; } = ConfirmationStatus.Open.ToString();
    public string? LockSessionId { get; set; }
    public DateTimeOffset? LockExpiresAtUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    public string? DinnerAttendance { get; set; }
    public string? EveningPartyAttendance { get; set; }
    public string? DinnerSingleAttendeePersonId { get; set; }
    public string? EveningPartySingleAttendeePersonId { get; set; }

    public string? DinnerAttendeePersonIds { get; set; }
    public string? EveningPartyAttendeePersonIds { get; set; }
}

public sealed class RsvpPersonEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // groupId
    public string RowKey { get; set; } = default!; // personId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string FullName { get; set; } = default!;
    public string Attending { get; set; } = AttendingStatus.No.ToString();
    public bool HasAllergies { get; set; }
    public string AllergiesText { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RsvpDeviceClaimEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // deviceId
    public string RowKey { get; set; } = "claim";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string GroupId { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
