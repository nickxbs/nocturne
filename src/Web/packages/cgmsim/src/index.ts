import { loadConfig } from "./config.js";
import { NocturneApiClient, formatError } from "./api-client.js";
import { CgmSimulatorEngine } from "./simulator.js";

async function main() {
  const config = loadConfig();
  console.log("=================================================");
  console.log("       Nocturne CGM Simulation Service           ");
  console.log("=================================================");
  console.log(`API URL:           ${config.apiUrl}`);
  console.log(`Base Domain:       ${config.baseDomain}`);
  console.log(`Service Name:      ${config.serviceName}`);
  console.log(`Instance Key:      ${config.instanceKey ? "[Configured]" : "[Not set / Dev mode]"}`);
  console.log(`Scheduler:         ${config.enabled ? "ENABLED" : "DISABLED (idle mode)"}`);
  console.log(`Interval:          ${config.intervalMs / 1000} seconds`);
  console.log(`Verbose Logs:      ${config.debug ? "ENABLED (SIMULATION_DEBUG=true)" : "DISABLED"}`);
  console.log("=================================================\n");

  if (!config.enabled) {
    console.log("[Simulator] Simulation scheduler is DISABLED via SIMULATION_ENABLED=false.");
    console.log("[Simulator] Service standing by in idle mode (will not query tenants or write entries).");

    const idleTimer = setInterval(() => {}, 1000 * 60 * 60);

    function shutdownIdle() {
      console.log("\n[Simulator] Stopping idle simulation service...");
      clearInterval(idleTimer);
      process.exit(0);
    }

    process.on("SIGINT", shutdownIdle);
    process.on("SIGTERM", shutdownIdle);
    return;
  }

  const apiClient = new NocturneApiClient(config);
  const engine = new CgmSimulatorEngine();

  let isRunning = false;

  async function runSimulationCycle() {
    if (isRunning) {
      console.log("[Simulator] Previous simulation tick still running, skipping...");
      return;
    }

    isRunning = true;
    const now = new Date();
    console.log(`\n--- [${now.toISOString()}] Starting Simulation Tick ---`);

    try {
      // 1. Single-call discovery: get all active tenants that DO NOT have Nightscout integration
      const tenants = await apiClient.getActiveTenantsWithoutNightscout();

      if (tenants.length === 0) {
        console.log("[Simulator] No active non-Nightscout tenants found to simulate.");
        return;
      }

      // 2. Simulate each tenant
      for (const tenant of tenants) {
        try {
          // Fetch virtual patient parameters
          const settings = await apiClient.getCgmSimSettings(tenant.slug);
          const params = settings?.parameters;
          const subject = settings?.subject;

          // Fetch treatments within DIA / basal window (45h covers GLA/DET/TOU/DEG long-acting curves)
          const diaHours = Math.max(params?.dia || 4.0, 45.0);
          const diaWindowStart = new Date(now.getTime() - diaHours * 60 * 60 * 1000).toISOString();
          const treatments = await apiClient.getRecentTreatments(tenant.slug, diaWindowStart);
          const basalProfile = await apiClient.getBasalProfile(tenant.slug);

          // Fetch current blood glucose reading
          const currentEntry = await apiClient.getCurrentEntry(tenant.slug);

          if (currentEntry) {
            const entryTs =
              currentEntry.mills ||
              currentEntry.date ||
              (currentEntry.dateString ? new Date(currentEntry.dateString).getTime() : 0);
            const isRecent = entryTs > 0 && now.getTime() - entryTs < 15 * 60 * 1000;
            const isExternalDevice = currentEntry.device && currentEntry.device !== "cgmsim-node";

            if (isRecent && isExternalDevice) {
              console.log(
                `  [${tenant.slug}] Skipping simulation: tenant is receiving live data from external device '${currentEntry.device}'.`
              );
              continue;
            }
          }

          // === LOG INPUTS ===
          console.log(`\n  ┌── [${tenant.slug}] SIMULATOR INPUTS ──────────────────────────────────────────`);
          console.log(`  │ Target Time:       ${now.toISOString()} (mills: ${now.getTime()})`);
          console.log(
            `  │ Virtual Patient:   ISF=${params?.isf ?? "50 (default)"} mg/dL, CR=${params?.cr ?? "10 (default)"} g/U, ` +
              `DIA=${params?.dia ?? "4.0 (default)"}h, Peak=${params?.insulinPeak ?? "75 (default)"}m, CarbsAbs=${params?.carbsAbsTime ?? "180 (default)"}m | ` +
              `Weight=${subject?.weight ?? "70 (default)"}kg, Age=${subject?.age ?? "30 (default)"}, Gender=${subject?.sex ?? "male"}`
          );

          if (currentEntry?.sgv) {
            const entryTs =
              currentEntry.mills ||
              currentEntry.date ||
              (currentEntry.dateString ? new Date(currentEntry.dateString).getTime() : 0);
            const ageMin = entryTs > 0 ? Math.round((now.getTime() - entryTs) / 60000) : "?";
            console.log(
              `  │ Previous SGV:      ${currentEntry.sgv} mg/dL (${currentEntry.direction ?? "None"}) | ` +
                `Recorded: ${currentEntry.dateString ?? (entryTs > 0 ? new Date(entryTs).toISOString() : "unknown")} (${ageMin} min ago) | Device: ${currentEntry.device ?? "unknown"}`
            );
          } else {
            console.log(`  │ Previous SGV:      [None / Initial bootstrap] (will seed baseline glucose)`);
          }

          if (basalProfile.length > 0) {
            const scheduleSummary = basalProfile
              .map((b) => `${String(b.hour).padStart(2, "0")}:00=${b.rate}U/h`)
              .join(", ");
            console.log(
              `  │ Pump / Basal:      ENABLED (Pump mode, ${basalProfile.length} segments: ${scheduleSummary})`
            );
          } else {
            const basalNotes = treatments
              .map((t) => t.notes?.trim())
              .filter((n): n is string => Boolean(n));
            const mdiDetail =
              basalNotes.length > 0
                ? `MDI active basal notes: [${basalNotes.join(", ")}]`
                : "MDI mode (no basal schedule or notes)";
            console.log(`  │ Pump / Basal:      DISABLED (${mdiDetail})`);
          }

          console.log(`  │ Treatments (${treatments.length} in ${diaHours}h window):`);
          if (treatments.length === 0) {
            console.log(`  │   (none)`);
          } else {
            for (const t of treatments) {
              const ts = t.mills || (t.created_at ? new Date(t.created_at).getTime() : 0);
              const minAgo = ts > 0 ? Math.round((now.getTime() - ts) / 60000) : "?";
              const parts: string[] = [];
              if (t.insulin && t.insulin > 0) parts.push(`insulin=${t.insulin}U`);
              if (t.carbs && t.carbs > 0) parts.push(`carbs=${t.carbs}g`);
              if (t.notes) parts.push(`notes="${t.notes.trim()}"`);
              if (t.eventType) parts.push(`type="${t.eventType}"`);
              console.log(
                `  │   • [${t.created_at || (ts > 0 ? new Date(ts).toISOString() : "no-time")}] (${minAgo}m ago): ${parts.join(", ")}`
              );
            }
          }

          // Compute next simulated reading using @lsandini/cgmsim-lib
          const customLogger = config.debug
            ? {
                debug: (msg: string, ...args: unknown[]) => console.log(`  │   [lib:debug] ${msg}`, ...args),
                info: (msg: string, ...args: unknown[]) => console.log(`  │   [lib:info] ${msg}`, ...args),
                warn: (msg: string, ...args: unknown[]) => console.warn(`  │   [lib:warn] ${msg}`, ...args),
                error: (msg: string, ...args: unknown[]) => console.error(`  │   [lib:error] ${msg}`, ...args),
              }
            : undefined;

          const { entry: nextEntry, result: simResult, pumpEnabled } = engine.computeNextReading(
            now,
            settings?.subject,
            settings?.parameters,
            treatments,
            currentEntry,
            basalProfile,
            { debug: config.debug, logger: customLogger }
          );

          // === LOG OUTPUTS ===
          console.log(`  ├── [${tenant.slug}] SIMULATOR OUTPUTS ─────────────────────────────────────────`);
          const rawSgv = Number.isFinite(simResult.sgv) ? simResult.sgv.toFixed(2) : String(simResult.sgv);
          const deltaStr =
            currentEntry?.sgv != null
              ? `${nextEntry.sgv - currentEntry.sgv >= 0 ? "+" : ""}${nextEntry.sgv - currentEntry.sgv} mg/dL`
              : "initial";
          console.log(
            `  │ Calculated SGV:    ${nextEntry.sgv} mg/dL (raw: ${rawSgv}, trend: ${nextEntry.direction}, delta: ${deltaStr})`
          );
          console.log(
            `  │ Metabolic State:   IOB=${simResult.iob.toFixed(2)}U | COB=${simResult.cob.toFixed(1)}g | ` +
              `Activity=${simResult.activity.toFixed(4)} | Pump=${pumpEnabled ? "ON" : "OFF"} | DeltaMinutes=${simResult.deltaMinutes.toFixed(1)}m`
          );
          console.log(`  │ Entry Payload:     ${JSON.stringify(nextEntry)}`);

          // Send reading to Nocturne Web API
          const success = await apiClient.postSimulatedEntry(tenant.slug, nextEntry);
          console.log(`  │ API POST Result:   ${success ? "SUCCESS (HTTP 200 OK)" : "FAILED (check API response above)"}`);
          console.log(`  └────────────────────────────────────────────────────────────────────────────\n`);
        } catch (tenantErr) {
          console.error(`  ├── [${tenant.slug}] SIMULATOR ERROR ──────────────────────────────────────────`);
          console.error(`  │ Simulation step failed:`, formatError(tenantErr));
          if (tenantErr instanceof Error && tenantErr.stack) {
            console.error(`  │ Stack trace:\n${tenantErr.stack.split("\n").map((l) => "  │   " + l).join("\n")}`);
          }
          console.error(`  └────────────────────────────────────────────────────────────────────────────\n`);
        }
      }
    } catch (err) {
      console.error("[Simulator] Simulation tick failed:", formatError(err));
    } finally {
      isRunning = false;
      console.log(`--- Finished Simulation Tick ---\n`);
    }
  }

  // Wait for Nocturne API to become available before starting ticks
  console.log(`[Simulator] Connecting to Nocturne API at ${config.apiUrl}...`);
  let apiReady = false;
  for (let attempt = 1; attempt <= 30; attempt++) {
    apiReady = await apiClient.checkHealth();
    if (apiReady) {
      console.log(`[Simulator] Nocturne API is ready and healthy!`);
      break;
    }
    console.log(`[Simulator] Waiting for Nocturne API to become ready (attempt ${attempt}/30, retrying in 3s)...`);
    await new Promise((resolve) => setTimeout(resolve, 3000));
  }

  if (!apiReady) {
    console.warn(`[Simulator] Nocturne API did not respond to /alive after 90s, attempting first tick anyway...`);
  }

  // Run first cycle
  await runSimulationCycle();

  // Schedule recurring interval
  const timer = setInterval(runSimulationCycle, config.intervalMs);

  function shutdown() {
    console.log("\n[Simulator] Stopping simulation service...");
    clearInterval(timer);
    process.exit(0);
  }

  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}

main().catch((err) => {
  console.error("Fatal simulator crash:", err);
  process.exit(1);
});
