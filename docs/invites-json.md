# Invites JSON schema

The invites JSON file defines groups, their members, and which events the group is invited to.

## Events

Only these event values are allowed:

- `Dinner`
- `EveningParty`

## Schema (v1)

```json
{
  "schemaVersion": 1,
  "groups": [
    {
      "groupId": "g001",
      "groupLabelFirstNames": "Jan, Piet",
      "invitedTo": ["Dinner", "EveningParty"],
      "members": [
        { "personId": "p001", "fullName": "Jan Janssen" },
        { "personId": "p002", "fullName": "Piet Pieters" }
      ]
    }
  ]
}
```

## Constraints

- `groupId` is unique.
- `personId` is unique within a group.
- `fullName` is the display name used for search.
- The frontend uses `groupLabelFirstNames` to disambiguate identical names across groups.
