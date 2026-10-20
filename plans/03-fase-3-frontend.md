# PR 3: Fase 3 — Frontend e Test Integrazione / e2e

> **Stato**: `TODO`

## Obiettivo
Implementare l'interfaccia utente di registrazione per i visitatori dell'apex e i test end-to-end con authenticator virtuale:
- Rigenerazione del client NSwag e remote functions.
- Nuova route `src/routes/(unauthenticated)/signup/`.
- Wizard a 3 step con passkey e handoff.
- Link su `/auth/login`.
- Test di integrazione (`ApiFactory` + Postgres) e test e2e (Playwright).

---

## Task List

- [ ] **1. Rigenerazione Client NSwag**
  - [ ] Eseguire `dotnet build src/API/Nocturne.API/Nocturne.API.csproj -p:GenerateNSwagClient=true`
  - [ ] Verificare la generazione di `signups.generated.remote.ts`

- [ ] **2. Route Frontend `/signup`**
  - [ ] Creare `src/routes/(unauthenticated)/signup/+page.server.ts`:
    - [ ] Controllo host tenantless e `SignupEnabled`
    - [ ] Redirect a `/auth/login` se non ammesso
  - [ ] Creare `src/routes/(unauthenticated)/signup/+page.svelte`:
    - [ ] Step 1: Account (username con `useAvailability` + display name)
    - [ ] Step 2: Spazio (tenant slug con `useAvailability` + display name + preview URL)
    - [ ] Step 3: Passkey (cerimonia `startRegistration` + salvataggio codici recupero `RecoveryCodes.svelte` + redirect `handoffUrl`)
    - [ ] Schermata di attesa approvazione in modalità `pendingApproval`

- [ ] **3. Integrazione Pagina di Login**
  - [ ] Aggiungere link "Crea un account" in `src/routes/(unauthenticated)/auth/login/+page.svelte` (visibile solo se `tenantless && signupEnabled`)

- [ ] **4. Test di Integrazione**
  - [ ] Scrivere `tests/Integration/Nocturne.API.Tests/Auth/SignupIntegrationTests.cs`:
    - [ ] Flusso completo options → complete → login code → handoff → verifica sessione e RLS

- [ ] **5. Test End-to-End con Virtual Authenticator**
  - [ ] Implementare helper CDP in `e2e/src/helpers/webauthn.ts`
  - [ ] Creare specifica `e2e/src/web/signup.e2e-spec.ts`

- [ ] **6. Qualità & Traduzioni**
  - [ ] Aggiungere stringhe di traduzione in `src/Web/locales/`
  - [ ] Eseguire `pnpm run translations:stage`
  - [ ] Eseguire `pnpm run check` e `pnpm run lint:ci`
