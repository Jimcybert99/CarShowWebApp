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
- **CarShowJudging.Infrastructure** — EF Core `AppDbContext`, concrete service implementations, and EF migrations. References Core only.
- **CarShowJudging.Web** — Blazor Web App (unified `AddRazorComponents()`/`MapRazorComponents<App>()` hosting model, interactive server render mode) for the main app, plus a handful of classic Razor Pages for auth (`Pages/Auth/*`). References both Core and Infrastructure.

### Data layer

`AppDbContext` extends `IdentityDbContext<ApplicationUser>`. Key schema decisions:
- `Vehicle.RegisteredById` → the logged-in user who submitted the form; nullable with `OnDelete(SetNull)` — deleting a user does not delete their vehicles (see `AllowUserDeletionKeepEntries` migration)
- `Vehicle.OwnerId` → nullable; intended for future owner account linking but **never populated** by any current code path — `GetByOwnerAsync` queries `RegisteredById`, not `OwnerId`
- `Vehicle.EntryNumber` has a DB-level unique index (`AddVehicleEntryNumberUniqueIndex` migration) — `VehicleService.RegisterAsync` retries the max+1 read/write a few times against `DbUpdateException` when two concurrent registrations race for the same number
- `Score` has a unique index on `(VehicleId, JudgeId)` — one score per judge per vehicle
- `Score.Overall` and `JudgeScoreDto.Overall` are computed properties ignored by EF, weighted per `CarShowJudging.Core/Constants/ScoreWeights.cs` (see Scoring model below)
- `Vehicle` ↔ `VehicleClass` is many-to-many via the `VehicleVehicleClass` join table (EF implicit)

SQLite is used in **all environments** — there is no SQL Server code path anymore (removed for this app's actual scale). `carshow.db` in the Web project directory by default (gitignored); override via `appsettings.Development.json` or the `ConnectionStrings__DefaultConnection` environment variable (e.g. a mounted volume in a container deployment). A `SqlitePragmaInterceptor` sets `journal_mode=WAL` and a 5s `busy_timeout` on every connection so concurrent judges scoring at once don't hit immediate "database is locked" failures.

### Services

All services are registered as **Scoped**, except `IBlobStorageService` which is **Singleton**. The domain services follow the same pattern: an interface in Core, a concrete implementation in Infrastructure injecting `AppDbContext` directly (no repository layer).

- `VehicleService.RegisterAsync` auto-assigns the next `EntryNumber` (max+1, retried on conflict — see Data layer above), optionally uploads a photo, then saves. A single `SaveChangesAsync` call is relied on for atomicity; an earlier explicit `BeginTransactionAsync` was removed after it caused "connection is already in a transaction" failures under load.
- `VehicleService.UpdateAsync` (new) lets an admin edit an existing vehicle's details/classes/photo via `VehicleUpdateDto`, deleting the old photo file if a new one is uploaded.
- `ScoreService.SubmitAsync` is an **upsert** — if a score already exists for the same `(VehicleId, JudgeId)` pair it is updated in place; judges can re-score.
- `ScoreService.GetScoringRowsAsync` loads all vehicles and their scores into memory before computing per-criterion averages — there is no server-side aggregation.
- `IBlobStorageService`, despite the name (kept for interface stability), is implemented by `LocalFileStorageService` — **Azure Blob Storage was removed entirely**. Photos are written to `wwwroot/uploads/vehicles/` with a `Guid`-based file name and served back via `UseStaticFiles()` (not `MapStaticAssets()`, which only serves build-time-known files).
- `ScoreSpreadsheetExporter` (new) generates an Excel export of the scoring leaderboard, downloaded client-side via `wwwroot/js/downloadFile.js`.

### Authentication & roles

ASP.NET Core Identity with four roles: **Admin**, **Judge**, **User**, **SuperUser** (new — used for admin vehicle editing and elevated management). `ApplicationUser` extends `IdentityUser` with no extra properties — there is no separate display name; `UserName` is shown everywhere a person's name is needed (`RemoveDisplayName` migration removed the old `DisplayName` column). Password complexity is intentionally disabled (min length 6, no complexity rules). A default `admin` account is seeded on startup if it doesn't exist, using `Seed:AdminPassword` (`SEED__ADMINPASSWORD` env var) — the app **refuses to start outside Development** unless this has been set to something other than the `password123` default.

Cookie paths: login → `/login`, logout → `/logout`, access denied → `/access-denied`.

**Permission model, deliberately asymmetric:**
- Only **Admin** and **SuperUser** can delete a vehicle entry (`Admin/Entries.razor`) or delete a submitted score (`Admin/Scores.razor`) — a **Judge** can view both pages (they need the leaderboard and entry list) but has no delete capability on either; `VehicleService.DeleteAsync` and `ScoreService.DeleteScoresAsync` both enforce this server-side (role check inside the service, not just hidden UI), since Blazor Server circuit events aren't otherwise gated like an HTTP endpoint would be.
- **SuperUser can promote other users to SuperUser** (this is intentional — `Admin/Users.razor`'s role `<select>` always offers the SuperUser option to any viewer who can reach the page). What SuperUser *cannot* do: touch the `Admin` role itself, or edit a row that is already SuperUser (that stays "Locked" unless the viewer is Admin) — so a SuperUser can create more SuperUsers but can't demote one or touch Admin.
- CSV bulk import (`Vehicles/Register.razor`) is hidden from plain `User`-role participants and capped at `MaxCsvRows` (200) per import — it's a staff tool (Admin/SuperUser/Judge), not part of public self-registration.
- Uploaded vehicle photos are validated server-side against an extension + magic-byte allow-list (JPG/PNG/WebP only, see `VehicleService.ValidateAndBufferPhotoAsync`) before being written to `wwwroot/uploads/vehicles/` — since that folder is served back same-origin via `UseStaticFiles()`, an unvalidated upload (e.g. an `.html` file) would be stored XSS.
- `/forgot-password` never renders the password-reset token to the HTTP response — it only emails it (via `IEmailService`), and if a user has no deliverable email on file (e.g. the seeded `admin` account, `@carshow.local`), the link is logged server-side only for an admin to relay manually. Don't reintroduce an on-screen "here's your reset link" fallback; that was a live account-takeover bug.

### Blazor UI structure

Pages live under `CarShowJudging.Web/Pages/` organized by role:
- `Vehicles/` — participant self-service (register vehicle, view own entries)
- `Judge/` — judge-facing entry list, scoring, and proxy vehicle registration
- `Admin/` — full entry/score/class/user management
- `Auth/` — login/register/logout (Razor Pages, not Blazor components)
- `Account/` — profile page

Shared components in `Shared/`: `NavMenu`, `MainLayout`, `ConfirmDialog` (reusable modal), `ScoreConfirmationModal`, `CriteriaInput`, `RedirectToLogin`, `NoteModal` (new).

`ConfirmDialog` uses an **imperative** pattern — call `_dialog.Show()` from a `@ref`, not a state flag. Pages that use it follow: declare `private ConfirmDialog _dialog = default!;`, wire `@ref="_dialog"`, then call `_dialog.Show()` in a handler.

`App.razor` is the HTML shell for the interactive Blazor app (replaced `_Host.cshtml`, which was deleted — the old `AddServerSideBlazor()`/`MapBlazorHub()` model has a confirmed .NET 10 regression, [dotnet/aspnetcore#66175](https://github.com/dotnet/aspnetcore/issues/66175)). `Routes.razor` holds the router (what used to live in `App.razor` under the old model). `App.razor` includes an inline `<script>` in `<head>` that reads `localStorage` to apply the saved dark mode theme (`data-bs-theme` attribute on `<html>`) before first paint, avoiding a flash. Bootstrap 5.3's native dark mode is used — no custom CSS framework needed beyond overrides for the handful of custom classes in `wwwroot/css/app.css`.

`Pages/Auth/*` (Razor Pages, not Blazor components) still use the older `_Layout.cshtml` shell — deliberately left on the old model since they don't depend on the Blazor circuit. If they ever need interactive components embedded, note `_Layout.cshtml` still references `_framework/blazor.server.js`, which would need the same fix `App.razor` got.

### Scoring model

Five criteria per judge score, weighted (see `CarShowJudging.Core/Constants/ScoreWeights.cs`): **Exterior** (30%), **Interior** (20%), **EngineBay** (15%), **Craftsmanship** (20%), **Presentation** (15%). `Overall` is the weighted sum. Scores are validated as 1–10 in component code (`ValidateScores()` in `Judge/Score.razor`) — there is no DB-level constraint enforcing this range.

### Photo storage

Vehicle photos are stored on **local disk** (`wwwroot/uploads/vehicles/`), not a cloud blob store — Azure Blob Storage support was removed entirely for this app's scale. See `IBlobStorageService`/`LocalFileStorageService` under Services above. This means the SQLite file and the uploads folder together are the *entire* persistent state of the app — both need to be backed up and both need to live on a durable volume in any container deployment.
