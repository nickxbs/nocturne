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

async function parseJson<T>(res: Response): Promise<T> {
  const json: unknown = await res.json();
  // eslint-disable-next-line @typescript-eslint/consistent-type-assertions -- HTTP boundary deserialization
  return json as T;
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
   * Builds the common headers for instance-key service-to-service communication,
   * optionally scoping to a tenant subdomain via the Host header.
   */
  private getHeaders(tenantSlug?: string): Record<string, string> {
    const headers: Record<string, string> = {
      Accept: "application/json",
      "Content-Type": "application/json",
    };

    if (this.instanceKeyHash) {
      headers["X-Instance-Key"] = this.instanceKeyHash;
      headers["X-Instance-Service"] = this.config.serviceName;
    }

    if (tenantSlug) {
      headers["Host"] = `${tenantSlug}.${this.config.baseDomain}`;
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
          headers: this.getHeaders(),
          signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
        });

        if (res.ok) {
          const tenants = await parseJson<TenantSummary[]>(res);
          const filtered = tenants.filter((t) => {
            if (!t.isActive) return false;
            const hasNightscout = t.connectors?.some(
              (c) => c.connectorName?.toLowerCase() === "nightscout"
            );
            return !hasNightscout;
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
          err instanceof Error ? err.message : String(err)
        );
      }
    }

    // 2. Production fallback via PlatformAdmin endpoints
    const res = await fetch(`${this.config.apiUrl}/api/v4/admin/tenants`, {
      headers: this.getHeaders(),
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
              headers: this.getHeaders(),
              signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
            }
          );
          if (connRes.ok) {
            const data = await parseJson<{ connectors?: Array<{ connectorName: string }> }>(connRes);
            const hasNightscout = data.connectors?.some(
              (c) => c.connectorName?.toLowerCase() === "nightscout"
            );
            return hasNightscout ? null : tenant;
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
        headers: this.getHeaders(tenantSlug),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });

      if (!res.ok) {
        if (res.status === 404) return null;
        console.warn(`[${tenantSlug}] Error fetching CGMSIM settings: ${res.status} ${res.statusText}`);
        return null;
      }

      return await parseJson<CgmSimSettings>(res);
    } catch (err) {
      console.error(`[${tenantSlug}] Failed to fetch CGMSIM settings:`, err);
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
          headers: this.getHeaders(tenantSlug),
          signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
        }
      );

      if (!res.ok) {
        console.warn(`[${tenantSlug}] Error fetching treatments: ${res.status}`);
        return [];
      }

      return await parseJson<Treatment[]>(res);
    } catch (err) {
      console.error(`[${tenantSlug}] Failed to fetch treatments:`, err);
      return [];
    }
  }

  /**
   * Fetches the current / latest blood glucose entry for this tenant.
   */
  async getCurrentEntry(tenantSlug: string): Promise<CurrentEntry | null> {
    try {
      const res = await fetch(`${this.config.apiUrl}/api/v1/entries/current`, {
        headers: this.getHeaders(tenantSlug),
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
      console.error(`[${tenantSlug}] Failed to fetch current entry:`, err);
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
        headers: this.getHeaders(tenantSlug),
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
      console.error(`[${tenantSlug}] Failed to post simulated entry:`, err);
      return false;
    }
  }
}
