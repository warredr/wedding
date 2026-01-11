# Export table (Azure Table Storage)

On successful submit, the backend writes one row per person to Azure Table Storage.

## Columns

| Full name | Dinner | Evening Party | Allergies |

- Dinner/Evening Party: `"X"` when attending that event, else empty.
- Allergies: exact text input if allergies yes, else empty.

## Keys

Rows are stored with:

- `PartitionKey = groupId`
- `RowKey = personId`

## Upsert behavior

- Upsert replaces the row for a given `(groupId, personId)`.
- On admin reset, all rows in the group partition are deleted.

## Reliability

A minimal outbox table (`RsvpSheetExports`) is used so export retries are idempotent and do not block RSVP submission.

## Configuration (Function App settings)

Bind via the `ExportTable` section:

- `ExportTable__TableName` (default `RsvpExportRows`)

## Outbox operations

The outbox row per group (`PartitionKey=groupId`, `RowKey=latest`) tracks:

- `Operation`: `Upsert` (on submit) or `Delete` (on admin reset)
- `Status`: `Pending` or `Succeeded`
- `AttemptCount`, `LastError`

## Retry mechanism

The timer-trigger function `sheet_export_timer` runs every 5 minutes and processes up to 25 pending outbox items.

- For `Upsert`: reads group + stored responses, derives per-person rows, and upserts them into the export table.
- For `Delete`: deletes all rows in the group partition.
