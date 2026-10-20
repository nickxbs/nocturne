# PR 4: Phase 2.4 — Platform Admin Approval

> **Status**: `TODO`

## Objective
Support the `RequiresApproval = true` mode, where newly registered tenants start inactive (`IsActive = false`) and require explicit approval by a platform admin before the user can sign in.

---

## Task List

- [ ] **1. Backend: Tenant Activation Endpoint**
  - [ ] Add `POST /api/v4/admin/tenants/{id}/activate` to `TenantController.cs` with `[Authorize(Roles = "platform_admin")]`
  - [ ] Invoke `ITenantService.UpdateAsync(id, ..., isActive: true)` without requiring tenant ownership
  - [ ] Evict resolution cache: `TenantResolutionMiddleware.EvictTenant`
  - [ ] Dismiss / archive the associated `TenantApprovalRequested` in-app notification

- [ ] **2. Platform Admin Notifications**
  - [ ] Define in-app notification type `TenantApprovalRequested`
  - [ ] Automatically publish notification in `SignupController.Complete` when `RequiresApproval = true`

- [ ] **3. Frontend Admin UI**
  - [ ] Update `src/routes/(authenticated)/settings/admin/tenants/+page.svelte`
  - [ ] Add "Pending" badge for inactive tenants
  - [ ] Add "Activate" button invoking the activation remote function

- [ ] **4. Tests**
  - [ ] Unit tests in `TenantControllerTests` (admin authorization, status change, notification dismissal)
  - [ ] Integration test on inactive tenant (receives `tenant_inactive`, activated by admin, successful login)
