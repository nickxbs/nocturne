# PR 4: Phase 2.4 — Platform Admin Approval

> **Status**: `DONE`

## Objective
Support the `RequiresApproval = true` mode, where newly registered tenants start inactive (`IsActive = false`) and require explicit approval by a platform admin before the user can sign in.

---

## Task List

- [x] **1. Backend: Tenant Activation Endpoint**
  - [x] Add `POST /api/v4/admin/tenants/{id}/activate` to `TenantController.cs` with `[Authorize(Roles = "platform_admin")]`
  - [x] Invoke `ITenantService.UpdateAsync(id, ..., isActive: true)` without requiring tenant ownership
  - [x] Evict resolution cache: `TenantResolutionMiddleware.EvictTenant`
  - [x] Dismiss / archive the associated `TenantApprovalRequested` in-app notification

- [x] **2. Platform Admin Notifications**
  - [x] Define in-app notification type `TenantApprovalRequested`
  - [x] Automatically publish notification in `SignupController.Complete` when `RequiresApproval = true`

- [x] **3. Frontend Admin UI**
  - [x] Update `src/routes/(authenticated)/settings/admin/tenants/+page.svelte`
  - [x] Add "Pending" badge for inactive tenants
  - [x] Add "Activate" button invoking the activation remote function

- [x] **4. Tests**
  - [x] Unit tests in `TenantControllerTests` (admin authorization, status change, notification dismissal)
  - [x] Integration test on inactive tenant (receives `tenant_inactive`, activated by admin, successful login)
