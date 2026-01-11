# Local development

## Prereqs

- .NET 8 SDK
- Azure Functions Core Tools v4
- Azurite (for Table Storage)

## Settings

Update `wedding-api/src/local.settings.json` (do not commit secrets):

- Start from the example file: `wedding-api/src/local.settings.example.json`

- `AzureWebJobsStorage`: `UseDevelopmentStorage=true`
- `Rsvp__QrAccessKey`: GUID
- `Rsvp__SixDigitAccessCode`: 6 digits
- `Rsvp__SessionTtlMinutes`: e.g. 15
- `Rsvp__SessionSigningKey`: random string
- `Rsvp__AdminKey`: GUID
- `Rsvp__DeadlineDate`: `2026-05-01`
- `Invites__JsonPath`: path to invites json (optional)

### Export table

The export timer writes per-person rows to Azure Table Storage (default table name `RsvpExportRows`).

- `ExportTable__TableName`: override table name (optional)

### CORS (local)

For browser-based local dev (Angular on `http://localhost:4200`), prefer host-level CORS config:

- `AzureFunctionsJobHost__extensions__http__cors__allowedOrigins`: `http://localhost:4200`
- `AzureFunctionsJobHost__extensions__http__cors__supportCredentials`: `true`

## Run

- From `wedding-api/src`:
  - `func start`

## Integration tests (Azurite)

The integration tests in `wedding-api.Tests` are auto-skipped unless `AzureWebJobsStorage` is set.

One workable Azurite connection string (Table endpoint) is:

`DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;`

Run tests:

- `AzureWebJobsStorage="<the-connection-string>" dotnet test wedding.sln -c Debug`

## Notes

- Frontend calls the backend with cookies; ensure CORS allows credentials.
- In production, configure CORS to allow your Azure Static Web Apps origin.
