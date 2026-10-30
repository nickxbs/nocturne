import { describe, it, expect } from "vitest";
import { CgmSimulatorEngine } from "../src/simulator.js";
import type { CgmSimParameters, CgmSimSubject, CurrentEntry, Treatment } from "../src/types.js";

describe("CgmSimulatorEngine using @lsandini/cgmsim-lib", () => {
  const engine = new CgmSimulatorEngine();

  const mockSubject: CgmSimSubject = {
    sex: "male",
    weight: 70.0,
    age: 30,
    height: 170.0,
  };

  const mockParams: CgmSimParameters = {
    isf: 50.0,
    dia: 4.0,
    carbsAbsTime: 180,
    insulinPeak: 75,
    cr: 10.0,
  };

  it("produces valid glucose and IOB/COB when no treatments are active", () => {
    const now = new Date();
    const prev: CurrentEntry = { sgv: 100, date: now.getTime() - 300000 };

    const { entry, result } = engine.computeNextReading(now, mockSubject, mockParams, [], prev);

    expect(entry.type).toBe("sgv");
    expect(entry.sgv).toBeGreaterThanOrEqual(40);
    expect(entry.sgv).toBeLessThanOrEqual(400);
    expect(entry.device).toBe("cgmsim-node");
    expect(result.iob).toBeCloseTo(0, 1);
    expect(result.cob).toBe(0);
  });

  it("reflects insulin on board when recent bolus is active", () => {
    const now = new Date();
    const bolusTreatment: Treatment = {
      eventType: "Correction Bolus",
      insulin: 5.0,
      mills: now.getTime() - 30 * 60 * 1000, // 30 min ago
    };
    const prev: CurrentEntry = { sgv: 180, date: now.getTime() - 300000 };

    const { entry, result } = engine.computeNextReading(
      now,
      mockSubject,
      mockParams,
      [bolusTreatment],
      prev
    );

    expect(result.iob).toBeGreaterThan(0);
    expect(entry.sgv).toBeDefined();
  });

  it("reflects carbs on board when recent meal is active", () => {
    const now = new Date();
    const mealTreatment: Treatment = {
      eventType: "Meal",
      carbs: 60.0,
      mills: now.getTime() - 20 * 60 * 1000, // 20 min ago
    };
    const prev: CurrentEntry = { sgv: 110, date: now.getTime() - 300000 };

    const { entry, result } = engine.computeNextReading(
      now,
      mockSubject,
      mockParams,
      [mealTreatment],
      prev
    );

    expect(result.cob).toBeGreaterThan(0);
    expect(entry.sgv).toBeDefined();
  });

  it("enables pump mode when a basalProfile is provided", () => {
    const now = new Date();
    const prev: CurrentEntry = { sgv: 110, date: now.getTime() - 300000 };

    const withoutPump = engine.computeNextReading(now, mockSubject, mockParams, [], prev);
    expect(withoutPump.pumpEnabled).toBe(false);

    const withPump = engine.computeNextReading(
      now,
      mockSubject,
      mockParams,
      [],
      prev,
      [{ hour: 0, rate: 0.85 }]
    );
    expect(withPump.pumpEnabled).toBe(true);
  });
});
