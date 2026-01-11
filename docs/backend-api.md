# Backend API

This is the HTTP API contract implemented by the current Azure Functions app.

Notes:
- Azure Functions defaults to the `/api` prefix unless overridden.
- All endpoints return JSON.
- All non-session endpoints require a valid session cookie.
- CORS must allow credentialed requests from:
  - your Azure Static Web Apps origin (production)
  - `http://localhost:4200` (local dev)

## Session

- `POST /api/session/start`
  - Body: `{ "code": "662026" }`
  - Result: `200` + sets HttpOnly session cookie
  - Response: `{ "ok": true, "expiresAtUtc": "..." }`

- `GET /api/session/from-qr?k=...`
  - Result: `200` + sets HttpOnly session cookie
  - Response: `{ "ok": true, "expiresAtUtc": "..." }`

## Config

- `GET /api/config`
  - Response: `{ "deadlineDate": "2026-05-01", "isClosed": false, "sessionExpiresAtUtc": "..." }`

## Search

- `GET /api/search?q=...`
  - Response: list of matches:
    - `personId`, `fullName`, `groupId`, `groupLabelFirstNames`, `groupStatus`

## Group

- `POST /api/groups/{groupId}/claim`
  - Response: `{ "groupStatus": "Locked", "sessionId": "...", "lockExpiresAtUtc": "..." }`

- `GET /api/groups/{groupId}?sessionId=...`
  - Response includes:
    - `groupId`, `groupLabelFirstNames`, `invitedToEvents`, `groupStatus`
    - `members`: list of `personId`, `fullName`, `attending`, `hasAllergies`, `allergiesText`
    - `eventAttendance`: current stored `GroupEventResponse` (if confirmed/available)

- `POST /api/groups/{groupId}/submit?sessionId=...`
  - Body:
    - `eventResponse`: `GroupEventResponse`
    - `personResponses`: map of `personId` -> `PersonResponse`
  - Response: `{ "ok": true }`

## Admin

- `POST /api/manage/groups/{groupId}/reset?adminKey=...`
  - Response: `{ "ok": true }`

## Enums

All enums are serialized as strings (e.g. `"Yes"`, `"No"`, `"All"`, `"One"`, `"None"`).

## Error shape

All error responses use:

`{ "code": "...", "message": "...", "details": ... }`

Common cases:
- `400` validation error: `code=validation_failed`, `details=[{ code, message, field? }, ...]`
- `403` missing session: `code=unauthorized`
- `404` unknown group: `code=not_found`
- `409` conflict: `code=conflict` (details may include `{ reason: "confirmed|locked|lock" }`)

## Error shape (recommended)

- `400` validation error: `{ code: "validation_failed", details: [...] }`
- `401/403` auth error: `{ code: "unauthorized" }`
- `409` conflict error: `{ code: "conflict", reason: "confirmed|locked|etag" }`
