# Plan: Deploy CarShowJudging to Azure

> **Status:** Reference document only. No code changes have been made. Follow these steps when you are ready to deploy — the app continues to run normally on localhost in the meantime.

## Context
The app runs correctly on localhost. The code already branches between SQLite (dev) and SQL Server (prod) in `Program.cs`, EF migrations run on startup via `MigrateAsync()`, and the Blob/SMTP services already read from configuration. What's missing is: three Azure resources, one code change, App Service environment variable overrides, and a GitHub Actions deployment pipeline.

---

## 1. Azure Resources to Create (Azure Portal)

| Resource | SKU | Est. cost |
|---|---|---|
| Azure SQL Server + Database (`CarShowJudging`) | General Purpose Serverless, 0.5 vCores, auto-pause 1hr | ~$0–$5/mo |
| Azure Storage Account (Standard LRS) | Already used by SDK — just needs a real account | ~$0–$1/mo |
| Azure App Service Plan + App Service (.NET 10, Linux) | B1 (~$13/mo) or F1 (free, limited) | $0–$13/mo |

**Required App Service settings (General Settings tab):**
- Web sockets → **On** (Blazor Server requires WebSocket; falls back to slow long-polling without it)
- ARR Affinity → **On** (sticky sessions; required for Blazor Server SignalR circuit)

---

## 2. Code Changes

### `CarShowJudging.Web/Program.cs`
The seed admin password is hardcoded as `"password123"`. Read it from config instead.

Change the call:
```csharp
await SeedAsync(app);
```
to:
```csharp
var adminPassword = app.Configuration["AdminSeed:Password"]
    ?? throw new InvalidOperationException("AdminSeed:Password not configured.");
await SeedAsync(app, adminPassword);
```

Update the method signature:
```csharp
static async Task SeedAsync(WebApplication app, string adminPassword)
```

Replace `"password123"` with `adminPassword` inside the method.

### `CarShowJudging.Web/appsettings.json`
Replace placeholder-style values so a mis-configured deploy fails loudly instead of silently connecting to dev resources:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "PLACEHOLDER_SET_IN_APP_SERVICE"
  },
  "AzureBlobStorage": {
    "ConnectionString": "PLACEHOLDER_SET_IN_APP_SERVICE"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": "587",
    "From": "",
    "User": "",
    "Password": ""
  },
  "AdminSeed": {
    "Password": "PLACEHOLDER_SET_IN_APP_SERVICE"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "PLACEHOLDER_SET_IN_APP_SERVICE"
}
```

---

## 3. App Service Application Settings

In Azure Portal → App Service → Configuration → Application Settings. ASP.NET Core maps double-underscores to nested config keys.

| Name | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=CarShowJudging;User ID=...;Password=...;Encrypt=True;Connection Timeout=30;` |
| `AzureBlobStorage__ConnectionString` | Full Azure Storage connection string from portal |
| `Smtp__From` | Gmail address |
| `Smtp__User` | Gmail address |
| `Smtp__Password` | Gmail App Password (16-char) |
| `AdminSeed__Password` | Secure initial admin password |
| `AllowedHosts` | `your-app.azurewebsites.net` (update if custom domain added) |

`Smtp__Host` and `Smtp__Port` already have correct values in `appsettings.json` and don't need to be repeated here.

---

## 4. GitHub Actions CI/CD Pipeline

**One-time setup:**
1. Azure Portal → App Service → Overview → **Get publish profile** → download the XML file
2. GitHub repo → Settings → Secrets → New secret → name: `AZURE_WEBAPP_PUBLISH_PROFILE` → paste XML content

**Create `.github/workflows/deploy.yml`:**
```yaml
name: Build and Deploy

on:
  push:
    branches: [ main ]
  workflow_dispatch:

env:
  DOTNET_VERSION: '10.0.x'
  AZURE_WEBAPP_NAME: 'your-app-service-name'
  PUBLISH_PATH: './publish'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore
        run: dotnet restore CarShowJudging.sln

      - name: Build
        run: dotnet build CarShowJudging.sln --configuration Release --no-restore

      - name: Publish
        run: dotnet publish CarShowJudging.Web/CarShowJudging.Web.csproj \
          --configuration Release --no-build \
          --output ${{ env.PUBLISH_PATH }}

      - name: Deploy to Azure App Service
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ env.AZURE_WEBAPP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: ${{ env.PUBLISH_PATH }}
```

Push to `main` triggers automatically. `workflow_dispatch` allows manual reruns without a code push.

---

## 5. Verification After First Deploy

1. **Log stream** (Azure Portal → App Service → Log stream) — watch for EF migration output and successful startup
2. **Database** — connect via Azure Data Studio; confirm all tables exist (`Vehicles`, `VehicleNotes`, `AspNetUsers`, etc.)
3. **Login** — navigate to `/login`, sign in as `admin` with the password set in `AdminSeed__Password`
4. **Blob upload** — register a vehicle with a photo; confirm URL is a real `blob.core.windows.net` URL
5. **WebSocket** — browser DevTools → Network → WS tab; confirm a `wss://` connection to `/_blazor` (not long-polling XHR)
6. **Email** — go to `/forgot-password`, request a reset; confirm email arrives

---

## What to Skip (Not Worth the Complexity for This App)

- **Key Vault** — App Service Application Settings are encrypted at rest; sufficient for 5–6 secrets
- **Docker / containers** — App Service has native .NET 10 runtime support; no container needed
- **Application Gateway / CDN** — the default `azurewebsites.net` domain includes TLS for free
- **Deployment slots** — unnecessary for a personal app deploying a few times a year
- **Application Insights** — App Service log streaming is enough; add later if needed
- **New EF migrations before deploy** — existing migrations run cleanly against SQL Server via `MigrateAsync()`; migration snapshot column types are provider-agnostic at runtime

---

## Files Modified

- `CarShowJudging.Web/Program.cs` — read seed password from config
- `CarShowJudging.Web/appsettings.json` — placeholder values for prod secrets
- `.github/workflows/deploy.yml` — new file, CI/CD pipeline
