# CGMSIM Onboarding Plan: Tenant Initialization Modal & Parameters

## 1. Overview

When entering a tenant workspace for the first time, users must initialize the CGMSIM configuration before accessing the dashboard and features.
This plan defines a full-screen, non-dismissible modal wizard at the `(authenticated)` layout level that prompts the user for:
1. **Dati Paziente / Subject**: Sesso, Peso (kg), Età (anni), Altezza (cm).
2. **Parametri Simulazione**: ISF, DIA, Tempo assorbimento carboidrati, Picco insulinico, Rapporto I:C (CR).

Key requirements:
- **Blocking modal dialog** over the layout (without changing URL), non-dismissible until completed.
- Both patient and simulation parameter fields start **empty**.
- Each step features a **"Genera valori casuali"** button that fills fields with clinically realistic values within valid ranges.
- Submission is **atomic** via a single API endpoint (`POST /api/v4/cgmsim/initialize`) that saves both sets of parameters and sets `IsInitialized = true`.
- For users without write permissions (read-only / viewers), a friendly informational waiting screen is shown instead of form inputs.
- Only active on tenant hosts (skipped on tenantless apex and public share hosts).

---

## 2. Database & Entity Model

### Entity Updates: `CgmSimSettingsEntity`
File: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/CgmSimSettingsEntity.cs`
- Add column:
  ```csharp
  [Column("is_initialized")]
  public bool IsInitialized { get; set; } = false;
  ```

### EF Core Migration
- Run: `dotnet build -p:GenerateNSwagClient=false`
- Run: `dotnet ef migrations add AddCgmSimIsInitialized -p src/Infrastructure/Nocturne.Infrastructure.Data.Migrations -s src/API/Nocturne.API`
- Inspect migration to ensure PostgreSQL conventions and RLS compatibility.

---

## 3. Core Models & DTOs

Directory: `src/Core/Nocturne.Core.Models/CgmSim/`
File: `CgmSimModels.cs`
- Update `CgmSimSettings`:
  ```csharp
  public bool IsInitialized { get; set; }
  ```
- Add `CgmSimInitializeRequest`:
  ```csharp
  public class CgmSimInitializeRequest
  {
      [Required]
      public CgmSimSubject Subject { get; set; } = new();

      [Required]
      public CgmSimParameters Parameters { get; set; } = new();
  }
  ```

---

## 4. API Layer: `CgmSimController`

File: `src/API/Nocturne.API/Controllers/V4/CgmSim/CgmSimController.cs`
- Update `GetSettings`: map `IsInitialized = entity.IsInitialized`.
- Add endpoint:
  ```csharp
  [HttpPost("initialize")]
  [RemoteCommand(Invalidates = ["GetSettings", "GetSubject", "GetParameters"])]
  [ProducesResponseType(typeof(CgmSimSettings), StatusCodes.Status200OK)]
  public async Task<ActionResult<CgmSimSettings>> Initialize(
      [FromBody] CgmSimInitializeRequest request,
      CancellationToken ct)
  ```
  - Validates request models.
  - Updates entity fields for both `Subject` and `Parameters`.
  - Sets `entity.IsInitialized = true`.
  - Saves changes atomically.
  - Returns updated `CgmSimSettings`.

---

## 5. Codegen Pipeline

Run:
```bash
dotnet build src/API/Nocturne.API/Nocturne.API.csproj -p:GenerateNSwagClient=true
```
This updates:
- OpenAPI specification.
- TypeScript interfaces in API client.
- SvelteKit remote functions in `src/Web/packages/app/src/lib/api/generated/cgmSim.generated.remote.ts`:
  - `initialize(...)`
  - `getSettings()` (now returns `isInitialized`)

---

## 6. Frontend: Full-Screen Onboarding Dialog

### 6.1 Randomizer Utilities
Define clinically sensible random value generators within valid ranges:
- **Subject**:
  - `sex`: random choice between `"male"`, `"female"`, `"other"`
  - `weight`: random float between 50.0 and 95.0 kg (1 decimal)
  - `age`: random integer between 18 and 68 years
  - `height`: random float between 155.0 and 190.0 cm (1 decimal)
- **Parameters**:
  - `isf`: random float between 30.0 and 80.0 mg/dL/U (step 5)
  - `dia`: random float between 3.0 and 5.5 hours (1 decimal)
  - `carbsAbsTime`: random integer between 120 and 240 minutes (step 15)
  - `insulinPeak`: random integer between 50 and 90 minutes (step 5)
  - `cr`: random float between 8.0 and 15.0 g/U (1 decimal)

### 6.2 Component: `CgmSimOnboardingDialog.svelte`
Path: `src/Web/packages/app/src/lib/components/cgmsim/CgmSimOnboardingDialog.svelte`
- Built using shadcn-svelte (`Dialog`, `Card`, `Button`, `Input`, `Label`, `Select`).
- Non-dismissible: cannot be closed via Escape, outside click, or close button.
- Multi-step wizard:
  - Header: Indicator for **Passaggio 1: Dati Paziente** e **Passaggio 2: Parametri di Simulazione**.
  - Step 1:
    - Form fields for Sex, Weight, Age, Height.
    - Button with icon (`Dices` or `Sparkles`): *"Genera valori casuali"*.
    - Button: *"Avanti"* (validates step 1 before proceeding).
  - Step 2:
    - Form fields for ISF, DIA, Carbs Absorption Time, Insulin Peak, CR.
    - Button with icon: *"Genera valori casuali"*.
    - Button: *"Indietro"* (returns to step 1) and *"Completa e Inizia"* (submits `initialize()`).
  - Read-only fallback view:
    - If user lacks write permissions, shows an informational screen explaining that workspace setup is pending by an administrator.

### 6.3 Layout Integration
File: `src/Web/packages/app/src/routes/(authenticated)/+layout.server.ts` & `+layout.svelte`
- Load `isCgmSimInitialized` in `+layout.server.ts` when authenticated and not on a tenantless or share host.
- In `+layout.svelte`:
  - If `!isCgmSimInitialized`: mount and display `CgmSimOnboardingDialog`.
  - Once initialized, dialog disappears reactively and dashboard is unlocked.

---

## 7. Testing & Verification

1. **Backend Tests**:
   - Unit test for `CgmSimController.Initialize` verifying atomic update and `IsInitialized = true`.
   - Run: `dotnet test --filter "Category!=Integration&Category!=Performance"`
2. **Frontend Type Checking & Lint**:
   - `cd src/Web/packages/app && pnpm run check`
   - `cd src/Web && pnpm --recursive --no-bail run lint:ci`
3. **Manual / End-to-End Verification**:
   - Create a fresh tenant or check uninitialized tenant.
   - Verify modal opens and cannot be dismissed.
   - Verify "Genera valori casuali" populates realistic values for both steps.
   - Verify submission succeeds and unlocks the tenant dashboard.
