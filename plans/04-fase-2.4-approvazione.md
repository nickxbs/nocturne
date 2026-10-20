# PR 4: Fase 2.4 — Approvazione Platform Admin

> **Stato**: `TODO`

## Obiettivo
Supportare la modalità `RequiresApproval = true`, in cui i nuovi tenant registrati nascono inattivi (`IsActive = false`) e richiedono l'approvazione esplicita di un platform admin prima che l'utente possa effettuare l'accesso.

---

## Task List

- [ ] **1. Backend: Attivazione Tenant**
  - [ ] Aggiungere `POST /api/v4/admin/tenants/{id}/activate` in `TenantController.cs` con `[Authorize(Roles = "platform_admin")]`
  - [ ] Chiamare `ITenantService.UpdateAsync(id, ..., isActive: true)`
  - [ ] Evicting cache: `TenantResolutionMiddleware.EvictTenant`
  - [ ] Archiviazione della notifica `TenantApprovalRequested` associata

- [ ] **2. Notifiche Platform Admin**
  - [ ] Creare tipo di notifica in-app `TenantApprovalRequested`
  - [ ] Emissione automatica in `SignupController.Complete` quando `RequiresApproval = true`

- [ ] **3. Frontend Admin UI**
  - [ ] Aggiornare `src/routes/(authenticated)/settings/admin/tenants/+page.svelte`
  - [ ] Badge visivo "In attesa" sui tenant inattivi
  - [ ] Azione "Attiva" che chiama la nuova remote function di attivazione

- [ ] **4. Test**
  - [ ] Unit test in `TenantControllerTests` (permessi admin, transizione stato, archiviazione notifica)
  - [ ] Integration test su tenant inattivo (risposta `tenant_inactive`, attivazione, login abilitato)
