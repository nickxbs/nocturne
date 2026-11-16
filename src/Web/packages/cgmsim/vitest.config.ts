import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
    coverage: {
      provider: "v8",
      reporter: ["text-summary", "json-summary", "cobertura"],
      reportsDirectory: "coverage",
      include: ["src/**/*.{ts,js}"],
      exclude: ["src/**/*.test.ts", "**/*.d.ts"],
    },
  },
});
