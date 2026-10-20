# PR 1: Phase 0 — Preparation

> **Status**: `IN PROGRESS`

## Objective
A preliminary, self-contained refactoring without breaking changes or behavioral shifts:
1. Prevent `getAuthStatus` from being called on the apex login page when the host has no resolved tenant (`page.data.tenantless === true`).
2. Extract the username validation logic from `SetupController` into a reusable `IUsernameValidator` service in `Services/Identity/`.

---

## Task List

- [x] **1. Fix `getAuthStatus` on tenantless hosts**
  - [x] Update `authStatusQuery` in `src/Web/packages/app/src/routes/(unauthenticated)/auth/login/+page.svelte`
  - [x] Guard the query with `tenantless ? null : getAuthStatus()`
  - [x] Verify that `allowAccessRequests` derives to `false` on tenantless hosts
  - [x] Verify frontend type check (`pnpm run check`)

- [ ] **2. Extract `IUsernameValidator`**
  - [ ] Define `IUsernameValidator` interface in `src/API/Nocturne.API/Services/Identity/IUsernameValidator.cs`
  - [ ] Implement `UsernameValidator.cs` encapsulating regex, reserved usernames, operator webhook, and availability checks
  - [ ] Register `IUsernameValidator` as scoped in `ServiceRegistrationExtensions.cs`
  - [ ] Inject `IUsernameValidator` into `SetupController.cs` and delegate validation
  - [ ] Run `SetupControllerTests` to guarantee 100% green tests without modification

---

## Implementation Details

### 1. `getAuthStatus` Fix
In `login/+page.svelte`:
```svelte
// Previously:
const authStatusQuery = getAuthStatus();
const allowAccessRequests = $derived(authStatusQuery.current?.allowAccessRequests ?? false);

// Now:
const tenantless = page.data.tenantless === true;
const authStatusQuery = tenantless ? null : getAuthStatus();
const allowAccessRequests = $derived(authStatusQuery?.current?.allowAccessRequests ?? false);
```

### 2. `IUsernameValidator` Service
Contract definition:
```csharp
namespace Nocturne.API.Services.Identity;

public interface IUsernameValidator
{
    Task<SlugValidationResult> ValidateAsync(
        string username,
        Guid? tenantId = null,
        CancellationToken ct = default);
}
```

---

## Acceptance Criteria
- [ ] `dotnet test --filter "FullyQualifiedName~SetupControllerTests"` passes with zero failures.
- [x] `cd src/Web/packages/app && pnpm run check` passes with zero errors.
