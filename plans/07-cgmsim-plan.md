# CGMSIM Feature Plan: Subject & Parameters Configuration

## 1. Overview

This feature introduces a new **CGMSIM** menu item in the Nocturne web application with two sub-pages:
- **Subject**: Configure physiological properties of the simulated subject (sex, weight, age, height).
- **Parameters**: Configure simulation parameters (ISF, DIA, carbs absorption time, insulin peak in minutes, CR).

Each tenant has a single (1:1) configuration. These settings are persisted in PostgreSQL via EF Core with multi-tenancy isolation (`ITenantScoped` with Row-Level Security and unique constraint per tenant), and accessed via dedicated REST APIs with NSwag-generated remote client functions.

---

## 2. Database & Entity Model

### Entity: `CgmSimSettingsEntity`
File: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/CgmSimSettingsEntity.cs`
- **Table**: `cgmsim_settings`
- **Interfaces**: `ITenantScoped`, `IEntityTimestamped`
- **Columns**:
  - `id`: `uuid` (Primary Key, UUID v7)
  - `tenant_id`: `uuid` (Foreign key to `tenants.id`, unique index `IX_cgmsim_settings_tenant_id_unique`)
  - **Subject fields (all mandatory, non-nullable)**:
    - `sex`: `varchar(32)` (mandatory: e.g. "male", "female", "other", default "other")
    - `weight`: `double precision` (mandatory, weight in kg, default 70.0)
    - `age`: `integer` (mandatory, age in years, default 30)
    - `height`: `double precision` (mandatory, height in cm, default 170.0)
  - **Simulation parameters (all mandatory, non-nullable)**:
    - `isf`: `double precision` (mandatory, Insulin Sensitivity Factor in mg/dL/U, default 50.0)
    - `dia`: `double precision` (mandatory, Duration of Insulin Action in hours, default 4.0)
    - `carbs_abs_time`: `integer` (mandatory, carb absorption duration in minutes, default 180)
    - `insulin_peak`: `integer` (mandatory, insulin peak time in minutes, default 75)
    - `cr`: `double precision` (mandatory, Carb Ratio in g/U, default 10.0)
  - **Timestamps**:
    - `created_at`: `timestamptz`
    - `updated_at`: `timestamptz`

### DbContext Configuration:
File: `src/Infrastructure/Nocturne.Infrastructure.Data/NocturneDbContext.cs`
- Add `public DbSet<CgmSimSettingsEntity> CgmSimSettings { get; set; }`.
- Register entity in `NocturneDbContext.cs`:
  - `entity.ToTable("cgmsim_settings");`
  - `entity.HasIndex(e => e.TenantId).IsUnique().HasDatabaseName("IX_cgmsim_settings_tenant_id_unique");`
  - Add `typeof(CgmSimSettingsEntity)` to timestamp marker column collections (`CreatedAt`, `UpdatedAt`).
- Add EF Core migration in `src/Infrastructure/Nocturne.Infrastructure.Data.Migrations`.

---

## 3. Core Domain Models & DTOs

Directory: `src/Core/Nocturne.Core.Models/CgmSim/`
- **`CgmSimSubject`**:
  - `[Required, MaxLength(32)] string Sex` (default: "other")
  - `[Required, Range(1.0, 500.0)] double Weight` (kg, default: 70.0)
  - `[Required, Range(1, 150)] int Age` (years, default: 30)
  - `[Required, Range(20.0, 300.0)] double Height` (cm, default: 170.0)
- **`CgmSimParameters`**:
  - `[Required, Range(1.0, 1000.0)] double Isf` (default: 50.0)
  - `[Required, Range(0.5, 24.0)] double Dia` (hours, default: 4.0)
  - `[Required, Range(5, 1440)] int CarbsAbsTime` (minutes, default: 180)
  - `[Required, Range(5, 360)] int InsulinPeak` (minutes, default: 75)
  - `[Required, Range(0.1, 200.0)] double Cr` (g/U, default: 10.0)
- **`CgmSimSettings`**:
  - Composition of subject + parameters + `DateTime UpdatedAt`.

---

## 4. API Layer: `CgmSimController`

File: `src/API/Nocturne.API/Controllers/V4/CgmSim/CgmSimController.cs`
- Route: `api/v4/cgmsim`
- Tags: `[Tags("CgmSim")]`, `[ApiController]`, `[Authorize]`
- Endpoints:
  - `GET /api/v4/cgmsim/subject` `[RemoteQuery]`
    - Retrieves or creates default subject parameters for the current tenant.
  - `PUT /api/v4/cgmsim/subject` `[RemoteForm(Invalidates = ["GetSubject"])]`
    - Updates subject parameters (`CgmSimSubject`).
  - `GET /api/v4/cgmsim/parameters` `[RemoteQuery]`
    - Retrieves or creates default simulation parameters for the current tenant.
  - `PUT /api/v4/cgmsim/parameters` `[RemoteForm(Invalidates = ["GetParameters"])]`
    - Updates simulation parameters (`CgmSimParameters`).

---

## 5. NSwag & Remote Functions Codegen

- Trigger NSwag generation with `dotnet build src/API/Nocturne.API/Nocturne.API.csproj -p:GenerateNSwagClient=true`.
- Output: `src/Web/packages/app/src/lib/api/generated/cgmSim.generated.remote.ts` providing type-safe remote functions:
  - `getSubject()`
  - `updateSubject()`
  - `getParameters()`
  - `updateParameters()`

---

## 6. Frontend Navigation & Pages

### Navigation
File: `src/Web/packages/app/src/lib/navigation/app-navigation.svelte.ts`
- Add `"cgmsim"` to `NavItemId`.
- Add navigation item with Lucide icon (`Activity`):
  - Group: `CGMSIM`
  - Children:
    - `Subject` -> `/cgmsim/subject` (Icon: `User`)
    - `Parameters` -> `/cgmsim/parameters` (Icon: `Sliders`)

### Pages
Directory: `src/Web/packages/app/src/routes/(authenticated)/cgmsim/`
- `+page.svelte`: redirects to `/cgmsim/subject`.
- `subject/+page.svelte`:
  - Form built with shadcn-svelte (`Card`, `Input`, `Select`, `Label`, `Button`).
  - Fields: Sex (Male, Female, Other), Weight (kg), Age (years), Height (cm).
  - Uses `getSubject()` and `updateSubject()` remote functions.
  - Feedback toast / notification on save.
- `parameters/+page.svelte`:
  - Form built with shadcn-svelte (`Card`, `Input`, `Label`, `Button`).
  - Fields: ISF (mg/dL/U), DIA (hours), Carbs absorption time (minutes), Insulin peak (minutes), CR (g/U).
  - Uses `getParameters()` and `updateParameters()` remote functions.
  - Feedback toast / notification on save.

---

## 7. Testing & Verification

- **Backend Unit Tests**: `tests/Unit/Nocturne.API.Tests/Controllers/V4/CgmSimControllerTests.cs` verifying Get and Put operations.
- **Frontend Typecheck & Linting**:
  - `pnpm run check` in `src/Web/packages/app`
  - `pnpm --recursive --no-bail run lint:ci` in `src/Web`
