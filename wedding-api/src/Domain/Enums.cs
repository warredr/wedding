namespace WeddingApi.Domain;

public enum AttendingStatus
{
    No = 0,
    Yes = 1,
}

public enum ConfirmationStatus
{
    Open = 0,
    Locked = 1,
    Confirmed = 2,
}

public enum EventType
{
    Dinner = 0,
    EveningParty = 1,
}

public enum EventAttendance
{
    None = 0,
    All = 1,
    One = 2,
    Some = 3,
}
