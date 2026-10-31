import {
  simulateGlucose,
  type CorePatient,
  type CoreTreatment,
  type CoreGlucoseEntry,
  type CoreSimulationResult,
  type SimulationLogger,
} from "@lsandini/cgmsim-lib";
import type {
  BasalProfileEntry,
  CgmSimParameters,
  CgmSimSubject,
  CurrentEntry,
  SimulatedEntry,
  Treatment,
  TrendDirection,
} from "./types.js";

function toTrendDirection(dir: string | undefined): TrendDirection {
  switch (dir) {
    case "DoubleUp":
      return "DoubleUp";
    case "SingleUp":
      return "SingleUp";
    case "FortyFiveUp":
      return "FortyFiveUp";
    case "FortyFiveDown":
      return "FortyFiveDown";
    case "SingleDown":
      return "SingleDown";
    case "DoubleDown":
      return "DoubleDown";
    default:
      return "Flat";
  }
}

export class CgmSimulatorEngine {
  private readonly defaultParameters: CgmSimParameters = {
    isf: 50.0,
    dia: 4.0,
    carbsAbsTime: 180,
    insulinPeak: 75,
    cr: 10.0,
  };

  private readonly defaultSubject: CgmSimSubject = {
    sex: "male",
    weight: 70.0,
    age: 30,
    height: 170.0,
  };

  /**
   * Runs a physiological simulation cycle using @lsandini/cgmsim-lib.
   */
  computeNextReading(
    now: Date,
    subject: CgmSimSubject | undefined,
    parameters: CgmSimParameters | undefined,
    treatments: Treatment[],
    previousEntry: CurrentEntry | null,
    basalProfile?: BasalProfileEntry[],
    options?: { debug?: boolean; logger?: SimulationLogger }
  ): { entry: SimulatedEntry; result: CoreSimulationResult; pumpEnabled: boolean } {
    const params = parameters || this.defaultParameters;
    const subj = subject || this.defaultSubject;

    const patient: CorePatient = {
      isf: params.isf || 50,
      cr: params.cr || 10,
      dia: params.dia || 4,
      tp: params.insulinPeak || 75,
      weight: subj.weight || 70,
      age: subj.age || 30,
      gender: subj.sex?.toLowerCase() === "female" ? "female" : "male",
      carbsAbsTime: params.carbsAbsTime || 180,
    };

    // Convert Nocturne treatments to @lsandini/cgmsim-lib CoreTreatment[]
    const coreTreatments: CoreTreatment[] = [];
    for (const t of treatments) {
      const ts = t.mills || (t.created_at ? new Date(t.created_at).getTime() : now.getTime());

      if (t.insulin && t.insulin > 0) {
        coreTreatments.push({
          timestamp: ts,
          type: "bolus",
          units: t.insulin,
        });
      }

      if (t.carbs && t.carbs > 0) {
        coreTreatments.push({
          timestamp: ts,
          type: "carb",
          carbs: t.carbs,
        });
      }

      if (t.notes && t.notes.trim().length > 0) {
        coreTreatments.push({
          timestamp: ts,
          type: "basal",
          notes: t.notes.trim(),
        });
      }
    }

    // Convert recent Nocturne entries to CoreGlucoseEntry[]
    const recentEntries: CoreGlucoseEntry[] = [];
    if (previousEntry?.sgv) {
      const entryTimestamp =
        previousEntry.mills ||
        previousEntry.date ||
        (previousEntry.dateString ? new Date(previousEntry.dateString).getTime() : now.getTime() - 300000);

      recentEntries.push({
        timestamp: entryTimestamp,
        sgv: previousEntry.sgv,
      });
    }

    const activeBasalProfile =
      basalProfile && basalProfile.length > 0 ? basalProfile : undefined;
    const pumpEnabled = Boolean(activeBasalProfile);

    // Run simulation through @lsandini/cgmsim-lib
    let simResult: CoreSimulationResult;
    try {
      simResult = simulateGlucose({
        patient,
        treatments: coreTreatments,
        recentEntries,
        basalProfile: activeBasalProfile,
        targetTime: now,
        logger: options?.logger,
      });
    } catch (simErr) {
      console.error("[CgmSimulatorEngine] Failed during simulateGlucose execution:", simErr);
      console.error("[CgmSimulatorEngine] Input Payload Dump:", JSON.stringify({
        targetTime: now.toISOString(),
        patient,
        treatmentsCount: coreTreatments.length,
        treatments: coreTreatments,
        recentEntries,
        pumpEnabled,
        basalProfile: activeBasalProfile,
      }, null, 2));
      throw simErr;
    }

    const direction: TrendDirection = toTrendDirection(simResult.direction);

    const entry: SimulatedEntry = {
      type: "sgv",
      sgv: Math.round(simResult.sgv),
      direction,
      date: now.getTime(),
      dateString: now.toISOString(),
      device: "cgmsim-node",
    };

    return { entry, result: simResult, pumpEnabled };
  }
}
