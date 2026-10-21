# Implementation Plans: Passkey Signup and Personal Tenant Provisioning

This directory contains the detailed implementation plans for self-service passkey registration and automatic personal tenant provisioning in Nocturne.

## Progress Tracking

| PR / Phase | Document | Status | Description |
|---|---|:---:|---|
| **Verifications** | [06-technical-verifications.md](file:///C:/git/nocturne/plans/06-technical-verifications.md) | `DONE` | Findings on the 6 preliminary technical points verified against active code |
| **PR 1** | [01-phase-0-preparation.md](file:///C:/git/nocturne/plans/01-phase-0-preparation.md) | `DONE` | Fix `getAuthStatus` on tenantless hosts & extract `IUsernameValidator` |
| **PR 2** | [02-phases-1-and-2-backend.md](file:///C:/git/nocturne/plans/02-phases-1-and-2-backend.md) | `DONE` | Operator configuration, `SignupController`, pipeline & guard tests |
| **PR 3** | [03-phase-3-frontend.md](file:///C:/git/nocturne/plans/03-phase-3-frontend.md) | `DONE` | NSwag client, new `/signup/` route, 3-step wizard, integration & e2e tests |
| **PR 4** | [04-phase-2.4-approval.md](file:///C:/git/nocturne/plans/04-phase-2.4-approval.md) | `TODO` | Tenant approval by platform admin (`/activate`), in-app notifications & admin UI |
| **PR 5** | [05-phase-2.5-account-cleanup.md](file:///C:/git/nocturne/plans/05-phase-2.5-account-cleanup.md) | `TODO` | Background service for periodic pruning of abandoned enrolling accounts |

Master plan: [00-master-plan.md](file:///C:/git/nocturne/plans/00-master-plan.md) (`IN PROGRESS`)
