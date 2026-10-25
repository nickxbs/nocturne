import { getRequestEvent, query, command } from "$app/server";
import { z } from "zod";
import {
  CreateBolusRequestSchema,
  UpdateBolusRequestSchema,
  CreateCarbIntakeRequestSchema,
  UpdateCarbIntakeRequestSchema,
  UpsertBGCheckRequestSchema,
  UpsertNoteRequestSchema,
  UpsertDeviceEventRequestSchema,
  CreateBasalInjectionRequestSchema,
  UpdateBasalInjectionRequestSchema,
} from "$lib/api/generated/schemas";

const entriesSchema = z.object({
  dateRange: z.object({
    from: z.date().optional(),
    to: z.date().optional(),
  }),
});

export const getEntries = query(entriesSchema, async (props) => {
  const { locals } = getRequestEvent();
  const { apiClient } = locals;

  const { from = new Date(), to = new Date() } = props.dateRange;
  if (!from || !to) throw new Error("Invalid date range");

  const response = await apiClient.sensorGlucose.getAll(from.toISOString(), to.toISOString(), 10000);
  return response.data ?? [];
});

export const getBolusesAndCarbs = query(entriesSchema, async (props) => {
  const { locals } = getRequestEvent();
  const { apiClient } = locals;

  const { from = new Date(), to = new Date() } = props.dateRange;
  if (!from || !to) throw new Error("Invalid date range");

  const [bolusResponse, carbResponse] = await Promise.all([
    apiClient.bolus.getAll(from.toISOString(), to.toISOString(), 10000),
    apiClient.nutrition.getCarbIntakes(from.toISOString(), to.toISOString(), 10000),
  ]);

  return {
    boluses: bolusResponse.data ?? [],
    carbIntakes: carbResponse.data ?? [],
  };
});

const treatmentIdSchema = z.object({
  treatmentId: z.string(),
});

/**
 * Look up a v4 entry record by treatmentId, trying each entity type
 * sequentially. Returns the matching record or null if not found in any
 * collection.
 */
export const getEntryByTreatmentId = query(
  treatmentIdSchema,
  async ({ treatmentId }) => {
    const { locals } = getRequestEvent();
    const { apiClient } = locals;

    const fetchers = [
      {
        kind: "bolus" as const,
        fetch: () => apiClient.bolus.getById(treatmentId),
      },
      {
        kind: "carbs" as const,
        fetch: () => apiClient.nutrition.getCarbIntakeById(treatmentId),
      },
      {
        kind: "bgCheck" as const,
        fetch: () => apiClient.bGCheck.getById(treatmentId),
      },
      {
        kind: "note" as const,
        fetch: () => apiClient.note.getById(treatmentId),
      },
      {
        kind: "deviceEvent" as const,
        fetch: () => apiClient.deviceEvent.getById(treatmentId),
      },
    ];

    for (const { kind, fetch } of fetchers) {
      try {
        const data = await fetch();
        if (data?.id) return { kind, data };
      } catch {
        // 404 or other error — try the next type
      }
    }

    return null;
  }
);

/**
 * Create a single entry (v4: dispatches to the correct endpoint by kind).
 */
export const createEntry = command(
  z.discriminatedUnion("kind", [
    z.object({ kind: z.literal("bolus"), data: CreateBolusRequestSchema }),
    z.object({ kind: z.literal("carbs"), data: CreateCarbIntakeRequestSchema }),
    z.object({ kind: z.literal("bgCheck"), data: UpsertBGCheckRequestSchema }),
    z.object({ kind: z.literal("note"), data: UpsertNoteRequestSchema }),
    z.object({ kind: z.literal("deviceEvent"), data: UpsertDeviceEventRequestSchema }),
    z.object({ kind: z.literal("basalInjection"), data: CreateBasalInjectionRequestSchema }),
  ]),
  async (input) => {
    const { apiClient } = getRequestEvent().locals;
    switch (input.kind) {
      case "bolus":
        return await apiClient.bolus.create(input.data);
      case "carbs":
        return await apiClient.nutrition.createCarbIntake(input.data);
      case "bgCheck":
        return await apiClient.bGCheck.create(input.data);
      case "note":
        return await apiClient.note.create(input.data);
      case "deviceEvent":
        return await apiClient.deviceEvent.create(input.data);
      case "basalInjection":
        return await apiClient.basalInjection.create(input.data);
    }
  }
);

/**
 * Update a single entry (v4: dispatches to the correct endpoint by kind).
 */
export const updateEntry = command(
  z.discriminatedUnion("kind", [
    z.object({ kind: z.literal("bolus"), id: z.string().min(1), data: UpdateBolusRequestSchema }),
    z.object({ kind: z.literal("carbs"), id: z.string().min(1), data: UpdateCarbIntakeRequestSchema }),
    z.object({ kind: z.literal("bgCheck"), id: z.string().min(1), data: UpsertBGCheckRequestSchema }),
    z.object({ kind: z.literal("note"), id: z.string().min(1), data: UpsertNoteRequestSchema }),
    z.object({ kind: z.literal("deviceEvent"), id: z.string().min(1), data: UpsertDeviceEventRequestSchema }),
    z.object({ kind: z.literal("basalInjection"), id: z.string().min(1), data: UpdateBasalInjectionRequestSchema }),
  ]),
  async (input) => {
    const { apiClient } = getRequestEvent().locals;
    switch (input.kind) {
      case "bolus":
        return await apiClient.bolus.update(input.id, input.data);
      case "carbs":
        return await apiClient.nutrition.updateCarbIntake(input.id, input.data);
      case "bgCheck":
        return await apiClient.bGCheck.update(input.id, input.data);
      case "note":
        return await apiClient.note.update(input.id, input.data);
      case "deviceEvent":
        return await apiClient.deviceEvent.update(input.id, input.data);
      case "basalInjection":
        return await apiClient.basalInjection.update(input.id, input.data);
    }
  }
);

