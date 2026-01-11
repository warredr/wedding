# Session & Access

The app requires an authenticated session.

## Entry points

- QR entry: the QR code contains a query parameter `k` (a secret GUID). The frontend calls:
  - `GET /api/session/from-qr?k=...`
- Manual entry: user enters a 6-digit code. The frontend calls:
  - `POST /api/session/start` with `{ "code": "662026" }`

## Session mechanism (cost-effective)

- Backend issues an HttpOnly cookie containing a signed, short-lived token (HMAC).
- This avoids server-side session storage.

## Security notes

- Cookie should be:
  - `HttpOnly`
  - `Secure` in production
  - `SameSite=Lax` (or `None` if cross-site is required)
- Frontend must use `withCredentials: true`.

## Expiration

- Session token includes expiry; expired sessions behave as unauthenticated.
