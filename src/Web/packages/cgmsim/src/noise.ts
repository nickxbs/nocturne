import perlin from "perlin-noise";
import type { CgmSimNoisePoint } from "./types.js";

export type NoisePoint = CgmSimNoisePoint;

export const DEFAULT_NOISE_AMPLITUDE = 1.0;
export const DEFAULT_NOISE_OCTAVES = 4;
export const DEFAULT_NOISE_PERSISTENCE = 0.5;

export function createPerlinArray(
  amplitude: number = DEFAULT_NOISE_AMPLITUDE,
  octaves: number = DEFAULT_NOISE_OCTAVES,
  persistence: number = DEFAULT_NOISE_PERSISTENCE,
  startTime: number = Date.now()
): NoisePoint[] {
  console.log("creation of perlin array started");
  const noiseArray: NoisePoint[] = [];

  let noise: number[];

  if (amplitude === 0 || octaves === 0 || persistence === 0) {
    console.log("Parameters are zero. Creating an array of zeros.");
    noise = new Array<number>(2016).fill(0);
  } else {
    console.log(
      "Generating perlin noise with parameters:",
      amplitude,
      octaves,
      persistence
    );
    noise = perlin.generatePerlinNoise(2016, 1, {
      amplitude,
      octaveCount: octaves,
      persistence,
    });
  }

  const map = noise.map((i) => Number(i));
  const totalNoise = map.reduce((a, b) => a + b, 0);
  const meanNoise = totalNoise / map.length - 0.5 || 0;
  console.log("This is the noise mean, it should be almost 0:", meanNoise);

  for (let i = 0; i < noise.length; i++) {
    let noiseValue: number;

    if (
      amplitude === 0 ||
      octaves === 0 ||
      persistence === 0 ||
      isNaN(noise[i])
    ) {
      noiseValue = 0;
    } else {
      noiseValue = (noise[i] - 0.5) * 10; // -5 to 5
    }

    noiseArray.push({
      noise: noiseValue,
      time: startTime + i * 1000 * 60 * 5,
    });
  }

  return noiseArray;
}

/**
 * Finds or interpolates the closest noise value for a given timestamp.
 */
export function getNoiseForTimestamp(
  points: NoisePoint[],
  targetTimeMs: number
): number | null {
  if (!points || points.length === 0) return null;

  const first = points[0];
  const last = points[points.length - 1];

  if (!first || !last) return null;

  // If outside of the array range
  if (targetTimeMs < first.time || targetTimeMs > last.time) {
    return null;
  }

  // Points are spaced by 5 minutes (300,000 ms)
  const index = Math.round((targetTimeMs - first.time) / (5 * 60 * 1000));
  if (index >= 0 && index < points.length && points[index]) {
    return points[index].noise;
  }

  return null;
}
