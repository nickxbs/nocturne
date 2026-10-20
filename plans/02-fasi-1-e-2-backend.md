# PR 2: Fasi 1 e 2.1–2.3 — Configurazione e Backend Signup

> **Stato**: `TODO`

## Obiettivo
Aggiungere il supporto alla registrazione pubblica lato backend, disabilitata di default:
- Estensione di `OperatorConfiguration` e DTO `MetadataController`.
- Nuovo controller `SignupController` (`POST /api/v4/signup/options`, `POST /api/v4/signup/complete`, `GET /api/v4/signup/validate-username`).
- Registrazione nella pipeline (`TenantResolutionMiddleware`, rate limit `signup`).
- Unit test completi e test di guardia.

---

## Task List

- [ ] **1. Configurazione Operatore**
  - [ ] Aggiungere `OperatorSignupConfiguration` in `OperatorConfiguration.cs`
  - [ ] Applicare la regola di attivazione combinata (`Signup.Enabled && AllowSelfServiceCreation && SetupComplete`)
  - [ ] Esporre `SignupEnabled` e `SignupRequiresApproval` nel DTO di `MetadataController.cs`
  - [ ] Aggiornare `appsettings.example.json`, `.env.example`, `scripts/publish-release.cs`

- [ ] **2. DTO di Registrazione**
  - [ ] Creare `SignupOptionsRequest`, `SignupOptionsResponse`
  - [ ] Creare `SignupCompleteRequest`, `SignupCompleteResponse`

- [ ] **3. `SignupController.cs`**
  - [ ] Implementare `POST /api/v4/signup/options`:
    - [ ] Controllo `PasskeyHostRefusal`
    - [ ] Guard registrazione attiva (404) e setup completo (409)
    - [ ] Validazione username (`IUsernameValidator`) e slug (`ITenantService.ValidateSlugAsync`)
    - [ ] Creazione/riuso enrolling subject (`IsActive = false`)
    - [ ] Chiamata a `_passkeyService.GenerateRegistrationOptionsAsync`
  - [ ] Implementare `POST /api/v4/signup/complete`:
    - [ ] Guard rivalidazione slug
    - [ ] Lookup soggetto con verifica challenge token (`expectedSubjectId`)
    - [ ] Chiamata a `_passkeyService.CompleteRegistrationAsync`
    - [ ] Attivazione soggetto (`IsActive = true`)
    - [ ] Creazione tenant (`TenantService.CreateAsync`) e assegnazione membership
    - [ ] Generazione recovery codes (`_recoveryCodeService.GenerateCodesAsync`)
    - [ ] Ramo approvazione (`RequiresApproval`): notifica platform admin, `pendingApproval = true`
    - [ ] Ramo immediato: emissione login code (`ILoginCodeService.IssueAsync`) e calcolo `handoffUrl`
    - [ ] Gestione eccezioni con rollback automatico (`DeleteSubjectAsync` + eventuale `DeleteAsync`)
  - [ ] Implementare `GET /api/v4/signup/validate-username` con `[RemoteQuery]`

- [ ] **4. Pipeline & Middleware**
  - [ ] Registrare `TenantlessPath("/api/v4/signup/", Prefix: true)` in `TenantResolutionMiddleware.cs`
  - [ ] Configurare rate limiter policy `signup` in `ServiceRegistrationExtensions.cs`

- [ ] **5. Test Unitari & Guardie**
  - [ ] Creare `SignupControllerTests.cs` (404 se disattivo, 409 se setup incompleto, 400 su validazioni, retry, rollback, approval)
  - [ ] Aggiornare `TenantlessAllowedPathsCoverageTests.cs`
  - [ ] Aggiornare `AnonymousSurfaceRateLimitTests.cs`
  - [ ] Aggiornare `TenantlessDemoSubjectCoverageTests.cs`
