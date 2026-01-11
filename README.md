# Wedding RSVP

This repo contains the wedding RSVP app:

- Backend: Azure Functions (.NET 8 isolated) + Azure Table Storage
- Frontend: Angular (mobile-first)

## Quick links

- Docs: [docs/README.md](docs/README.md)
- Backend project: [wedding-api/src](wedding-api/src)
- Frontend project: [wedding-web/wedding](wedding-web/wedding)

## Backend local development

The backend expects configuration via `wedding-api/src/local.settings.json` (not committed).

- Setup instructions: [docs/local-development.md](docs/local-development.md)
- API contract: [docs/backend-api.md](docs/backend-api.md)

### Required settings

- `AzureWebJobsStorage`
- `Rsvp__QrAccessKey`
- `Rsvp__SixDigitAccessCode`
- `Rsvp__SessionSigningKey`
- `Rsvp__SessionTtlMinutes`
- `Rsvp__AdminKey`
- `Rsvp__DeadlineDate`

### Run

From repo root:

- `dotnet build wedding.sln`
- `dotnet test wedding.sln`

Then run Functions via VS Code debug or `func start` in `wedding-api/src`.
