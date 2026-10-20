# PR 2: Phases 1 and 2.1–2.3 — Configuration & Backend Signup

> **Status**: `TODO`

## Objective
Introduce backend support for public self-service signup, disabled by default:
- Extend `OperatorConfiguration` and `MetadataController` DTOs.
- Create `SignupController` (`POST /api/v4/signup/options`, `POST /api/v4/signup/complete`, `GET /api/v4/signup/validate-username`).
- Integrate into HTTP pipeline (`TenantResolutionMiddleware`, `signup` rate limit policy).
- Full unit tests and guard test coverage.

---

## Task List

- [ ] **1. Operator Configuration**
  - [ ] Add `OperatorSignupConfiguration` to `OperatorConfiguration.cs` (`Enabled = false`, `RequiresApproval = false`)
  - [ ] Enforce combined activation rule (`Signup.Enabled && AllowSelfServiceCreation && SetupComplete`)
  - [ ] Expose `SignupEnabled` and `SignupRequiresApproval` via `MetadataController.cs`
  - [ ] Update `appsettings.example.json`, `.env.example`, and `scripts/publish-release.cs`

- [ ] **2. Signup DTOs**
  - [ ] Define `SignupOptionsRequest`, `SignupOptionsResponse`
  - [ ] Define `SignupCompleteRequest`, `SignupCompleteResponse`

- [ ] **3. `SignupController.cs` Implementation**
  - [ ] Implement `POST /api/v4/signup/options`:
    - [ ] `PasskeyHostRefusal` check
    - [ ] Guard: active signup (404) and setup complete (409)
    - [ ] Username (`IUsernameValidator`) and slug (`ITenantService.ValidateSlugAsync`) validation
    - [ ] Create or reuse enrolling subject (`IsActive = false`)
    - [ ] Invoke `_passkeyService.GenerateRegistrationOptionsAsync`
  - [ ] Implement `POST /api/v4/signup/complete`:
    - [ ] Slug re-validation against concurrent claims
    - [ ] Secure subject lookup with challenge token verification (`expectedSubjectId`)
    - [ ] Invoke `_passkeyService.CompleteRegistrationAsync`
    - [ ] Activate subject (`IsActive = true`)
    - [ ] Create tenant (`TenantService.CreateAsync`) and set member username
    - [ ] Generate recovery codes (`_recoveryCodeService.GenerateCodesAsync`)
    - [ ] If `RequiresApproval`: notify platform admins, set `pendingApproval = true`
    - [ ] If active: issue login code (`ILoginCodeService.IssueAsync`) and construct `handoffUrl`
    - [ ] Exception handling with automatic cascading rollback (`DeleteSubjectAsync` + `DeleteAsync`)
  - [ ] Implement `GET /api/v4/signup/validate-username` with `[RemoteQuery]`

- [ ] **4. Pipeline & Middleware**
  - [ ] Register `TenantlessPath("/api/v4/signup/", Prefix: true)` in `TenantResolutionMiddleware.cs`
  - [ ] Register rate limit policy `signup` in `ServiceRegistrationExtensions.cs`

- [ ] **5. Unit Tests & Guard Tests**
  - [ ] Create `SignupControllerTests.cs` (404 when disabled, 409 when setup incomplete, 400 validations, retry, rollback, approval)
  - [ ] Update `TenantlessAllowedPathsCoverageTests.cs`
  - [ ] Update `AnonymousSurfaceRateLimitTests.cs`
  - [ ] Update `TenantlessDemoSubjectCoverageTests.cs`
