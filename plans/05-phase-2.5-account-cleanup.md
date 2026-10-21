# PR 5: Phase 2.5 — Abandoned Account Cleanup

> **Status**: `DONE`

## Objective
Implement a background maintenance service that periodically prunes uncompleted registrations and abandoned enrollment accounts.

---

## Task List

- [x] **1. `EnrolmentPruningService.cs` Implementation**
  - [x] Create `EnrolmentPruningService` inheriting from `BackgroundService` in `src/API/Nocturne.API/Services/BackgroundServices/`
  - [x] Configure hourly execution interval respecting `CancellationToken`
  - [x] Query candidates from `subjects`:
    - [x] `IsActive == false`
    - [x] `CreatedAt < DateTime.UtcNow.AddHours(-24)`
    - [x] `!IsSystemSubject`
    - [x] No credentials in `passkey_credentials`
    - [x] No identities in `subject_oidc_identities`
    - [x] No memberships in `tenant_members`
  - [x] Remove candidates using `ISubjectService.DeleteSubjectAsync` to cascade deletions and scrub audit entries
  - [x] Prune orphaned anonymous access requests
  - [x] Structured diagnostic logging with number of purged subjects

- [x] **2. Dependency Injection Registration**
  - [x] Register `services.AddHostedService<EnrolmentPruningService>()` in `ServiceRegistrationExtensions.cs`

- [x] **3. Unit Tests**
  - [x] Unit tests in `EnrolmentPruningServiceTests`:
    - [x] Abandoned enrolling subject > 24h → deleted
    - [x] Recent subject < 24h → preserved
    - [x] Active subject or subject with credentials/memberships → preserved
