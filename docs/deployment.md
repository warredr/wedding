# Deployment

This repo is split into:

- Frontend: Angular app in `wedding-web`
- Backend: Azure Functions (.NET 8 isolated) in `wedding-api/src`

## Frontend (manual deploy) — Azure Static Web Apps

This deployment is intentionally manual (no CI) using the Static Web Apps CLI.

### 1) Create the Static Web App

In Azure Portal:

- Create **Static Web App**
- Deployment source: **Other** (manual)
- Framework preset: **Angular** (or Custom)

After creation, copy the **Deployment token** (Manage deployment token).

### 2) Build the Angular app

From repo root:

- `cd wedding-web`
- `npm ci`
- `npm run build`

Build output is written to `wedding-web/dist/wedding`.

### 3) Deploy with SWA CLI

Install the CLI:

- `npm i -g @azure/static-web-apps-cli`

Deploy:

- `swa deploy wedding-web/dist/wedding --deployment-token "$SWA_DEPLOYMENT_TOKEN"`

Notes:
- The token is sensitive; do not commit it.
- Repeat the deploy command whenever you want to publish a new frontend build.

## Backend (GitHub deploy) — Azure Functions

Backend deployment uses GitHub Actions to build + deploy `wedding-api` on pushes.

### 1) Create the Function App

In Azure Portal:

- Create **Function App**
- Runtime stack: **.NET (isolated)**
- Version: **.NET 8**

### 2) Configure GitHub secrets

This repo includes a workflow: `.github/workflows/azure-functions-deploy.yml`.

Add these **GitHub repository secrets**:

- `AZURE_FUNCTIONAPP_NAME`: your Function App name (e.g. `wedding-api-prod`)
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`: the Function App publish profile XML

Get the publish profile from Azure Portal:

- Function App → Overview → **Get publish profile**

### 3) Configure Function App settings

Set these app settings in Azure (Function App → Configuration):

- `AzureWebJobsStorage`
- `Rsvp__QrAccessKey`
- `Rsvp__SixDigitAccessCode`
- `Rsvp__SessionSigningKey`
- `Rsvp__SessionTtlMinutes`
- `Rsvp__AdminKey`
- `Rsvp__DeadlineDate`
- `Invites__JsonPath` (or package the invites file)
- `ExportTable__TableName` (optional)

Also configure CORS to allow the Static Web App origin and credentials.

### 4) Deploy

Push to `main`/`master` and GitHub Actions will:

- Build the solution
- Publish the Functions app
- Deploy it to the configured Function App
