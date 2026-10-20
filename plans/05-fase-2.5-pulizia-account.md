# PR 5: Fase 2.5 — Pulizia degli Account Abbandonati

> **Stato**: `TODO`

## Obiettivo
Implementare un servizio in background periodico che elimini gli account e le richieste rimaste incomplete o abbandonate prima del termine della cerimonia di registrazione.

---

## Task List

- [ ] **1. Implementazione `EnrolmentPruningService.cs`**
  - [ ] Creare il servizio derivato da `BackgroundService` in `src/API/Nocturne.API/Services/Background/`
  - [ ] Timer orario con cancellazione su `CancellationToken`
  - [ ] Query sui soggetti:
    - [ ] `IsActive == false`
    - [ ] `SysCreatedAt < DateTime.UtcNow.AddHours(-24)`
    - [ ] `!IsSystemSubject`
    - [ ] Nessuna credenziale in `passkey_credentials`
    - [ ] Nessuna identità in `subject_oidc_identities`
    - [ ] Nessuna membership in `tenant_members`
  - [ ] Eliminazione controllata tramite `ISubjectService.DeleteSubjectAsync`
  - [ ] Pulizia access request orfane
  - [ ] Logging informativo sul numero di subject eliminati

- [ ] **2. Registrazione DI**
  - [ ] Registrare `services.AddHostedService<EnrolmentPruningService>()` in `ServiceRegistrationExtensions.cs`

- [ ] **3. Test**
  - [ ] Unit test `EnrolmentPruningServiceTests`:
    - [ ] Subject abbandonato > 24h → rimosso
    - [ ] Subject recente < 24h → preservato
    - [ ] Subject attivo o con credenziali → preservato
