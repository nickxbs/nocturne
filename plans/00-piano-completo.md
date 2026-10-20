# Piano: Registrazione con Passkey e Tenant Personale

> **Stato Generale**: `TODO` (Verifiche preliminari completate `DONE`)

## Obiettivo e scelte di fondo

Un visitatore sull'apex (`https://nocturne.cgmsim.com/signup`) sceglie username e slug, registra una passkey e si ritrova owner del tenant `https://<slug>.nocturne.cgmsim.com`, già loggato.

| Scelta | Motivo |
|---|---|
| **Utente e tenant creati nello stesso passo di completamento** | Un utente con passkey ma senza tenant non può più rientrare: sull'apex c'è solo OIDC. |
| **Niente sessione sull'apex: passaggio al sottodominio con codice di login (handoff)** | È il meccanismo già esistente (`ILoginCodeService` + `POST /api/auth/handoff`), e il sottodominio è l'unico posto dove la passkey serve. |
| **Nessuna migration** | Il tenant inattivo (`IsActive = false`) e il riuso dell'utente "in iscrizione" esistono già nel modello dati. |
| **Spento di default** | Aprire la registrazione è una scelta esplicita dell'operatore dell'istanza. |
| **Il codice di `SetupController` viene estratto e condiviso, non copiato** | Validazione username, webhook dell'operatore, username riservati rimangono coerenti. |

---

## Fasi di Implementazione

### - [ ] Fase 0: Preparazione (PR 1) — `TODO`
- [ ] 1. Riapplicare la correzione di `getAuthStatus` sulla login dell'apex: non chiamarla quando l'host non ha un tenant (`page.data.tenantless === true`).
- [ ] 2. Estrarre la validazione username da `SetupController` in `IUsernameValidator` (`Services/Identity/`) senza cambiare comportamento di `SetupControllerTests`.

### - [ ] Fase 1: Configurazione (PR 2) — `TODO`
- [ ] Aggiungere `OperatorSignupConfiguration` (`Enabled = false`, `RequiresApproval = false`) in `OperatorConfiguration`.
- [ ] Applicare la regola di attivazione: `Signup.Enabled && AllowSelfServiceCreation && SetupComplete`.
- [ ] Esporre `SignupEnabled` e `SignupRequiresApproval` nel DTO di `MetadataController`.
- [ ] Aggiornare `appsettings.example.json`, `.env.example`, `scripts/publish-release.cs`.

### - [ ] Fase 2: Backend, nuovo `SignupController` (PR 2) — `TODO`
- [ ] 2.1 `POST /api/v4/signup/options`: validazione, enrolling subject (`IsActive = false`), generazione opzioni WebAuthn.
- [ ] 2.2 `POST /api/v4/signup/complete`: rivalidazione slug, verifica attestation, creazione tenant e attivazione utente, emissione login code e handoffUrl (o stato approval), rollback su errore.
- [ ] 2.3 Pipeline e Middleware: `TenantlessPath` per `/api/v4/signup/`, policy rate limiting `signup`, query `GET /api/v4/signup/validate-username`.
- [ ] Unit test `SignupControllerTests` e aggiornamento test di guardia.

### - [ ] Fase 3: Frontend (PR 3) — `TODO`
- [ ] Rigenerazione client NSwag e remote functions.
- [ ] Route `src/routes/(unauthenticated)/signup/` (`+page.server.ts` guard, `+page.svelte` wizard 3 step).
- [ ] Link "Crea un account" sulla pagina di login dell'apex se abilitato.
- [ ] Test di integrazione (`ApiFactory` + Postgres) ed e2e (Playwright + virtual authenticator CDP).

### - [ ] Fase 2.4: Approvazione da parte del Platform Admin (PR 4) — `TODO`
- [ ] Endpoint `POST /api/v4/admin/tenants/{id}/activate` per platform admin.
- [ ] Notifica in-app `TenantApprovalRequested` all'iscrizione con approval richiesto.
- [ ] UI admin: badge "In attesa" e pulsante di attivazione su `/settings/admin/tenants`.

### - [ ] Fase 2.5: Pulizia degli Account Abbandonati (PR 5) — `TODO`
- [ ] Background service orario `EnrolmentPruningService` per eliminare account orfani `IsActive = false` > 24h e access request abbandonate.

---

## Verifiche Tecniche Eseguite — `DONE`
- [x] Unicità username: per tenant su `tenant_members`, globale su `subjects` non vincolata a DB.
- [x] RP ID e passkey apex → sottodominio: pienamente compatibili tramite `CanUseConfiguredRpId` e `AllowOriginFromClientData`.
- [x] `ILoginCodeService.IssueAsync`: attore appropriato `new AuthAuditActor(subjectId, null)`.
- [x] Rollback e cascata: `DeleteSubjectAsync` e `DeleteAsync` ripuliscono a cascata senza orfani.
- [x] Playwright e2e: virtual authenticator da configurare tramite CDP.
- [x] Riuso componenti setup: wizard di signup dedicato che riusa i blocchi atomici (`RecoveryCodes.svelte`, `useAvailability`, `startRegistration`).
