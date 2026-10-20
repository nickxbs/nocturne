# Verifiche Tecniche Preliminari nel Codebase di Nocturne

> **Stato**: `DONE` (Tutti i 6 punti sono stati verificati nel codice sorgente attivo)

Questo documento sintetizza le risposte e le evidenze rilevate analizzando il codice di Nocturne prima dell'inizio dello sviluppo, a copertura dei 6 punti dubbi del piano.

---

### 1. Unicità dello username: istanza o tenant?
* **Database**:
  * In `SubjectEntity.cs`, `username` ha solo `[Column("username")]` e **nessun indice univoco**.
  * In `NocturneDbContext.cs` riga 2390–2394, l'indice univoco è su `TenantMemberEntity`:
    ```csharp
    modelBuilder.Entity<TenantMemberEntity>()
        .HasIndex(e => new { e.TenantId, e.Username })
        .HasDatabaseName("ix_tenant_members_tenant_username")
        .IsUnique()
        .HasFilter("username IS NOT NULL");
    ```
  * L'unicità dello username è quindi **per tenant**, non a livello globale di istanza. Due tenant distinti possono avere lo stesso username per due owner diversi.
* **Flussi di autenticazione**:
  * Sia `PasskeyService.GenerateAssertionOptionsAsync` sia `RecoveryCodeService` filtrano sempre prima per `tm.TenantId == tenantId` e poi per `s.Username == username`.
* **Impatto sul signup**:
  * Sul nuovo tenant creato durante il completamento, lo username dell'owner sarà per definizione univoco.
  * Durante la fase transitoria di iscrizione (`signup/options` e `signup/complete`), il record temporaneo `SubjectEntity` (`IsActive = false`) deve essere correlato in modo univoco al tentativo in corso per evitare che due tentativi simultanei con lo stesso username interferiscano tra loro.

---

### 2. Passkey dall'apex al sottodominio
* **Configurazione e derivazione RP ID**:
  * In `ServiceRegistrationExtensions.cs` riga 434:
    ```csharp
    var rpId = baseDomain.Split(':')[0];
    ```
    L'RP ID è ricavato direttamente dal dominio base (es. `nocturne.cgmsim.com` o in locale `nocturne.localhost`).
  * In `PasskeyService.cs` riga 74–80:
    ```csharp
    private bool CanUseConfiguredRpId(string host)
    {
        var rpId = _fido2Config.ServerDomain;
        return _environment.IsDevelopment()
            || string.Equals(host, rpId, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{rpId}", StringComparison.OrdinalIgnoreCase);
    }
    ```
  * In `PasskeyService.cs` riga 100–125 (`AllowOriginFromClientData`), se l'origine del browser è un sottodominio valido dell'RP ID, viene inserita a runtime tra le origini FIDO2 ammesse.
* **Standard WebAuthn**:
  * Una passkey con RP ID impostato sul dominio apex (`nocturne.cgmsim.com`) è ammessa sia per la registrazione sull'apex che per l'asserzione (login) su qualsiasi suo sottodominio (`*.nocturne.cgmsim.com`).
  * **Conclusione**: Funziona nativamente senza modifiche all'infrastruttura passkey.

---

### 3. `ILoginCodeService.IssueAsync` e `AuthAuditActor`
* In `LoginCodeService.cs` riga 67–73, la firma del metodo richiede un `AuthAuditActor actor`.
* In `IAuthAuditService.cs` riga 41–44, la logica di audit afferma:
  > *"Only an unauthenticated request has no caller to name — a login carries AuthContext.Unauthenticated until it succeeds — so it yields null and leaves the subject as its own actor."*
* Poiché il completamento del signup ha appena validato la passkey e creato l'utente, l'attore che riceve e causa l'emissione del login code è l'utente stesso.
* L'argomento corretto da passare è:
  ```csharp
  new AuthAuditActor(subjectId, null)
  ```

---

### 4. Transazione o annullamento: `DeleteSubjectAsync` e pulizia a cascata
* **Relazioni nel database**:
  * `PasskeyCredentialEntity`: foreign key verso `SubjectEntity` con `OnDelete: Cascade`.
  * `TenantMemberEntity`: foreign key verso `SubjectEntity` con `OnDelete: Cascade`.
  * `SubjectRoleEntity`: foreign key verso `SubjectEntity` con `OnDelete: Cascade`.
  * `TenantEntity`: foreign key verso tutte le tabelle tenant-scoped con `OnDelete: Cascade`.
* **Comportamento di rollback**:
  * Invocando `await _subjectService.DeleteSubjectAsync(subjectId)`, Postgres elimina a cascata la riga `subjects`, le sue credenziali passkey e le membership.
  * Se il tenant fosse già stato creato prima dell'errore, invocando `await _tenantService.DeleteAsync(tenantId)` viene rimossa l'intera partizione del tenant.
  * **Conclusione**: Nessun orfano; il rollback manuale tramite `DeleteSubjectAsync` + `DeleteAsync` è atomico dal punto di vista dello stato applicativo.

---

### 5. Authenticator virtuale negli e2e (Playwright)
* In `e2e/src/web/`, i test esistenti usano il bypass del dev login link (`seedTenant()`). Non ci sono al momento spec che simulino WebAuthn nel browser.
* Per testare il wizard di signup e la registrazione passkey, verrà introdotto un helper `e2e/src/helpers/webauthn.ts` che attiva il protocollo CDP di Chromium (`WebAuthn.enable`, `WebAuthn.addVirtualAuthenticator`) con `transport: "internal"`, `hasResidentKey: true`, `hasUserVerification: true`.

---

### 6. Riusabilità di `TenantIdentity.svelte` e `AccountCreation.svelte`
* I componenti `TenantIdentity.svelte` e `AccountCreation.svelte` del wizard di setup iniziale:
  * Importano direttamente da `../setup.remote.ts`.
  * Hanno lo stile grafico specifico dell'onboarding scuro.
  * Nel setup il tenant viene creato *prima* nel database e l'utente *dopo*; nel signup la creazione è contestuale e atomica alla fine.
  * `AccountCreation.svelte` include pulsanti per provider OIDC.
* **Raccomandazione confermata**:
  * Creare componenti dedicati per il wizard di registrazione, condividendo i blocchi isolati già esistenti:
    * `RecoveryCodes.svelte`
    * `useAvailability` da `$lib/forms`
    * `startRegistration` di `@simplewebauthn/browser`
    * `describePasskeyError` e `parseCeremonyOptions` da `$lib/components/auth/passkey-errors`.
