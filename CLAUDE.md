# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common commands

```powershell
# Run the app (Development, SQLite, http://localhost:5000)
dotnet run --project CarShowJudging.Web

# Build the solution
dotnet build CarShowJudging.sln

# Add an EF Core migration (always target the Infrastructure project)
dotnet ef migrations add <MigrationName> --project CarShowJudging.Infrastructure --startup-project CarShowJudging.Web

# Apply migrations manually (migrations also run automatically on startup)
dotnet ef database update --project CarShowJudging.Infrastructure --startup-project CarShowJudging.Web
```

There are no automated tests in this project.

## Architecture

Three-project layered architecture targeting **net10.0**:

- **CarShowJudging.Core** — domain models (`Models/`), service interfaces (`Interfaces/`), and DTOs (`DTOs/`). No dependencies on other projects.
- **CarShowJudging.Infrastructure** — EF Core `AppDbContext`, concrete service implementations, EF migrations, and `BlobStorageService`. References Core only.
- **CarShowJudging.Web** — Blazor Server app (Razor Pages shell + Razor components). References both Core and Infrastructure.

### Data layer

`AppDbContext` extends `IdentityDbContext<ApplicationUser>`. Key schema decisions:
- `Vehicle.RegisteredById` → the logged-in user who submitted the form (required, restrict delete)
- `Vehicle.OwnerId` → nullable; intended for future owner account linking but **never populated** by any current code path — `GetByOwnerAsync` queries `RegisteredById`, not `OwnerId`
- `Score` has a unique index on `(VehicleId, JudgeId)` — one score per judge per vehicle
- `Score.Overall` and `JudgeScoreDto.Overall` are computed properties ignored by EF
- `Vehicle` ↔ `VehicleClass` is many-to-many via the `VehicleVehicleClass` join table (EF implicit)

SQLite is used in all environments (`carshow.db` in the Web project directory by default, gitignored). The connection string in `appsettings.json` points there by default; override via `appsettings.Development.json` or the `ConnectionStrings__DefaultConnection` environment variable to point elsewhere (e.g. a mounted volume in a container deployment).

### Services

All services are registered as **Scoped**. `BlobServiceClient` is **Singleton**. The three domain services follow the same pattern: an interface in Core, a concrete implementation in Infrastructure injecting `AppDbContext` directly (no repository layer).

- `VehicleService.RegisterAsync` opens a DB transaction, auto-assigns the next `EntryNumber` (max+1), optionally uploads a photo to Azure Blob Storage, then saves.
- `ScoreService.SubmitAsync` is an **upsert** — if a score already exists for the same `(VehicleId, JudgeId)` pair it is updated in place; judges can re-score.
- `ScoreService.GetScoringRowsAsync` loads all vehicles and their scores into memory before computing per-criterion averages — there is no server-side aggregation.
- `BlobStorageService` uploads to the `vehicle-photos` container with **public blob access** — photo URLs are publicly readable without authentication.
- Photos are stored with a `Guid`-based blob name; the content type is hardcoded to `image/jpeg` regardless of the actual file extension.

### Authentication & roles

ASP.NET Core Identity with three roles: **Admin**, **Judge**, **User**. `ApplicationUser` extends `IdentityUser` with a single extra property: `DisplayName`. Password complexity is intentionally disabled (min length 6, no complexity rules). A default `admin / password123` account is seeded on startup if it doesn't exist.

Cookie paths: login → `/login`, logout → `/logout`, access denied → `/access-denied`.

### Blazor UI structure

Pages live under `CarShowJudging.Web/Pages/` organized by role:
- `Vehicles/` — participant self-service (register vehicle, view own entries)
- `Judge/` — judge-facing entry list, scoring, and proxy vehicle registration
- `Admin/` — full entry/score/class/user management
- `Auth/` — login/register/logout (Razor Pages, not Blazor components)
- `Account/` — profile page

Shared components in `Shared/`: `NavMenu`, `MainLayout`, `ConfirmDialog` (reusable modal), `ScoreConfirmationModal`, `CriteriaInput`, `RedirectToLogin`.

`ConfirmDialog` uses an **imperative** pattern — call `_dialog.Show()` from a `@ref`, not a state flag. Pages that use it follow: declare `private ConfirmDialog _dialog = default!;`, wire `@ref="_dialog"`, then call `_dialog.Show()` in a handler.

`_Layout.cshtml` is the HTML shell. It includes an inline `<script>` in `<head>` that reads `localStorage` to apply the saved dark mode theme (`data-bs-theme` attribute on `<html>`) before first paint, avoiding a flash. Bootstrap 5.3's native dark mode is used — no custom CSS framework needed beyond overrides for the handful of custom classes in `wwwroot/css/app.css`.

### Scoring model

Five criteria per judge score: **Condition**, **PaintAndBody**, **Interior**, **ShowAppeal**, **SuperCoolnessFactor** — all equally weighted. `Overall` = sum / 5. Scores are validated as 1–10 in component code (`ValidateScores()` in `Judge/Score.razor`) — there is no DB-level constraint enforcing this range.

### Azure Blob Storage

Configured via `AzureBlobStorage:ConnectionString` in app config. In development, `UseDevelopmentStorage=true` (Azurite). The container name is hardcoded to `vehicle-photos` in `BlobStorageService`.
