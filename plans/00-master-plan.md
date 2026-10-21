# Master Plan: Passkey Signup and Personal Tenant Provisioning

> **Overall Status**: `DONE` (All 5 PRs and Technical Verifications completed)

## Objective and Core Decisions

A visitor arriving at the apex domain (`https://nocturne.cgmsim.com/signup`) chooses a username and tenant slug, registers a passkey, and is immediately redirected as the logged-in owner of their new tenant `https://<slug>.nocturne.cgmsim.com`.

| Decision | Rationale |
|---|---|
| **User and tenant created in the same completion step** | A user with a registered passkey but no tenant cannot sign in: only OIDC is available on the apex. |
| **No session on apex: handoff to subdomain via login code** | Leverages the existing mechanism (`ILoginCodeService` + `POST /api/auth/handoff`), and the tenant subdomain is the only place where the passkey will be used. |
| **No database migrations required** | Inactive tenants (`IsActive = false`) and reusing an enrolling subject (`IsActive = false`) are already supported by the database schema. |
| **Disabled by default** | Opening self-service registration must be an explicit decision made by the instance operator. |
| **`SetupController` logic extracted and shared, not copied** | Username regex validation, reserved usernames, operator webhook, and uniqueness checks remain consistent. |

---

## Implementation Phases

### - [x] Phase 0: Preparation (PR 1) — `DONE`
- [x] 1. Reapply `getAuthStatus` fix on the apex login page: do not invoke it when the host has no tenant (`page.data.tenantless === true`).
- [x] 2. Extract username validation from `SetupController` into a shared `IUsernameValidator` service in `Services/Identity/`. Ensure `SetupControllerTests` remain 100% green without modification.

### - [x] Phase 1: Configuration (PR 2) — `DONE`
- [x] Add `OperatorSignupConfiguration` (`Enabled = false`, `RequiresApproval = false`) to `OperatorConfiguration`.
- [x] Apply activation rule: signup is active only if `Signup.Enabled && AllowSelfServiceCreation && SetupComplete`.
- [x] Expose `SignupEnabled` and `SignupRequiresApproval` via `MetadataController`'s `MultitenancyInfo` DTO.
- [x] Update `appsettings.example.json`, `.env.example`, and `scripts/publish-release.cs`.

### - [x] Phase 2: Backend `SignupController` (PR 2) — `DONE`
- [x] 2.1 `POST /api/v4/signup/options`: request validation, enrolling subject lookup/creation (`IsActive = false`), WebAuthn registration options generation.
- [x] 2.2 `POST /api/v4/signup/complete`: slug re-validation, attestation verification, tenant creation and subject activation (`IsActive = true`), recovery codes generation, login code issuance and `handoffUrl` generation (or approval pending state), automatic rollback on error.
- [x] 2.3 Pipeline & Middleware: add `TenantlessPath("/api/v4/signup/", Prefix: true)` to `TenantResolutionMiddleware`, configure rate limit policy `signup`, provide real-time username availability endpoint `GET /api/v4/signup/validate-username`.
- [x] Unit tests (`SignupControllerTests`) and update guard tests (`TenantlessAllowedPathsCoverageTests`, `AnonymousSurfaceRateLimitTests`, `TenantlessDemoSubjectCoverageTests`).

### - [x] Phase 3: Frontend (PR 3) — `DONE`
- [x] Regenerate NSwag client and remote functions (`dotnet build ... -p:GenerateNSwagClient=true`).
- [x] Implement `src/routes/(unauthenticated)/signup/` route (`+page.server.ts` guard, `+page.svelte` 3-step wizard).
- [x] Add "Create an account" link on the apex login page when signup is enabled.
- [x] Integration tests (`ApiFactory` + Postgres) and e2e tests (Playwright + CDP virtual authenticator).

### - [x] Phase 2.4: Platform Admin Approval (PR 4) — `DONE`
- [x] Dedicated endpoint `POST /api/v4/admin/tenants/{id}/activate` for platform admins.
- [x] In-app notification `tenant.approval_requested` for platform admins when registration requires approval.
- [x] Admin UI on `/settings/admin/tenants`: "Pending" status badge and "Activate" action.

### - [x] Phase 2.5: Abandoned Account Cleanup (PR 5) — `DONE`
- [x] Hourly background service `EnrolmentPruningService` to purge unactivated subjects (`IsActive = false`, older than 24 hours, no credentials, no OIDC identities, no memberships) and orphaned access requests.

---

## Technical Verifications Completed — `DONE`
- [x] Username uniqueness: scoped per-tenant on `tenant_members`, unconstrained globally on `subjects`.
- [x] RP ID & passkey inheritance (apex to subdomain): fully supported by WebAuthn standard and existing `PasskeyService.CanUseConfiguredRpId` / `AllowOriginFromClientData`.
- [x] `ILoginCodeService.IssueAsync`: actor correctly passed as `new AuthAuditActor(subjectId, null)`.
- [x] Rollback & cascading deletion: `DeleteSubjectAsync` and `DeleteAsync` cleanly cascade in Postgres without leaving orphans.
- [x] Playwright e2e: virtual authenticator setup to be integrated via Chrome DevTools Protocol (CDP).
- [x] Setup components reusability: create dedicated signup wizard steps while reusing atomic auth building blocks (`RecoveryCodes.svelte`, `useAvailability`, `startRegistration`).
