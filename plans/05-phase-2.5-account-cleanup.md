# PR 5: Phase 2.5 — Abandoned Account Cleanup

> **Status**: `TODO`

## Objective
Implement a background maintenance service that periodically prunes uncompleted registrations and abandoned enrollment accounts.

---

## Task List

- [ ] **1. `EnrolmentPruningService.cs` Implementation**
  - [ ] Create `EnrolmentPruningService` inheriting from `BackgroundService` in `src/API/Nocturne.API/Services/Background/`
  - [ ] Configure hourly execution interval respecting `CancellationToken`
  - [ ] Query candidates from `subjects`:
    - [ ] `IsActive == false`
    - [ ] `SysCreatedAt < DateTime.UtcNow.AddHours(-24)`
    - [ ] `!IsSystemSubject`
    - [ ] No credentials in `passkey_credentials`
    - [ ] No identities in `subject_oidc_identities`
    - [ ] No memberships in `tenant_members`
  - [ ] Remove candidates using `ISubjectService.DeleteSubjectAsync` to cascade deletions and scrub audit entries
  - [ ] Prune orphaned anonymous access requests
  - [ ] Structured diagnostic logging with number of purged subjects

- [ ] **2. Dependency Injection Registration**
  - [ ] Register `services.AddHostedService<EnrolmentPruningService>()` in `ServiceRegistrationExtensions.cs`

- [ ] **3. Unit Tests**
  - [ ] Unit tests in `EnrolmentPruningServiceTests`:
    - [ ] Abandoned enrolling subject > 24h → deleted
    - [ ] Recent subject < 24h → preserved
    - [ ] Active subject or subject with credentials/memberships → preserved
