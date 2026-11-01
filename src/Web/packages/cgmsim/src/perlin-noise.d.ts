declare module "perlin-noise" {
  export interface PerlinNoiseOptions {
    amplitude?: number;
    octaveCount?: number;
    persistence?: number;
    whiteNoise?: number[];
  }

  export function generatePerlinNoise(
    width: number,
    height: number,
    options?: PerlinNoiseOptions
  ): number[];

  export function generateWhiteNoise(width: number, height: number): number[];

  const defaultExport: {
    generatePerlinNoise: typeof generatePerlinNoise;
    generateWhiteNoise: typeof generateWhiteNoise;
  };

  export default defaultExport;
}

