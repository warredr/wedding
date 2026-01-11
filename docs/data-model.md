# Data model (Azure Table Storage)

## Tables

- `RsvpGroups` (one entity per group)
- `RsvpResponses` (one entity per person per group)
- `RsvpSheetExports` (outbox for export retries)

## Group entity (RsvpGroups)

PartitionKey: `groupId`

RowKey: `meta`

Fields:

- `Status`: `Open | Locked | Confirmed`
- `LockSessionId`
- `LockExpiresAtUtc`
- `ConfirmedAtUtc`
- Event-level fields:
  - `DinnerAttendance` (`None|All|One`)
  - `EveningPartyAttendance` (`None|All|One`)
  - `DinnerSingleAttendeePersonId` (optional)
  - `EveningPartySingleAttendeePersonId` (optional)

## Response entity (RsvpResponses)

PartitionKey: `groupId`

RowKey: `personId`

Fields:

- `FullName`
- `Attending` (`Yes|No`)
- `HasAllergies`
- `AllergiesText`
- `UpdatedAtUtc`

## Notes

- Concurrency: use ETag updates on group entity.
- Locks expire automatically based on `LockExpiresAtUtc`.
