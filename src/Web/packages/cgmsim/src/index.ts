import { loadConfig } from "./config.js";
import { NocturneApiClient } from "./api-client.js";
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

          // Fetch treatments within DIA window
          const diaHours = params?.dia || 4.0;
          const diaWindowStart = new Date(now.getTime() - diaHours * 60 * 60 * 1000).toISOString();
          const treatments = await apiClient.getRecentTreatments(tenant.slug, diaWindowStart);

          // Fetch current blood glucose reading
          const currentEntry = await apiClient.getCurrentEntry(tenant.slug);

          // Compute next simulated reading using @lsandini/cgmsim-lib
          const { entry: nextEntry, result: simResult } = engine.computeNextReading(
            now,
            settings?.subject,
            settings?.parameters,
            treatments,
            currentEntry
          );

          // Send reading to Nocturne Web API
          const success = await apiClient.postSimulatedEntry(tenant.slug, nextEntry);

          if (success) {
            const prevSgv = currentEntry?.sgv ?? "init";
            console.log(
              `  [${tenant.slug}] SGV: ${prevSgv} -> ${nextEntry.sgv} mg/dL ` +
                `(${nextEntry.direction}) | IOB: ${simResult.iob.toFixed(2)}U | ` +
                `COB: ${simResult.cob.toFixed(1)}g | Treatments: ${treatments.length}`
            );
          }
        } catch (tenantErr) {
          console.error(
            `  [${tenant.slug}] Error running simulation step:`,
            tenantErr instanceof Error ? tenantErr.message : tenantErr
          );
        }
      }
    } catch (err) {
      console.error("[Simulator] Simulation tick failed:", err instanceof Error ? err.message : err);
    } finally {
      isRunning = false;
      console.log(`--- Finished Simulation Tick ---\n`);
    }
  }

  // Run immediately on start
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
