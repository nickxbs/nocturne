# PR 3: Phase 3 — Frontend & Integration / E2E Tests

> **Status**: `TODO`

## Objective
Implement the visitor-facing signup flow on the apex domain, along with end-to-end testing using a virtual WebAuthn authenticator:
- NSwag client codegen and remote functions generation.
- New route `src/routes/(unauthenticated)/signup/`.
- 3-step wizard with passkey creation and handoff.
- Apex login link integration on `/auth/login`.
- Integration tests (`ApiFactory` + Postgres) and e2e specs (Playwright).

---

## Task List

- [ ] **1. NSwag Client Codegen**
  - [ ] Run `dotnet build src/API/Nocturne.API/Nocturne.API.csproj -p:GenerateNSwagClient=true`
  - [ ] Verify generated `signups.generated.remote.ts`

- [ ] **2. Frontend Route `/signup`**
  - [ ] Create `src/routes/(unauthenticated)/signup/+page.server.ts`:
    - [ ] Guard: tenantless host and `SignupEnabled`
    - [ ] Redirect to `/auth/login` when disallowed
  - [ ] Create `src/routes/(unauthenticated)/signup/+page.svelte`:
    - [ ] Step 1: Account (username with `useAvailability` + display name)
    - [ ] Step 2: Workspace (tenant slug with `useAvailability` + display name + live URL preview)
    - [ ] Step 3: Passkey (ceremony via `startRegistration` + recovery codes via `RecoveryCodes.svelte` + redirect via `handoffUrl`)
    - [ ] Approval pending screen when `pendingApproval` is true

- [ ] **3. Apex Login Page Integration**
  - [ ] Add "Create an account" link on `src/routes/(unauthenticated)/auth/login/+page.svelte` (conditional on `tenantless && signupEnabled`)

- [ ] **4. Integration Tests**
  - [ ] Write `tests/Integration/Nocturne.API.Tests/Auth/SignupIntegrationTests.cs`:
    - [ ] Full cycle: options → complete → login code → handoff → session verification & RLS tenant isolation

- [ ] **5. End-to-End Tests with Virtual Authenticator**
  - [ ] Implement CDP WebAuthn helper in `e2e/src/helpers/webauthn.ts`
  - [ ] Create Playwright spec `e2e/src/web/signup.e2e-spec.ts`

- [ ] **6. Quality & Translations**
  - [ ] Add translation keys to `src/Web/locales/`
  - [ ] Run `pnpm run translations:stage`
  - [ ] Run `pnpm run check` and `pnpm run lint:ci`
