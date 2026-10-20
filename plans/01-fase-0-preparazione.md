# PR 1: Fase 0 — Preparazione

> **Stato**: `TODO`

## Obiettivo
Intervento preliminare, piccolo e privo di cambi di comportamento o di breaking change, mirato a:
1. Correggere la chiamata a `getAuthStatus` nella pagina di login quando eseguita su un host senza tenant (apex).
2. Estrarre la logica di validazione dello username da `SetupController` in un servizio riutilizzabile `IUsernameValidator`.

---

## Task List

- [ ] **1. Correzione `getAuthStatus` su host tenantless**
  - [ ] Identificare `authStatusQuery` in `src/Web/packages/app/src/routes/(unauthenticated)/auth/login/+page.svelte`
  - [ ] Condizionare la chiamata a `!tenantless`
  - [ ] Verificare che `allowAccessRequests` valga `false` su host tenantless
  - [ ] Verificare i test frontend esistenti

- [ ] **2. Estrazione `IUsernameValidator`**
  - [ ] Definire `IUsernameValidator` in `src/API/Nocturne.API/Services/Identity/IUsernameValidator.cs`
  - [ ] Implementare `UsernameValidator.cs` spostando regex, reserved usernames, webhook call e verifica unicità
  - [ ] Registrare `IUsernameValidator` come scoped in `ServiceRegistrationExtensions.cs`
  - [ ] Aggiornare `SetupController.cs` iniettando `IUsernameValidator` e delegando la validazione
  - [ ] Eseguire `SetupControllerTests` per garantire che restino al 100% verdi senza modifiche

---

## Dettagli di Implementazione

### 1. Correzione `getAuthStatus`
In `login/+page.svelte`:
```svelte
// Prima:
const authStatusQuery = getAuthStatus();
const allowAccessRequests = $derived(authStatusQuery.current?.allowAccessRequests ?? false);

// Dopo:
const authStatusQuery = tenantless ? null : getAuthStatus();
const allowAccessRequests = $derived(authStatusQuery?.current?.allowAccessRequests ?? false);
```

### 2. Estrazione `IUsernameValidator`
Interfaccia di servizio:
```csharp
public interface IUsernameValidator
{
    Task<SlugValidationResult> ValidateAsync(
        string username,
        Guid? tenantId = null,
        CancellationToken ct = default);
}
```

---

## Criteri di Accettazione
- [ ] `dotnet test --filter "FullyQualifiedName~SetupControllerTests"` passa senza errori.
- [ ] `cd src/Web/packages/app && pnpm run check` passa senza errori di tipo.
