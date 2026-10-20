# Piani di Implementazione: Registrazione con Passkey e Tenant Personale

Questa cartella contiene i piani dettagliati per l'implementazione della registrazione self-service con passkey e creazione automatica del tenant personale in Nocturne.

## Stato di Avanzamento

| PR / Fase | Documento | Stato | Descrizione |
|---|---|:---:|---|
| **Verifiche** | [06-verifiche-tecniche.md](file:///C:/git/nocturne/plans/06-verifiche-tecniche.md) | `DONE` | Riscontro sui 6 punti tecnici verificati nel codice |
| **PR 1** | [01-fase-0-preparazione.md](file:///C:/git/nocturne/plans/01-fase-0-preparazione.md) | `TODO` | Correzione `getAuthStatus` su apex ed estrazione `IUsernameValidator` |
| **PR 2** | [02-fasi-1-e-2-backend.md](file:///C:/git/nocturne/plans/02-fasi-1-e-2-backend.md) | `TODO` | Configurazione operatore, controller `SignupController`, pipeline e test |
| **PR 3** | [03-fase-3-frontend.md](file:///C:/git/nocturne/plans/03-fase-3-frontend.md) | `TODO` | Client NSwag, nuova route `/signup/`, wizard a 3 step, test integrazione ed e2e |
| **PR 4** | [04-fase-2.4-approvazione.md](file:///C:/git/nocturne/plans/04-fase-2.4-approvazione.md) | `TODO` | Approvazione tenant da platform admin (`/activate`), notifiche in-app e badge UI |
| **PR 5** | [05-fase-2.5-pulizia-account.md](file:///C:/git/nocturne/plans/05-fase-2.5-pulizia-account.md) | `TODO` | Background service per pulizia periodica degli account abbandonati |

Piano generale di riferimento: [00-piano-completo.md](file:///C:/git/nocturne/plans/00-piano-completo.md) (`TODO`)
