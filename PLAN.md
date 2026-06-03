# Car Show Judging Application — Implementation Plan

## Context

A new web application is needed to manage entries and judging for a car show event. The system must support three user roles with distinct permissions, allow vehicle registration with optional photos and class assignment, enable judges to score entries on five criteria, and give the admin a scoring overview with filtering and sorting. Built from scratch with .NET 10, Blazor Server, SQL Server, and Azure Blob Storage.

---

## Solution Overview

**Stack:**
- .NET 10 Blazor Server (`CarShowJudging.Web`)
- ASP.NET Core Identity (cookie auth, role-based)
- Entity Framework Core 10 + SQL Server / LocalDB (dev)
- Azure Blob Storage (vehicle photos)
- Bootstrap 5 (UI, included via CDN or libman)

**Solution structure:**
```
CarShowJudging.sln
├── CarShowJudging.Web/           ← Blazor Server app (entry point)
├── CarShowJudging.Core/          ← Domain models, interfaces, enums
└── CarShowJudging.Infrastructure/ ← EF Core, Blob storage, repositories
```

---

## Data Model

### Identity / Roles
- Extend `IdentityUser` with `DisplayName` (string)
- Three roles seeded at startup: `Admin`, `Judge`, `User`
- Admin seeded with credentials `admin` / `password123`

### `VehicleClass`
| Column | Type |
|---|---|
| Id | int PK |
| Name | string (required) |

### `Vehicle` (Entry)
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| EntryNumber | int | Sequential, assigned at insert (MAX+1) |
| OwnerName | string | Entered at registration (free text) |
| RegisteredById | FK IdentityUser | Who submitted the registration |
| OwnerId | FK IdentityUser (nullable) | The actual vehicle owner's account, if one exists |
| Make | string | |
| Model | string | |
| Year | int | |
| PhotoUrl | string? | Azure Blob URL |
| CreatedAt | DateTimeOffset | |
| Classes | ICollection\<VehicleClass\> | Many-to-many; EF Core generates `VehicleVehicleClass` join table automatically |

A vehicle can belong to zero or more classes (e.g., a 1987 Mustang could be in both "80's Muscle" and "Ford"). The class selector on the registration form uses a multi-select control.

### `Score`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| VehicleId | FK Vehicle | |
| JudgeId | FK IdentityUser | |
| Condition | int (1–10) | |
| PaintAndBody | int (1–10) | |
| Interior | int (1–10) | |
| ShowAppeal | int (1–10) | |
| SuperCoolnessFactor | int (1–10) | |
| ScoredAt | DateTimeOffset | |
| Unique index on (VehicleId, JudgeId) | | Enforces one score per judge per vehicle |

**Computed overall score** = average of the five criteria (Condition, PaintAndBody, Interior, ShowAppeal, SuperCoolnessFactor) for each judge score record, then averaged across all judge scores for a vehicle. Calculated in the service layer, not stored.

---

## Project Breakdown

### `CarShowJudging.Core`
- Models: `Vehicle`, `VehicleClass`, `Score`, `ApplicationUser`
- Interfaces: `IVehicleService`, `IScoreService`, `IClassService`, `IBlobStorageService`
  - `IVehicleService.DeleteAsync(vehicleId, requestingUserId, requestingUserRole)` — enforces that a normal user can only delete their own entry; Admin and Judge can delete any
- Enums: `UserRole` (Admin, Judge, User)
- DTOs: `VehicleRegistrationDto`, `ScoreSubmitDto`, `ScoringRowDto` (for the scoring grid)

### `CarShowJudging.Infrastructure`
- `AppDbContext` : EF Core, includes Identity tables + app tables
- Repositories / service implementations
- `BlobStorageService` using `Azure.Storage.Blobs`
- EF migrations

### `CarShowJudging.Web`
- Program.cs: DI wiring, Identity, EF, Blob, role seeding
- Blazor pages and components (see below)

---

## Pages & Components

### Auth (shared)
| Route | Description |
|---|---|
| `/login` | Username/password login form |
| `/register` | Creates account with `User` role |
| `/account/profile` | View/edit display name |

### Normal User
| Route | Description |
|---|---|
| `/vehicles/register` | Register new vehicle (name, make, model, year, optional class, optional photo upload) |
| `/vehicles/my` | List current user's registered vehicles; delete button on each row (confirm dialog, user can only delete their own) |

### Judge
| Route | Description |
|---|---|
| `/judge/entries` | All entries in entry-number order; shows judge status badges (scored / not scored by current judge) |
| `/judge/entries/{id}/score` | Score form — 5 criteria sliders or number inputs (1–10); submit triggers confirmation dialog |
| `/judge/register-vehicle` | Register a vehicle on behalf of another user (same form as normal user) |

### Admin
| Route | Description |
|---|---|
| `/admin/entries` | All entries + which judges have scored each; Admin and Judge can delete any entry (confirm dialog) |
| `/admin/scores` | Scoring leaderboard page (described below) |
| `/admin/classes` | Add / remove vehicle classes |
| `/admin/register-vehicle` | Register vehicle on behalf of another user |

### Scoring Leaderboard (`/admin/scores`)
- Default view: all entries sorted by overall score descending
- **Filter by class** dropdown (shows vehicles that belong to the selected class; a vehicle in multiple classes appears in each class filter)
- **Sort by criteria** dropdown (Condition, Paint & Body, Interior, Show Appeal, Super Coolness Factor, or Overall)
- Table columns: Entry #, Owner Name, Make/Model/Year, Class, Condition, Paint & Body, Interior, Show Appeal, Super Coolness Factor, Overall
- Expandable rows: click to expand and see per-judge breakdown
- Virtualized using Blazor's `Virtualize` component to handle 100+ rows smoothly
- Admin and Judge can delete any entry from this view (confirm dialog before delete)
- Normal users can delete their own entries from `/vehicles/my` (confirm dialog before delete)

---

## Key Implementation Details

### Entry Numbering
Assign `EntryNumber` in `VehicleService.RegisterAsync()`:
```csharp
entry.EntryNumber = (await _db.Vehicles.MaxAsync(v => (int?)v.EntryNumber) ?? 0) + 1;
```
Wrap in a transaction to prevent duplicates under concurrent inserts.

### Score Upsert
`ScoreService.SubmitAsync()` does an upsert (update if row exists for VehicleId+JudgeId, insert otherwise). EF Core 10's `ExecuteUpdateAsync` or a manual `FindAsync` + update pattern.

### Photo Upload
`IBlobStorageService.UploadAsync(Stream, string fileName)` → returns CDN/blob URL stored in `Vehicle.PhotoUrl`. Container is set to public read (or use SAS URLs). `InputFile` Blazor component triggers the upload on file selection.

### Confirmation Dialog After Scoring
A `<ConfirmationDialog>` Blazor component (modal) is shown after a successful score submit. It displays entry number, vehicle info, and submitted scores before the judge navigates away.

### Authorization
Use `[Authorize(Roles = "Admin")]`, `[Authorize(Roles = "Admin,Judge")]` on page `@attribute` declarations. A custom `AuthorizationPolicy` can be added if more nuance is needed later.

### Role Seeding (Program.cs)
```csharp
// On startup
await SeedRolesAndAdminAsync(app.Services);
```
Creates roles Admin/Judge/User if absent, creates admin user if absent with username `admin`, password `password123`, role `Admin`.

---

## Navigation Layout

```
NavMenu (role-aware):
  All users:       Home | My Vehicles | Register Vehicle
  Judge:      +    Entries to Judge
  Admin/Judge:+    Register Vehicle (on behalf)
  Admin:      +    Manage Classes | Scores | All Entries
```

---

## Scaffold Order

1. Create solution + 3 projects, add project references
2. Core models + interfaces
3. Infrastructure: `AppDbContext`, EF migrations (InitialCreate)
4. `Program.cs`: Identity, EF, Blob, role seeding, DI registrations
5. Auth pages (login, register)
6. Vehicle registration + my vehicles (normal user flow)
7. Judge entries list + scoring form + confirmation dialog
8. Admin entries view (judge tracking)
9. Admin class management
10. Scoring leaderboard with filter/sort/expand/delete

---

## Verification

| Scenario | How to verify |
|---|---|
| Role seeding | Run app, log in as `admin` / `password123`, confirm admin nav appears |
| Vehicle registration | Register as new user, submit vehicle with photo, confirm entry number assigned and photo displays |
| Judge scoring | Log in as a judge, score an entry, verify confirmation dialog, re-score and verify first score overwritten |
| Admin class management | Add two classes, register a vehicle in both, remove one class, verify vehicle still appears in the remaining class |
| Scoring leaderboard | Submit scores for several vehicles, verify sort order by overall, then switch filter to a class and sort by Criteria 2 |
| Admin delete entry | Delete an entry from leaderboard, verify it disappears from all views |
| 100+ entries | Seed 150 vehicles, open leaderboard, verify Virtualize renders without layout issues |
