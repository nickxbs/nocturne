import { describe, it, expect } from "vitest";
import { toCreateEntryInput, toUpdateEntryInput } from "./entry-request";
import type { EntryRecord } from "$lib/constants/entry-categories";
import { BolusType, GlucoseType, GlucoseUnit, DeviceEventType } from "$lib/api";

describe("entry-request mapping", () => {
  const testMills = 1792904577976;
  const expectedIso = new Date(testMills).toISOString();

  it("maps bolus create request and replaces mills with ISO timestamp", () => {
    const record: EntryRecord = {
      kind: "bolus",
      data: {
        mills: testMills,
        insulin: 4,
        bolusType: BolusType.Normal,
        correlationId: "380e8597-aa51-4cb6-a8ab-5af83f5cf804",
      },
    };

    const input = toCreateEntryInput(record);
    expect(input.kind).toBe("bolus");
    if (input.kind !== "bolus") {
      throw new Error("Expected bolus input");
    }
    expect(input.data.timestamp).toBe(expectedIso);
    expect(input.data.insulin).toBe(4);
    expect(input.data.bolusType).toBe(BolusType.Normal);
    expect(input.data.correlationId).toBe("380e8597-aa51-4cb6-a8ab-5af83f5cf804");
    expect("mills" in input.data).toBe(false);
  });

  it("maps carbs create request and replaces mills with ISO timestamp", () => {
    const record: EntryRecord = {
      kind: "carbs",
      data: {
        mills: testMills,
        carbs: 45,
        correlationId: "380e8597-aa51-4cb6-a8ab-5af83f5cf804",
      },
    };

    const input = toCreateEntryInput(record);
    expect(input.kind).toBe("carbs");
    if (input.kind !== "carbs") {
      throw new Error("Expected carbs input");
    }
    expect(input.data.timestamp).toBe(expectedIso);
    expect(input.data.carbs).toBe(45);
    expect(input.data.correlationId).toBe("380e8597-aa51-4cb6-a8ab-5af83f5cf804");
    expect("mills" in input.data).toBe(false);
  });

  it("maps bgCheck, note, deviceEvent, and basalInjection create requests", () => {
    const bgCheck: EntryRecord = {
      kind: "bgCheck",
      data: { mills: testMills, glucose: 120, units: GlucoseUnit.MgDl, glucoseType: GlucoseType.Finger },
    };
    expect(toCreateEntryInput(bgCheck)).toEqual({
      kind: "bgCheck",
      data: {
        timestamp: expectedIso,
        utcOffset: expect.any(Number),
        app: "Nocturne Web",
        glucose: 120,
        units: GlucoseUnit.MgDl,
        glucoseType: GlucoseType.Finger,
      },
    });

    const note: EntryRecord = {
      kind: "note",
      data: { mills: testMills, text: "Snack time", isAnnouncement: true },
    };
    expect(toCreateEntryInput(note)).toEqual({
      kind: "note",
      data: {
        timestamp: expectedIso,
        utcOffset: expect.any(Number),
        app: "Nocturne Web",
        text: "Snack time",
        eventType: undefined,
        isAnnouncement: true,
      },
    });

    const deviceEvent: EntryRecord = {
      kind: "deviceEvent",
      data: { mills: testMills, eventType: DeviceEventType.SiteChange, notes: "Arm" },
    };
    expect(toCreateEntryInput(deviceEvent)).toEqual({
      kind: "deviceEvent",
      data: {
        timestamp: expectedIso,
        utcOffset: expect.any(Number),
        app: "Nocturne Web",
        eventType: DeviceEventType.SiteChange,
        notes: "Arm",
      },
    });

    const basalInjection: EntryRecord = {
      kind: "basalInjection",
      data: {
        mills: testMills,
        units: 20,
        insulinContext: { patientInsulinId: "ins-1" },
        correlationId: "corr-1",
      },
    };
    expect(toCreateEntryInput(basalInjection)).toEqual({
      kind: "basalInjection",
      data: {
        timestamp: expectedIso,
        utcOffset: expect.any(Number),
        app: "Nocturne Web",
        patientInsulinId: "ins-1",
        units: 20,
        notes: undefined,
        correlationId: "corr-1",
      },
    });
  });

  it("maps update request preserving id, provenance, and converting mills to timestamp", () => {
    const record: EntryRecord = {
      kind: "bolus",
      data: {
        id: "bolus-123",
        mills: testMills,
        insulin: 5,
        device: "pump-x",
        dataSource: "tandem",
        syncIdentifier: "sync-456",
      },
    };

    const update = toUpdateEntryInput(record);
    expect(update.kind).toBe("bolus");
    if (update.kind !== "bolus") {
      throw new Error("Expected bolus update");
    }
    expect(update.id).toBe("bolus-123");
    expect(update.data.timestamp).toBe(expectedIso);
    expect(update.data.insulin).toBe(5);
    expect(update.data.device).toBe("pump-x");
    expect(update.data.dataSource).toBe("tandem");
    expect(update.data.syncIdentifier).toBe("sync-456");
    expect("mills" in update.data).toBe(false);
  });
});
