# Technical Verifications in the Nocturne Codebase

> **Status**: `DONE` (All 6 points verified against the active codebase)

This document records the findings obtained from analyzing the Nocturne source code prior to development, addressing the 6 open verification points from the plan.

---

### 1. Username Uniqueness: Instance vs. Tenant?
* **Database Schema**:
  * In `SubjectEntity.cs`, `username` is an unconstrained `varchar(50)` column (`[Column("username")]`) with **no unique index**.
  * In `NocturneDbContext.cs` lines 2390–2394, the unique index is on `TenantMemberEntity`:
    ```csharp
    modelBuilder.Entity<TenantMemberEntity>()
        .HasIndex(e => new { e.TenantId, e.Username })
        .HasDatabaseName("ix_tenant_members_tenant_username")
        .IsUnique()
        .HasFilter("username IS NOT NULL");
    ```
  * Therefore, username uniqueness is strictly **per-tenant**, not global across the entire instance. Two different tenants can each have a user with the same username.
* **Authentication Flows**:
  * Both non-discoverable passkey assertion (`PasskeyService.GenerateAssertionOptionsAsync`) and recovery code verification filter by `tm.TenantId == tenantId` before matching `s.Username == username`.
* **Signup Impact**:
  * When a new tenant is created upon signup completion, the owner's username is by definition unique within that tenant.
  * During the enrollment phase (`signup/options` to `signup/complete`), temporary enrolling subjects (`IsActive = false`) must be correlated to the signup attempt to avoid race collisions if two visitors coincidentally choose the same username simultaneously.

---

### 2. Passkey Inheritance from Apex to Subdomain
* **RP ID Derivation & Origin Handling**:
  * In `ServiceRegistrationExtensions.cs` line 434:
    ```csharp
    var rpId = baseDomain.Split(':')[0];
    ```
    The RP ID is derived directly from the configured base domain (e.g., `nocturne.cgmsim.com` in production, `nocturne.localhost` in development).
  * In `PasskeyService.cs` lines 74–80:
    ```csharp
    private bool CanUseConfiguredRpId(string host)
    {
        var rpId = _fido2Config.ServerDomain;
        return _environment.IsDevelopment()
            || string.Equals(host, rpId, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{rpId}", StringComparison.OrdinalIgnoreCase);
    }
    ```
  * In `PasskeyService.cs` lines 100–125 (`AllowOriginFromClientData`), when the browser issues a ceremony from a valid subdomain of the configured RP ID, Nocturne dynamically admits that origin into FIDO2's allowed origins.
* **W3C WebAuthn Specification**:
  * A credential registered with `rpId = "nocturne.cgmsim.com"` is valid for the apex and for all subdomains (`*.nocturne.cgmsim.com`). The browser permits credential creation and assertion across any subdomain matching the RP ID suffix.
  * **Conclusion**: Passkeys registered on the apex work on subdomains natively without infrastructure changes.

---

### 3. `ILoginCodeService.IssueAsync` and `AuthAuditActor`
* In `LoginCodeService.cs` lines 67–73, `IssueAsync` requires an `AuthAuditActor actor`.
* In `IAuthAuditService.cs` lines 41–44:
  > *"Only an unauthenticated request has no caller to name — a login carries AuthContext.Unauthenticated until it succeeds — so it yields null and leaves the subject as its own actor."*
* When completing signup, the user has just successfully proved ownership of their new passkey and is the direct recipient of the handoff code.
* The correct actor parameter to provide is:
  ```csharp
  new AuthAuditActor(subjectId, null)
  ```

---

### 4. Transactions vs. Cascading Deletion in `DeleteSubjectAsync`
* **Database Relationships**:
  * `PasskeyCredentialEntity`: foreign key to `SubjectEntity` with `OnDelete: Cascade`.
  * `TenantMemberEntity`: foreign key to `SubjectEntity` with `OnDelete: Cascade`.
  * `SubjectRoleEntity`: foreign key to `SubjectEntity` with `OnDelete: Cascade`.
  * `TenantEntity`: cascading deletion configured for all tenant-scoped data (`AddTenantCascadeDeletes` migration).
* **Rollback Behavior**:
  * Calling `await _subjectService.DeleteSubjectAsync(subjectId)` removes the subject from `subjects` and PostgreSQL immediately cascades the deletion of passkey credentials and memberships.
  * If the tenant was already created before a failure occurs, calling `await _tenantService.DeleteAsync(tenantId)` purges the tenant and all associated records.
  * **Conclusion**: A manual `try/catch` invoking `DeleteSubjectAsync` and `DeleteAsync` provides complete, orphan-free cleanup.

---

### 5. Virtual Authenticator in Playwright E2E Tests
* In `e2e/src/web/`, existing tests use the dev login link (`seedTenant()`). No WebAuthn browser specs currently exist.
* To test the signup wizard end-to-end, a helper (`e2e/src/helpers/webauthn.ts`) will be introduced to initialize a virtual authenticator via Chrome DevTools Protocol (`WebAuthn.enable`, `WebAuthn.addVirtualAuthenticator`) configured with `protocol: "ctap2"`, `transport: "internal"`, `hasResidentKey: true`, and `hasUserVerification: true`.

---

### 6. Reusability of Setup Wizard Components
* Existing components (`TenantIdentity.svelte` and `AccountCreation.svelte`):
  * Tightly bound to `../setup.remote.ts`.
  * Styled with onboarding-specific dark constellation theme.
  * Reverse order of operations: in setup, the tenant is created *first* in the database, then the account; in signup, both are created *atomically at the end*.
  * `AccountCreation.svelte` includes OIDC provider options not needed for passkey-first signup.
* **Confirmed Recommendation**:
  * Create dedicated components for the signup route in `src/routes/(unauthenticated)/signup/`, while reusing standalone building blocks:
    * `RecoveryCodes.svelte`
    * `useAvailability` from `$lib/forms`
    * `startRegistration` from `@simplewebauthn/browser`
    * `describePasskeyError` and `parseCeremonyOptions` from `$lib/components/auth/passkey-errors`.
