# @nocturne/cgmsim

Autonomous Node.js / TypeScript service that generates realistic simulated Continuous Glucose Monitoring (CGM) readings for Nocturne tenants using `@lsandini/cgmsim-lib`.

## Features

- **Physiological Kinetics Engine**: Powered by `@lsandini/cgmsim-lib` (`simulateGlucose`), modeling DIA, insulin peak, carb absorption curves, IOB, COB, and metabolic activity.
- **Multi-Tenant Autonomous Loop**: Automatically discovers all active tenants that **do not** have Nightscout configured as an integration.
- **Single-Call Discovery**: In local/dev environments, queries `GET /api/v4/dev-only/admin/tenants` to inspect all tenants and their active connectors in a single HTTP request.
- **Service-to-Service Security**: Uses `X-Instance-Key` (SHA-256 digest) and `X-Instance-Service` (`platform_admin` level) to access cross-tenant settings and post entries.
- **Live Realtime Propagation**: Writes new entries to `POST /api/v1/entries`, which immediately triggers SignalR / Socket.IO broadcast updates on the web dashboard and evaluates alerts.

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `NOCTURNE_API_URL` | `http://localhost:1610` | Nocturne API base address |
| `NOCTURNE_INSTANCE_KEY` (or `INSTANCE_KEY`) | `""` | Raw instance key (minimum 32 characters) |
| `NOCTURNE_BASE_DOMAIN` | `nocturne.localhost` | Domain suffix used for `Host: <slug>.<baseDomain>` |
| `SIMULATION_INTERVAL_MS` | `300000` (5 min) | Tick interval (e.g. `60000` for 1-minute test ticks) |
| `INSTANCE_SERVICE_NAME` | `cgmsim` | Value for `X-Instance-Service` header |
| `SIMULATION_TENANT` | `""` | Optional: limit simulation to a single tenant slug |
| `ALLOW_DEV_FALLBACK` | `true` | Allows using dev-only discovery when available |

## Running the Service

```bash
# In src/Web
pnpm --filter @nocturne/cgmsim dev

# Run unit tests
pnpm --filter @nocturne/cgmsim test

# Or build and run
pnpm --filter @nocturne/cgmsim build
pnpm --filter @nocturne/cgmsim start
```
