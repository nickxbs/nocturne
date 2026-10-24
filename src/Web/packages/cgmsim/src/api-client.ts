import { createHash } from "node:crypto";
import type { SimulatorConfig } from "./config.js";
import type {
  CgmSimSettings,
  CurrentEntry,
  SimulatedEntry,
  TenantSummary,
  Treatment,
} from "./types.js";

const REQUEST_TIMEOUT_MS = 10_000;

export function formatError(err: unknown): string {
  if (err instanceof Error) {
    if ("cause" in err && err.cause) {
      const cause = err.cause;
      const causeMsg = cause instanceof Error ? cause.message : String(cause);
      return `${err.message} (cause: ${causeMsg})`;
    }
    return err.message;
  }
  return String(err);
}

async function parseJson<T>(res: Response): Promise<T> {
  const json: unknown = await res.json();
  // eslint-disable-next-line @typescript-eslint/consistent-type-assertions -- HTTP boundary deserialization
  return json as T;
}

/**
 * Detects whether a connector configuration represents Nightscout.
 * Handles both dev-only DTOs ('name') and platform-admin DTOs ('connectorName').
 */
export function isNightscoutConnector(c: ConnectorSummary): boolean {
  const name = (c.name || c.connectorName || "").toLowerCase().trim();
  return (
    name === "nightscout" ||
    name === "nightscout-connector" ||
    name.includes("nightscout")
  );
}

export class NocturneApiClient {
  private readonly config: SimulatorConfig;
  private readonly instanceKeyHash: string | null;

  constructor(config: SimulatorConfig) {
    this.config = config;
    this.instanceKeyHash = config.instanceKey
      ? createHash("sha256").update(config.instanceKey).digest("hex").toLowerCase()
      : null;
  }

  /**
   * Checks if the Nocturne API is reachable and responding.
   */
  async checkHealth(): Promise<boolean> {
    try {
      const res = await fetch(`${this.config.apiUrl}/alive`, {
        signal: AbortSignal.timeout(3000),
      });
      return res.ok;
    } catch {
      return false;
    }
  }

  private readonly tokenCache = new Map<string, { token: string; expiresAt: number }>();

  /**
   * Acquires a bearer token for a tenant via dev-only login endpoint when instance key is not set.
   */
  private async getDevAccessToken(tenantSlug: string): Promise<string | null> {
    const cached = this.tokenCache.get(tenantSlug);
    const now = Date.now();
    if (cached && cached.expiresAt > now + 60_000) {
      return cached.token;
    }

    try {
      const res = await fetch(`${this.config.apiUrl}/api/v4/dev-only/auth/login`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ tenant: tenantSlug, username: null }),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });

      if (res.ok) {
        const data = await parseJson<{ accessToken: string; expiresInSeconds?: number }>(res);
        if (data?.accessToken) {
          const expiresInMs = (data.expiresInSeconds || 3600) * 1000;
          this.tokenCache.set(tenantSlug, {
            token: data.accessToken,
            expiresAt: now + expiresInMs,
          });
          return data.accessToken;
        }
      }
    } catch {
      // Dev login endpoint unavailable, proceed without dev token
    }

    return null;
  }

  /**
   * Builds headers for API calls. Sets X-Forwarded-Host to ensure the tenant
   * is resolved by TenantResolutionMiddleware even when called directly against localhost:1610.
   */
  private async getHeaders(tenantSlug?: string): Promise<Record<string, string>> {
    const headers: Record<string, string> = {
      Accept: "application/json",
      "Content-Type": "application/json",
    };

    if (this.instanceKeyHash) {
      headers["X-Instance-Key"] = this.instanceKeyHash;
      headers["X-Instance-Service"] = this.config.serviceName;
    }

    if (tenantSlug) {
      const tenantHost = `${tenantSlug}.${this.config.baseDomain}`;
      headers["Host"] = tenantHost;
      headers["X-Forwarded-Host"] = tenantHost;
      headers["X-Forwarded-Proto"] = "https";

      if (!this.instanceKeyHash && this.config.allowDevFallback) {
        const token = await this.getDevAccessToken(tenantSlug);
        if (token) {
          headers["Authorization"] = `Bearer ${token}`;
        }
      }
    }

    return headers;
  }

  /**
   * Discovers all active tenants that DO NOT have Nightscout configured as an integration.
   *
   * In local/development environments, this is completed in a SINGLE call via
   * `GET /api/v4/dev-only/admin/tenants`, which embeds connector health for every tenant.
   * In production, falls back to querying `GET /api/v4/admin/tenants` and inspecting connectors.
   */
  async getActiveTenantsWithoutNightscout(): Promise<TenantSummary[]> {
    if (this.config.targetTenantSlug) {
      console.log(`[Simulator] Targeted single tenant override: '${this.config.targetTenantSlug}'`);
      return [
        {
          id: "manual-override",
          slug: this.config.targetTenantSlug,
          displayName: this.config.targetTenantSlug,
          isActive: true,
        },
      ];
    }

    // 1. Single-call attempt via dev-only admin endpoint (fastest & includes connector list)
    if (this.config.allowDevFallback) {
      try {
        const res = await fetch(`${this.config.apiUrl}/api/v4/dev-only/admin/tenants`, {
          headers: await this.getHeaders(),
          signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
        });

        if (res.ok) {
          const tenants = await parseJson<TenantSummary[]>(res);
          const filtered = tenants.filter((t) => {
            if (!t.isActive) {
              console.log(`[Simulator] Tenant '${t.slug}' is inactive, skipping.`);
              return false;
            }

            const nsConnector = t.connectors?.find(isNightscoutConnector);
            if (nsConnector) {
              const syncDetail = nsConnector.lastSuccessfulSync
                ? `last sync: ${nsConnector.lastSuccessfulSync}`
                : "configured (no successful sync yet)";
              const healthDetail =
                nsConnector.isHealthy === false
                  ? ` [unhealthy: ${nsConnector.lastError || nsConnector.lastErrorMessage || "error"}]`
                  : "";
              console.log(
                `[Simulator] Skipping tenant '${t.slug}': Nightscout integration active (${syncDetail}${healthDetail}).`
              );
              return false;
            }

            if (t.connectors && t.connectors.length > 0) {
              const connNames = t.connectors
                .map((c) => c.name || c.connectorName)
                .filter(Boolean)
                .join(", ");
              console.log(
                `[Simulator] Tenant '${t.slug}' will be simulated (connectors configured: [${connNames}]).`
              );
            }

            return true;
          });

          console.log(
            `[Simulator] Discovered ${tenants.length} tenant(s) in a single call. ` +
              `${filtered.length} active tenant(s) do not have Nightscout integration.`
          );
          return filtered;
        }
      } catch (err) {
        console.warn(
          `[Simulator] Dev-only tenants endpoint not reachable, falling back to platform-admin:`,
          formatError(err)
        );
      }
    }

    // 2. Production fallback via PlatformAdmin endpoints
    const res = await fetch(`${this.config.apiUrl}/api/v4/admin/tenants`, {
      headers: await this.getHeaders(),
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });

    if (!res.ok) {
      throw new Error(
        `Failed to fetch platform tenants (${res.status} ${res.statusText}). ` +
          `Verify X-Instance-Key or NOCTURNE_API_URL.`
      );
    }

    const allTenants = await parseJson<TenantSummary[]>(res);
    const activeTenants = allTenants.filter((t) => t.isActive);

    // Parallel connector check for each active tenant
    const results = await Promise.all(
      activeTenants.map(async (tenant) => {
        try {
          const connRes = await fetch(
            `${this.config.apiUrl}/api/v4/admin/connectors/${tenant.id}`,
            {
              headers: await this.getHeaders(),
              signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
            }
          );
          if (connRes.ok) {
            const data = await parseJson<{ connectors?: ConnectorSummary[] }>(connRes);
            const nsConnector = data.connectors?.find(isNightscoutConnector);
            if (nsConnector) {
              const syncDetail = nsConnector.lastSuccessfulSync
                ? `last sync: ${nsConnector.lastSuccessfulSync}`
                : "configured";
              console.log(
                `[Simulator] Skipping tenant '${tenant.slug}': Nightscout integration active (${syncDetail}).`
              );
              return null;
            }
            return tenant;
          }
        } catch {
          // If connectors cannot be queried, keep tenant as candidate
        }
        return tenant;
      })
    );

    const filtered = results.filter((t): t is TenantSummary => t !== null);
    console.log(
      `[Simulator] Platform admin discovered ${activeTenants.length} active tenant(s), ` +
        `${filtered.length} without Nightscout integration.`
    );
    return filtered;
  }

  /**
   * Fetches CGMSIM virtual patient settings for a tenant.
   */
  async getCgmSimSettings(tenantSlug: string): Promise<CgmSimSettings | null> {
    try {
      const res = await fetch(`${this.config.apiUrl}/api/v4/cgmsim/settings`, {
        headers: await this.getHeaders(tenantSlug),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });

      if (!res.ok) {
        if (res.status === 404) return null;
        console.warn(`[${tenantSlug}] Error fetching CGMSIM settings: ${res.status} ${res.statusText}`);
        return null;
      }

      return await parseJson<CgmSimSettings>(res);
    } catch (err) {
      console.error(`[${tenantSlug}] Failed to fetch CGMSIM settings:`, formatError(err));
      return null;
    }
  }

  /**
   * Fetches recent treatments (boluses & carbs) within the DIA window.
   */
  async getRecentTreatments(tenantSlug: string, sinceIso: string): Promise<Treatment[]> {
    try {
      const query = encodeURIComponent(sinceIso);
      const res = await fetch(
        `${this.config.apiUrl}/api/v1/treatments?find[created_at][$gte]=${query}&count=50`,
        {
          headers: await this.getHeaders(tenantSlug),
          signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
        }
      );

      if (!res.ok) {
        console.warn(`[${tenantSlug}] Error fetching treatments: ${res.status}`);
        return [];
      }

      return await parseJson<Treatment[]>(res);
    } catch (err) {
      console.error(`[${tenantSlug}] Failed to fetch treatments:`, formatError(err));
      return [];
    }
  }

  /**
   * Fetches the current / latest blood glucose entry for this tenant.
   */
  async getCurrentEntry(tenantSlug: string): Promise<CurrentEntry | null> {
    try {
      const res = await fetch(`${this.config.apiUrl}/api/v1/entries/current`, {
        headers: await this.getHeaders(tenantSlug),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });

      if (!res.ok) {
        return null;
      }

      const json = await parseJson<unknown>(res);
      if (Array.isArray(json)) {
        if (json.length === 0) return null;
        // eslint-disable-next-line @typescript-eslint/consistent-type-assertions -- HTTP boundary deserialization
        return json[0] as CurrentEntry;
      }
      // eslint-disable-next-line @typescript-eslint/consistent-type-assertions -- HTTP boundary deserialization
      return json as CurrentEntry;
    } catch (err) {
      console.error(`[${tenantSlug}] Failed to fetch current entry:`, formatError(err));
      return null;
    }
  }

  /**
   * Writes a newly simulated glucose reading to Nocturne.
   * This immediately triggers SignalR / Socket.IO broadcasts and updates the UI live.
   */
  async postSimulatedEntry(tenantSlug: string, entry: SimulatedEntry): Promise<boolean> {
    try {
      const res = await fetch(`${this.config.apiUrl}/api/v1/entries`, {
        method: "POST",
        headers: await this.getHeaders(tenantSlug),
        body: JSON.stringify([entry]),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });

      if (!res.ok) {
        const errorText = await res.text();
        console.error(`[${tenantSlug}] Failed to post entry (${res.status}): ${errorText}`);
        return false;
      }

      return true;
    } catch (err) {
      console.error(`[${tenantSlug}] Failed to post simulated entry:`, formatError(err));
      return false;
    }
  }
}
