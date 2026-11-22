export interface SimulatorConfig {
  enabled: boolean;
  debug: boolean;
  apiUrl: string;
  instanceKey: string;
  baseDomain: string;
  intervalMs: number;
  serviceName: string;
  targetTenantSlug?: string;
  allowDevFallback: boolean;
  noiseAmplitude: number;
  noiseOctaves: number;
  noisePersistence: number;
}

export function loadConfig(): SimulatorConfig {
  const enabledEnv = process.env.SIMULATION_ENABLED ?? process.env.CGMSIM_ENABLED ?? process.env.SIMULATOR_ENABLED;
  const enabled = enabledEnv !== "false" && enabledEnv !== "0";

  const debugEnv = process.env.SIMULATION_DEBUG ?? process.env.DEBUG ?? process.env.CGMSIM_DEBUG;
  const debug = debugEnv === "true" || debugEnv === "1" || debugEnv === "verbose";

  const apiUrl = (process.env.NOCTURNE_API_URL || "http://localhost:1610").replace(/\/+$/, "");
  const instanceKey = process.env.NOCTURNE_INSTANCE_KEY || process.env.INSTANCE_KEY || "";
  const baseDomainRaw =
    process.env.NOCTURNE_BASE_DOMAIN || process.env.BASE_DOMAIN || "nocturne.localhost";
  const baseDomain = baseDomainRaw.split(":")[0];
  const intervalMs = parseInt(process.env.SIMULATION_INTERVAL_MS || "300000", 10);
  const serviceName = process.env.INSTANCE_SERVICE_NAME || "cgmsim";
  const targetTenantSlug = process.env.SIMULATION_TENANT?.trim();
  const allowDevFallback = process.env.ALLOW_DEV_FALLBACK !== "false";

  const noiseAmpEnv = process.env.NOISE_AMPLITUDE ?? process.env.SIMULATION_NOISE_AMPLITUDE;
  const noiseAmplitude = noiseAmpEnv ? parseFloat(noiseAmpEnv) : 1.0;

  const noiseOctEnv = process.env.NOISE_OCTAVES ?? process.env.SIMULATION_NOISE_OCTAVES;
  const noiseOctaves = noiseOctEnv ? parseInt(noiseOctEnv, 10) : 4;

  const noisePersEnv = process.env.NOISE_PERSISTENCE ?? process.env.SIMULATION_NOISE_PERSISTENCE;
  const noisePersistence = noisePersEnv ? parseFloat(noisePersEnv) : 0.5;

  return {
    enabled,
    debug,
    apiUrl,
    instanceKey,
    baseDomain,
    intervalMs: isNaN(intervalMs) || intervalMs < 1000 ? 300000 : intervalMs,
    serviceName,
    targetTenantSlug: targetTenantSlug || undefined,
    allowDevFallback,
    noiseAmplitude: isNaN(noiseAmplitude) ? 1.0 : noiseAmplitude,
    noiseOctaves: isNaN(noiseOctaves) || noiseOctaves < 1 ? 4 : noiseOctaves,
    noisePersistence: isNaN(noisePersistence) ? 0.5 : noisePersistence,
  };
}
