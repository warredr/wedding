# Architecture

## Topology

- Frontend: Angular (mobile-first)
- Backend: Azure Functions (.NET 8 isolated)
- Storage: Azure Table Storage
- Export: Azure Table Storage (upsert + delete on reset)

## Core flows

- Session establishment:
  - QR param `k` -> backend issues HttpOnly session cookie
  - OR 6-digit code -> backend issues HttpOnly session cookie
- RSVP:
  - Search by name -> select group -> claim lock -> per-person RSVP -> overview -> submit
- Admin:
  - Reset group -> clears responses + reopens group + updates/deletes export rows

## Key design principles

- Cost-effective: Table Storage, minimal Azure resources, small number of API calls.
- Correctness under concurrency: claim locks with TTL + ETag updates.
- Testability: domain validation and mapping logic are pure functions.
