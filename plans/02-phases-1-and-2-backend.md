# PR 2: Phases 1 and 2.1–2.3 — Configuration & Backend Signup

> **Status**: `DONE`

## Objective
Introduce backend support for public self-service signup, disabled by default:
- Extend `OperatorConfiguration` and `MetadataController` DTOs.
- Create `SignupController` (`POST /api/v4/signup/options`, `POST /api/v4/signup/complete`, `GET /api/v4/signup/validate-username`).
- Integrate into HTTP pipeline (`TenantResolutionMiddleware`, `signup` rate limit policy).
- Full unit tests and guard test coverage.

---

## Task List

- [x] **1. Operator Configuration**
  - [x] Add `OperatorSignupConfiguration` to `OperatorConfiguration.cs` (`Enabled = false`, `RequiresApproval = false`)
  - [x] Enforce combined activation rule (`Signup.Enabled && AllowSelfServiceCreation && SetupComplete`)
  - [x] Expose `SignupEnabled` and `SignupRequiresApproval` via `MetadataController.cs`
  - [x] Update `appsettings.example.json` and `scripts/publish-release.cs`

- [x] **2. Signup DTOs**
  - [x] Define `SignupOptionsRequest`, `SignupOptionsResponse`
  - [x] Define `SignupCompleteRequest`, `SignupCompleteResponse`

- [x] **3. `SignupController.cs` Implementation**
  - [x] Implement `POST /api/v4/signup/options`:
    - [x] `PasskeyHostRefusal` check
    - [x] Guard: active signup (404) and setup complete (409)
    - [x] Username (`IUsernameValidator`) and slug (`ITenantService.ValidateSlugAsync`) validation
    - [x] Create or reuse enrolling subject (`IsActive = false`)
    - [x] Invoke `_passkeyService.GenerateRegistrationOptionsAsync`
  - [x] Implement `POST /api/v4/signup/complete`:
    - [x] Slug re-validation against concurrent claims
    - [x] Secure subject lookup with challenge token verification (`expectedSubjectId`)
    - [x] Invoke `_passkeyService.CompleteRegistrationAsync`
    - [x] Activate subject (`IsActive = true`)
    - [x] Create tenant (`TenantService.CreateAsync`) and set member username
    - [x] Generate recovery codes (`_recoveryCodeService.GenerateCodesAsync`)
    - [x] If `RequiresApproval`: notify platform admins, set `pendingApproval = true`
    - [x] If active: issue login code (`ILoginCodeService.IssueAsync`) and construct `handoffUrl`
    - [x] Exception handling with automatic cascading rollback (`DeleteSubjectAsync` + `DeleteAsync`)
  - [x] Implement `GET /api/v4/signup/validate-username` with `[RemoteQuery]`

- [x] **4. Pipeline & Middleware**
  - [x] Register `TenantlessPath("/api/v4/signup/", Prefix: true)` in `TenantResolutionMiddleware.cs`
  - [x] Register rate limit policy `signup` in `ServiceRegistrationExtensions.cs`

- [x] **5. Unit Tests & Guard Tests**
  - [x] Create `SignupControllerTests.cs` (404 when disabled, 409 when setup incomplete, 400 validations, retry, rollback, approval)
  - [x] Update `TenantlessAllowedPathsCoverageTests.cs`
  - [x] Update `AnonymousSurfaceRateLimitTests.cs`
  - [x] Update `TenantlessDemoSubjectCoverageTests.cs`
