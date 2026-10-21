# PR 3: Phase 3 — Frontend & Integration / E2E Tests

> **Status**: `DONE`

## Objective
Implement the visitor-facing signup flow on the apex domain, along with end-to-end testing using a virtual WebAuthn authenticator:
- NSwag client codegen and remote functions generation.
- New route `src/routes/(unauthenticated)/signup/`.
- 3-step wizard with passkey creation and handoff.
- Apex login link integration on `/auth/login`.
- Integration tests (`ApiFactory` + Postgres) and e2e specs (Playwright).

---

## Task List

- [x] **1. NSwag Client Codegen**
  - [x] Run `dotnet build src/API/Nocturne.API/Nocturne.API.csproj -p:GenerateNSwagClient=true`
  - [x] Verify generated `signups.generated.remote.ts`

- [x] **2. Frontend Route `/signup`**
  - [x] Create `src/routes/(unauthenticated)/signup/+page.server.ts`:
    - [x] Guard: tenantless host and `SignupEnabled`
    - [x] Redirect to `/auth/login` when disallowed
  - [x] Create `src/routes/(unauthenticated)/signup/+page.svelte`:
    - [x] Step 1: Account (username with `useAvailability` + display name)
    - [x] Step 2: Workspace (tenant slug with `useAvailability` + display name + live URL preview)
    - [x] Step 3: Passkey (ceremony via `startRegistration` + recovery codes via `RecoveryCodes.svelte` + redirect via `handoffUrl`)
    - [x] Approval pending screen when `pendingApproval` is true

- [x] **3. Apex Login Page Integration**
  - [x] Add "Create an account" link on `src/routes/(unauthenticated)/auth/login/+page.svelte` (conditional on `tenantless && signupEnabled`)

- [x] **4. Integration Tests**
  - [x] Write `tests/Integration/Nocturne.API.Tests/Auth/SignupIntegrationTests.cs`:
    - [x] Full cycle: options → complete → login code → handoff → session verification & RLS tenant isolation

- [x] **5. End-to-End Tests with Virtual Authenticator**
  - [x] Implement CDP WebAuthn helper in `e2e/src/helpers/webauthn.ts`
  - [x] Create Playwright spec `e2e/src/web/signup.e2e-spec.ts`

- [x] **6. Quality & Translations**
  - [x] Add translation keys via `translations:sync`
  - [x] Run `pnpm run check` and `pnpm run lint:ci`
