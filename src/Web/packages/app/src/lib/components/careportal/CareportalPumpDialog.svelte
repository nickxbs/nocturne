<script lang="ts">
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as Dialog from "#lib/components/ui/dialog/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import { Label } from "#lib/components/ui/label/index.js";
  import {
    BolusType,
    DeviceEventType,
    GlucoseType,
    GlucoseUnit,
    ReservoirReportKind,
    TempBasalOrigin,
  } from "#lib/api/index.js";
  import { createEntry } from "#lib/api/entries.remote.js";
  import { remoteErrorMessage } from "#lib/api/remote-error.js";
  import { getWizardPreview } from "#lib/api/generated/cgmSims.generated.remote.js";
  import { getProfileSummary } from "#lib/api/generated/profiles.generated.remote.js";
  import { create as createReservoirReport } from "#lib/api/generated/reservoirReports.generated.remote.js";
  import { getRealtimeStore } from "#lib/stores/realtime-store.svelte.js";
  import { glucoseUnits } from "#lib/stores/appearance-store.svelte.js";
  import {
    bgLabel,
    convertFromDisplayUnits,
    timeStringToSeconds,
  } from "#lib/utils/formatting.js";
  import { toast } from "svelte-sonner";
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import Calendar from "@lucide/svelte/icons/calendar";
  import Check from "@lucide/svelte/icons/check";
  import CircleX from "@lucide/svelte/icons/circle-x";
  import Cpu from "@lucide/svelte/icons/cpu";
  import Droplet from "@lucide/svelte/icons/droplet";
  import Hourglass from "@lucide/svelte/icons/hourglass";
  import Martini from "@lucide/svelte/icons/martini";
  import Minus from "@lucide/svelte/icons/minus";
  import Pill from "@lucide/svelte/icons/pill";
  import Play from "@lucide/svelte/icons/play";
  import Plus from "@lucide/svelte/icons/plus";
  import RefreshCw from "@lucide/svelte/icons/refresh-cw";
  import Settings from "@lucide/svelte/icons/settings";
  import Square from "@lucide/svelte/icons/square";
  import Syringe from "@lucide/svelte/icons/syringe";
  import Timer from "@lucide/svelte/icons/timer";
  import Utensils from "@lucide/svelte/icons/utensils";
  import WandSparkles from "@lucide/svelte/icons/wand-sparkles";
  import Wheat from "@lucide/svelte/icons/wheat";
  import Zap from "@lucide/svelte/icons/zap";

  interface Props {
    open: boolean;
    currentReservoir?: number | null;
    onSaved?: () => void;
  }

  let {
    open = $bindable(false),
    currentReservoir = null,
    onSaved,
  }: Props = $props();

  type ScreenId =
    | "root"
    | "meal-menu"
    | "wizard"
    | "food"
    | "carbs-correction"
    | "extended-bolus"
    | "drink"
    | "correction-bolus"
    | "pump-menu"
    | "basal-profile"
    | "temp-basal"
    | "refill-reservoir"
    | "medication"
    | "sensor-menu"
    | "bg-check"
    | "settings-menu";

  const realtimeStore = getRealtimeStore();

  let screen = $state<ScreenId>("root");
  let submitting = $state(false);

  let wizardCarbs = $state(0);
  let wizardAdj = $state(0);

  let foodCarbs = $state(0);
  let foodProteins = $state(0);
  let foodFats = $state(0);

  let fastCarbs = $state(15);
  let drinkCarbs = $state(15);

  let extBolusUnits = $state(1.0);
  let extBolusCarbs = $state(0);
  let extBolusDuration = $state(60);

  let correctionBolusUnits = $state(1.0);

  let basalMorning = $state(0.8);
  let basalAfternoon = $state(0.8);
  let basalEvening = $state(0.8);
  let basalNight = $state(0.8);
  let basalInitialized = $state(false);

  let tempBasalRate = $state(0.8);
  let tempBasalDuration = $state(30);

  let reservoirCapacity = $state<100 | 200 | 300>(200);

  let medicationUnits = $state(0);
  let medicationNote = $state("");

  let bgCheckValue = $state(100);

  const wizardQuery = $derived(
    open && screen === "wizard"
      ? getWizardPreview({ carbs: wizardCarbs, adjustment: wizardAdj })
      : null,
  );
  const wizardPreview = $derived(wizardQuery?.current);
  const recommendedBolus = $derived(wizardPreview?.recommendedBolus ?? 0);

  const profileQuery = getProfileSummary(undefined);
  const profileSummary = $derived(profileQuery.current);

  const activeProfileName = $derived.by(() => {
    const settings = profileSummary?.therapySettings ?? [];
    const def = settings.find((s) => s.isDefault) ?? settings[0];
    return def?.profileName ?? "Default";
  });

  $effect(() => {
    if (!open) {
      screen = "root";
      submitting = false;
      basalInitialized = false;
      return;
    }

    if (screen === "basal-profile" && !basalInitialized && profileSummary) {
      const schedule = (profileSummary.basalSchedules ?? []).find(
        (b) => b.profileName === activeProfileName,
      );
      const entries = schedule?.entries ?? [];
      if (entries.length > 0) {
        const rateAt = (hour: number) => {
          const targetSec = hour * 3600;
          let current = entries[0]?.value ?? 0.8;
          for (const e of entries) {
            const sec =
              e.timeAsSeconds ??
              (e.time ? timeStringToSeconds(e.time) : 0);
            if (sec <= targetSec && typeof e.value === "number") {
              current = e.value;
            }
          }
          return Number(current.toFixed(2));
        };
        basalMorning = rateAt(6);
        basalAfternoon = rateAt(11);
        basalEvening = rateAt(18);
        basalNight = rateAt(23);
      }
      basalInitialized = true;
    }
  });

  const parentScreen: Record<ScreenId, ScreenId | null> = {
    root: null,
    "meal-menu": "root",
    wizard: "meal-menu",
    food: "meal-menu",
    "carbs-correction": "meal-menu",
    "extended-bolus": "meal-menu",
    drink: "meal-menu",
    "correction-bolus": "root",
    "pump-menu": "root",
    "basal-profile": "pump-menu",
    "temp-basal": "pump-menu",
    "refill-reservoir": "pump-menu",
    medication: "root",
    "sensor-menu": "root",
    "bg-check": "sensor-menu",
    "settings-menu": "root",
  };

  const canConfirm = $derived.by(() => {
    if (submitting) return false;
    switch (screen) {
      case "wizard":
        return wizardCarbs > 0 || recommendedBolus > 0;
      case "food":
        return foodCarbs > 0 || foodProteins > 0 || foodFats > 0;
      case "carbs-correction":
        return fastCarbs > 0;
      case "extended-bolus":
        return extBolusUnits > 0 || extBolusCarbs > 0;
      case "drink":
        return drinkCarbs > 0;
      case "correction-bolus":
        return correctionBolusUnits > 0;
      case "basal-profile":
        return true;
      case "temp-basal":
        return tempBasalRate >= 0 && tempBasalDuration > 0;
      case "refill-reservoir":
        return reservoirCapacity > 0;
      case "medication":
        return medicationUnits > 0 || medicationNote.trim().length > 0;
      case "bg-check":
        return bgCheckValue > 0;
      default:
        return false;
    }
  });

  function handleBackOrCancel() {
    const parent = parentScreen[screen];
    if (parent === null) {
      open = false;
    } else {
      screen = parent;
    }
  }

  function stepValue(
    current: number,
    delta: number,
    min: number,
    max: number,
    precision = 0,
  ): number {
    const factor = Math.pow(10, precision);
    const next = Math.round((current + delta) * factor) / factor;
    return Math.max(min, Math.min(max, next));
  }

  function commonMeta() {
    const now = new Date();
    return {
      timestamp: now.toISOString(),
      utcOffset: -now.getTimezoneOffset(),
      app: "Nocturne Careportal",
    };
  }

  async function recordSensorEvent(eventType: DeviceEventType, label: string) {
    if (submitting) return;
    submitting = true;
    try {
      await createEntry({
        kind: "deviceEvent",
        data: {
          ...commonMeta(),
          eventType,
        },
      });
      realtimeStore.noteTreatmentWrite();
      toast.success(`${label} recorded`);
      open = false;
      onSaved?.();
    } catch (err) {
      toast.error(remoteErrorMessage(err, `Failed to record ${label.toLowerCase()}`));
    } finally {
      submitting = false;
    }
  }

  async function handleConfirm() {
    if (!canConfirm) return;
    submitting = true;

    try {
      const meta = commonMeta();

      switch (screen) {
        case "wizard": {
          const bolus = Number(recommendedBolus.toFixed(2));
          if (wizardCarbs > 0 && bolus > 0) {
            await createEntry({
              kind: "meal",
              data: {
                ...meta,
                carbs: wizardCarbs,
                insulin: bolus,
                bolusType: BolusType.Normal,
              },
            });
          } else if (bolus > 0) {
            await createEntry({
              kind: "bolus",
              data: {
                ...meta,
                insulin: bolus,
                bolusType: BolusType.Normal,
              },
            });
          } else if (wizardCarbs > 0) {
            await createEntry({
              kind: "carbs",
              data: {
                ...meta,
                carbs: wizardCarbs,
              },
            });
          }
          realtimeStore.noteTreatmentWrite();
          toast.success(`Wizard: ${bolus.toFixed(1)}U / ${wizardCarbs}g recorded`);
          wizardCarbs = 0;
          wizardAdj = 0;
          break;
        }

        case "food": {
          await createEntry({
            kind: "carbs",
            data: {
              ...meta,
              carbs: foodCarbs,
              proteinGrams: foodProteins > 0 ? foodProteins : undefined,
              fatGrams: foodFats > 0 ? foodFats : undefined,
            },
          });
          realtimeStore.noteTreatmentWrite();
          toast.success(
            `Food recorded (${foodCarbs}g CHO, ${foodProteins}g P, ${foodFats}g F)`,
          );
          foodCarbs = 0;
          foodProteins = 0;
          foodFats = 0;
          break;
        }

        case "carbs-correction": {
          await createEntry({
            kind: "carbs",
            data: {
              ...meta,
              carbs: fastCarbs,
              absorptionTime: 60,
            },
          });
          realtimeStore.noteTreatmentWrite();
          toast.success(`Fast carbs (${fastCarbs}g) recorded`);
          break;
        }

        case "extended-bolus": {
          if (extBolusCarbs > 0 && extBolusUnits > 0) {
            await createEntry({
              kind: "meal",
              data: {
                ...meta,
                carbs: extBolusCarbs,
                insulin: extBolusUnits,
                bolusType: BolusType.Square,
                duration: extBolusDuration,
              },
            });
          } else if (extBolusUnits > 0) {
            await createEntry({
              kind: "bolus",
              data: {
                ...meta,
                insulin: extBolusUnits,
                bolusType: BolusType.Square,
                duration: extBolusDuration,
              },
            });
          }
          realtimeStore.noteTreatmentWrite();
          toast.success(
            `Extended bolus (${extBolusUnits}U over ${extBolusDuration}m) recorded`,
          );
          break;
        }

        case "drink": {
          await createEntry({
            kind: "carbs",
            data: {
              ...meta,
              carbs: drinkCarbs,
              absorptionTime: 90,
            },
          });
          realtimeStore.noteTreatmentWrite();
          toast.success(`Drink (${drinkCarbs}g) recorded`);
          break;
        }

        case "correction-bolus": {
          await createEntry({
            kind: "bolus",
            data: {
              ...meta,
              insulin: correctionBolusUnits,
              bolusType: BolusType.Normal,
            },
          });
          realtimeStore.noteTreatmentWrite();
          toast.success(`Correction bolus (${correctionBolusUnits}U) recorded`);
          break;
        }

        case "basal-profile": {
          await createEntry({
            kind: "basalSchedule",
            data: {
              profileName: activeProfileName,
              timestamp: meta.timestamp,
              dataSource: "manual",
              entries: [
                {
                  time: "00:00",
                  timeAsSeconds: 0,
                  value: basalNight,
                },
                {
                  time: "06:00",
                  timeAsSeconds: 6 * 3600,
                  value: basalMorning,
                },
                {
                  time: "11:00",
                  timeAsSeconds: 11 * 3600,
                  value: basalAfternoon,
                },
                {
                  time: "18:00",
                  timeAsSeconds: 18 * 3600,
                  value: basalEvening,
                },
                {
                  time: "23:00",
                  timeAsSeconds: 23 * 3600,
                  value: basalNight,
                },
              ],
            },
          });
          await profileQuery.refresh();
          realtimeStore.noteTreatmentWrite();
          toast.success("Basal profile updated");
          break;
        }

        case "temp-basal": {
          await createEntry({
            kind: "tempBasal",
            data: {
              ...meta,
              rate: tempBasalRate,
              durationMinutes: tempBasalDuration,
              origin: TempBasalOrigin.Manual,
            },
          });
          realtimeStore.noteTreatmentWrite();
          toast.success(
            `Temp basal (${tempBasalRate} U/h for ${tempBasalDuration} min) started`,
          );
          break;
        }

        case "refill-reservoir": {
          await createReservoirReport({
            units: reservoirCapacity,
            kind: ReservoirReportKind.Fill,
            timestamp: meta.timestamp,
            utcOffset: meta.utcOffset,
            app: meta.app,
          });
          realtimeStore.noteTreatmentWrite();
          toast.success(`Reservoir refilled (${reservoirCapacity}U)`);
          break;
        }

        case "medication": {
          if (medicationUnits > 0) {
            await createEntry({
              kind: "basalInjection",
              data: {
                ...meta,
                units: medicationUnits,
                notes: medicationNote.trim() || undefined,
              },
            });
          } else if (medicationNote.trim()) {
            await createEntry({
              kind: "note",
              data: {
                ...meta,
                text: medicationNote.trim(),
                eventType: "Medication",
              },
            });
          }
          realtimeStore.noteTreatmentWrite();
          toast.success("Medication recorded");
          medicationUnits = 0;
          medicationNote = "";
          break;
        }

        case "bg-check": {
          const mgdl = convertFromDisplayUnits(
            bgCheckValue,
            glucoseUnits.current,
          );
          await createEntry({
            kind: "bgCheck",
            data: {
              ...meta,
              glucose: mgdl,
              units:
                glucoseUnits.current === "mmol"
                  ? GlucoseUnit.Mmol
                  : GlucoseUnit.MgDl,
              glucoseType: GlucoseType.Finger,
            },
          });
          realtimeStore.noteTreatmentWrite();
          toast.success(`BG Check (${bgCheckValue} ${bgLabel()}) recorded`);
          break;
        }
      }

      open = false;
      onSaved?.();
    } catch (err) {
      toast.error(remoteErrorMessage(err, "Failed to save entry"));
    } finally {
      submitting = false;
    }
  }

  const blueStepperBrand = {
    background: "var(--info)",
    foreground: "var(--info-foreground)",
  };
  const grayStepperBrand = {
    background: "var(--muted)",
    foreground: "var(--foreground)",
  };
  const cancelBrand = {
    background: "var(--warning)",
    foreground: "var(--warning-foreground)",
  };
  const okBrand = {
    background: "var(--success)",
    foreground: "var(--success-foreground)",
  };
</script>

{#snippet stepperRow(
  label: string,
  value: number,
  onMinus: () => void,
  onPlus: () => void,
  onInput: (val: number) => void,
  variant: "blue" | "gray" = "blue",
  step: number = 1,
)}
  <div class="flex flex-col items-center gap-1.5">
    <span class="text-xs font-medium text-foreground">{label}</span>
    <div class="flex items-center justify-center gap-5 w-full">
      <Button
        variant="brand"
        size="icon"
        brand={variant === "blue" ? blueStepperBrand : grayStepperBrand}
        onclick={onMinus}
        aria-label="Decrease {label}"
      >
        <Minus class="size-5" />
      </Button>

      <Input
        type="number"
        {step}
        value={value}
        oninput={(e) => {
          const parsed = Number(e.currentTarget.value);
          if (!Number.isNaN(parsed)) onInput(parsed);
        }}
        class="w-16 text-center"
      />

      <Button
        variant="brand"
        size="icon"
        brand={variant === "blue" ? blueStepperBrand : grayStepperBrand}
        onclick={onPlus}
        aria-label="Increase {label}"
      >
        <Plus class="size-5" />
      </Button>
    </div>
  </div>
{/snippet}

<Dialog.Root bind:open>
  <Dialog.Content
    showClose={false}
    class="dark max-w-sm select-none"
  >
    <Dialog.Title class="sr-only">Careportal Pump Menu</Dialog.Title>
    <Dialog.Description class="sr-only">
      Touch-oriented insulin pump careportal interface
    </Dialog.Description>

    <div class="rounded-2xl border border-border bg-secondary p-3 shadow-2xl flex flex-col gap-2.5 text-foreground">
      <div
        class="rounded-xl bg-background p-3.5 min-h-96 flex flex-col gap-2.5"
      >
        {#if screen === "root"}
          <div
            class="bg-carbs text-primary-foreground font-bold text-center py-2.5 px-4 rounded-lg text-lg tracking-tight"
          >
            Advanced Mode
          </div>

          <div class="flex flex-col gap-2">
            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "meal-menu")}
              class="w-full justify-start"
            >
              <Utensils class="size-5 shrink-0" />
              <span>Meal</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "correction-bolus")}
              class="w-full justify-start"
            >
              <Zap class="size-5 shrink-0 fill-current" />
              <span>Correction Bolus</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "pump-menu")}
              class="w-full justify-start"
            >
              <Syringe class="size-5 shrink-0" />
              <span>Insulin Pump</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "medication")}
              class="w-full justify-start"
            >
              <Pill class="size-5 shrink-0" />
              <span>Medication</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "sensor-menu")}
              class="w-full justify-start"
            >
              <Cpu class="size-5 shrink-0" />
              <span>CGM Sensor</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "settings-menu")}
              class="w-full justify-start"
            >
              <Settings class="size-5 shrink-0" />
              <span>Settings</span>
            </Button>
          </div>
        {:else if screen === "meal-menu"}
          <div class="flex flex-col gap-2">
            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "wizard")}
              class="w-full justify-start"
            >
              <WandSparkles class="size-5 shrink-0" />
              <span>Wizard</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "food")}
              class="w-full justify-start"
            >
              <Utensils class="size-5 shrink-0" />
              <span>Food</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "carbs-correction")}
              class="w-full justify-start"
            >
              <Wheat class="size-5 shrink-0" />
              <span>Carbs Correction</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "extended-bolus")}
              class="w-full justify-start"
            >
              <Timer class="size-5 shrink-0" />
              <span>Extended bolus</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "drink")}
              class="w-full justify-start"
            >
              <Martini class="size-5 shrink-0" />
              <span>Drink</span>
            </Button>
          </div>
        {:else if screen === "wizard"}
          <h3 class="text-center text-lg font-medium text-foreground">
            Carbs / Insulin
          </h3>

          <div class="bg-muted py-2 px-3 rounded text-center text-lg">
            <span class="text-foreground">Result: </span>
            <span class="text-insulin font-semibold">
              {recommendedBolus.toFixed(1)}U
            </span>
            <span class="text-muted-foreground"> - </span>
            <span class="text-carbs font-semibold">{wizardCarbs}g</span>
          </div>

          {@render stepperRow(
            "Carbs (gr):",
            wizardCarbs,
            () => (wizardCarbs = stepValue(wizardCarbs, -5, 0, 300, 0)),
            () => (wizardCarbs = stepValue(wizardCarbs, 5, 0, 300, 0)),
            (v) => (wizardCarbs = Math.max(0, Math.round(v))),
            "blue",
            5,
          )}

          {@render stepperRow(
            "Insulin Adjustment (U):",
            wizardAdj,
            () => (wizardAdj = stepValue(wizardAdj, -0.5, -20, 20, 1)),
            () => (wizardAdj = stepValue(wizardAdj, 0.5, -20, 20, 1)),
            (v) => (wizardAdj = v),
            "blue",
            0.5,
          )}

          <div
            class="bg-primary text-primary-foreground rounded-md p-2.5 mt-auto text-sm space-y-0.5"
          >
            <div class="flex justify-between font-bold">
              <span>Bolus</span>
              <span>{recommendedBolus.toFixed(1)}U</span>
            </div>
            <div class="flex justify-between">
              <span>BG</span>
              <span>{(wizardPreview?.bgUnits ?? 0).toFixed(1)}U</span>
            </div>
            <div class="flex justify-between">
              <span>CHO</span>
              <span>{(wizardPreview?.choUnits ?? 0).toFixed(1)}U</span>
            </div>
            <div class="flex justify-between">
              <span>COB</span>
              <span>{(wizardPreview?.cobUnits ?? 0).toFixed(1)}U</span>
            </div>
            <div class="flex justify-between">
              <span>IOB</span>
              <span>{(wizardPreview?.iobUnits ?? 0).toFixed(1)}U</span>
            </div>
            <div class="flex justify-between">
              <span>Adj.</span>
              <span>{(wizardPreview?.adjustmentUnits ?? wizardAdj).toFixed(1)}U</span>
            </div>
          </div>
        {:else if screen === "food"}
          <h3 class="text-center text-lg font-medium text-foreground mb-1">
            Food
          </h3>

          <div class="flex flex-col gap-3">
            {@render stepperRow(
              "Carbs (gr):",
              foodCarbs,
              () => (foodCarbs = stepValue(foodCarbs, -5, 0, 300, 0)),
              () => (foodCarbs = stepValue(foodCarbs, 5, 0, 300, 0)),
              (v) => (foodCarbs = Math.max(0, Math.round(v))),
              "blue",
              5,
            )}

            {@render stepperRow(
              "Proteins (gr):",
              foodProteins,
              () => (foodProteins = stepValue(foodProteins, -5, 0, 300, 0)),
              () => (foodProteins = stepValue(foodProteins, 5, 0, 300, 0)),
              (v) => (foodProteins = Math.max(0, Math.round(v))),
              "gray",
              5,
            )}

            {@render stepperRow(
              "Fats (gr):",
              foodFats,
              () => (foodFats = stepValue(foodFats, -5, 0, 300, 0)),
              () => (foodFats = stepValue(foodFats, 5, 0, 300, 0)),
              (v) => (foodFats = Math.max(0, Math.round(v))),
              "gray",
              5,
            )}
          </div>
        {:else if screen === "carbs-correction"}
          <h3 class="text-center text-lg font-medium text-foreground mb-2">
            Carbs Correction
          </h3>

          {@render stepperRow(
            "Fast Carbs (gr):",
            fastCarbs,
            () => (fastCarbs = stepValue(fastCarbs, -5, 0, 200, 0)),
            () => (fastCarbs = stepValue(fastCarbs, 5, 0, 200, 0)),
            (v) => (fastCarbs = Math.max(0, Math.round(v))),
            "blue",
            5,
          )}
        {:else if screen === "extended-bolus"}
          <h3 class="text-center text-lg font-medium text-foreground mb-1">
            Extended bolus
          </h3>

          <div class="flex flex-col gap-3">
            {@render stepperRow(
              "Insulin (U):",
              extBolusUnits,
              () => (extBolusUnits = stepValue(extBolusUnits, -0.5, 0, 50, 1)),
              () => (extBolusUnits = stepValue(extBolusUnits, 0.5, 0, 50, 1)),
              (v) => (extBolusUnits = Math.max(0, v)),
              "blue",
              0.5,
            )}

            {@render stepperRow(
              "Carbs (gr):",
              extBolusCarbs,
              () => (extBolusCarbs = stepValue(extBolusCarbs, -5, 0, 300, 0)),
              () => (extBolusCarbs = stepValue(extBolusCarbs, 5, 0, 300, 0)),
              (v) => (extBolusCarbs = Math.max(0, Math.round(v))),
              "blue",
              5,
            )}

            {@render stepperRow(
              "Duration (min):",
              extBolusDuration,
              () =>
                (extBolusDuration = stepValue(extBolusDuration, -15, 15, 480, 0)),
              () =>
                (extBolusDuration = stepValue(extBolusDuration, 15, 15, 480, 0)),
              (v) => (extBolusDuration = Math.max(15, Math.round(v))),
              "blue",
              15,
            )}
          </div>
        {:else if screen === "drink"}
          <h3 class="text-center text-lg font-medium text-foreground mb-2">
            Drink
          </h3>

          {@render stepperRow(
            "Carbs (gr):",
            drinkCarbs,
            () => (drinkCarbs = stepValue(drinkCarbs, -5, 0, 200, 0)),
            () => (drinkCarbs = stepValue(drinkCarbs, 5, 0, 200, 0)),
            (v) => (drinkCarbs = Math.max(0, Math.round(v))),
            "blue",
            5,
          )}
        {:else if screen === "correction-bolus"}
          <h3 class="text-center text-lg font-medium text-foreground mb-2">
            Correction Bolus
          </h3>

          {@render stepperRow(
            "Insulin (U):",
            correctionBolusUnits,
            () =>
              (correctionBolusUnits = stepValue(
                correctionBolusUnits,
                -0.5,
                0,
                50,
                1,
              )),
            () =>
              (correctionBolusUnits = stepValue(
                correctionBolusUnits,
                0.5,
                0,
                50,
                1,
              )),
            (v) => (correctionBolusUnits = Math.max(0, v)),
            "blue",
            0.5,
          )}
        {:else if screen === "pump-menu"}
          <div class="flex flex-col gap-2">
            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "basal-profile")}
              class="w-full justify-start"
            >
              <Calendar class="size-5 shrink-0" />
              <span>Basal Profile</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "temp-basal")}
              class="w-full justify-start"
            >
              <Hourglass class="size-5 shrink-0" />
              <span>Temp Basal</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "refill-reservoir")}
              class="w-full justify-start"
            >
              <Droplet class="size-5 shrink-0" />
              <span>Refill Reservoir</span>
            </Button>
          </div>
        {:else if screen === "basal-profile"}
          <h3 class="text-center text-lg font-medium text-foreground">
            Basal Profile
          </h3>

          <div class="flex flex-col gap-2">
            {@render stepperRow(
              "Morning 06-11 (U/h):",
              basalMorning,
              () => (basalMorning = stepValue(basalMorning, -0.05, 0, 20, 2)),
              () => (basalMorning = stepValue(basalMorning, 0.05, 0, 20, 2)),
              (v) => (basalMorning = Math.max(0, v)),
              "blue",
              0.05,
            )}

            {@render stepperRow(
              "Afternoon 11-18 (U/h):",
              basalAfternoon,
              () => (basalAfternoon = stepValue(basalAfternoon, -0.05, 0, 20, 2)),
              () => (basalAfternoon = stepValue(basalAfternoon, 0.05, 0, 20, 2)),
              (v) => (basalAfternoon = Math.max(0, v)),
              "blue",
              0.05,
            )}

            {@render stepperRow(
              "Evening 18-23 (U/h):",
              basalEvening,
              () => (basalEvening = stepValue(basalEvening, -0.05, 0, 20, 2)),
              () => (basalEvening = stepValue(basalEvening, 0.05, 0, 20, 2)),
              (v) => (basalEvening = Math.max(0, v)),
              "blue",
              0.05,
            )}

            {@render stepperRow(
              "Night 23-06 (U/h):",
              basalNight,
              () => (basalNight = stepValue(basalNight, -0.05, 0, 20, 2)),
              () => (basalNight = stepValue(basalNight, 0.05, 0, 20, 2)),
              (v) => (basalNight = Math.max(0, v)),
              "blue",
              0.05,
            )}
          </div>
        {:else if screen === "temp-basal"}
          <h3 class="text-center text-lg font-medium text-foreground mb-1">
            Temp Basal
          </h3>

          <div class="flex flex-col gap-3">
            {@render stepperRow(
              "Rate (U/h)",
              tempBasalRate,
              () => (tempBasalRate = stepValue(tempBasalRate, -0.1, 0, 20, 1)),
              () => (tempBasalRate = stepValue(tempBasalRate, 0.1, 0, 20, 1)),
              (v) => (tempBasalRate = Math.max(0, v)),
              "blue",
              0.1,
            )}

            {@render stepperRow(
              "Duration (min)",
              tempBasalDuration,
              () =>
                (tempBasalDuration = stepValue(
                  tempBasalDuration,
                  -15,
                  15,
                  720,
                  0,
                )),
              () =>
                (tempBasalDuration = stepValue(
                  tempBasalDuration,
                  15,
                  15,
                  720,
                  0,
                )),
              (v) => (tempBasalDuration = Math.max(15, Math.round(v))),
              "blue",
              15,
            )}
          </div>
        {:else if screen === "refill-reservoir"}
          <div
            class="bg-primary text-primary-foreground font-normal text-base py-2.5 px-3.5 rounded-lg flex items-center gap-3 w-full"
          >
            <Droplet class="size-5 shrink-0" />
            <span>Refill Reservoir</span>
          </div>

          <div class="flex flex-col items-center gap-3 mt-2">
            <span class="text-sm text-foreground">Current Reservoir Level:</span>
            <div
              class="w-48 py-2.5 rounded-lg border border-info bg-muted text-center text-base font-medium text-foreground"
            >
              {currentReservoir != null ? `${currentReservoir.toFixed(0)}U` : "N/A"}
            </div>

            <span class="text-sm text-foreground mt-1">Select new capacity :</span>
            <div class="flex items-center justify-center gap-3 w-full">
              {#each [100, 200, 300] as const as cap (cap)}
                <Button
                  variant={reservoirCapacity === cap ? "outline" : "brand"}
                  size="sm"
                  brand={reservoirCapacity === cap ? undefined : blueStepperBrand}
                  onclick={() => (reservoirCapacity = cap)}
                >
                  {cap}U
                </Button>
              {/each}
            </div>

            <div class="my-3 flex items-center justify-center text-foreground">
              <Syringe class="size-12" />
            </div>

            <span class="text-sm text-foreground">Press OK to refill</span>
          </div>
        {:else if screen === "medication"}
          <h3 class="text-center text-lg font-medium text-foreground mb-1">
            Medication
          </h3>

          <div class="flex flex-col gap-3">
            {@render stepperRow(
              "Basal / Long-Acting Insulin (U):",
              medicationUnits,
              () => (medicationUnits = stepValue(medicationUnits, -1, 0, 100, 0)),
              () => (medicationUnits = stepValue(medicationUnits, 1, 0, 100, 0)),
              (v) => (medicationUnits = Math.max(0, v)),
              "blue",
              1,
            )}

            <div class="flex flex-col gap-1 mt-2">
              <Label for="pump-med-note" size="sm" variant="muted" class="text-center">
                Medication / Note:
              </Label>
              <Input
                id="pump-med-note"
                type="text"
                bind:value={medicationNote}
                placeholder="e.g. Lantus, Metformin..."
                class="w-full"
              />
            </div>
          </div>
        {:else if screen === "sensor-menu"}
          <div class="flex flex-col gap-2">
            <Button
              variant="default"
              size="lg"
              onclick={() => (screen = "bg-check")}
              class="w-full justify-start"
            >
              <Droplet class="size-5 shrink-0" />
              <span>BG Check / Calibration</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() =>
                recordSensorEvent(DeviceEventType.SensorStart, "Sensor Start")}
              class="w-full justify-start"
            >
              <Play class="size-5 shrink-0" />
              <span>Start Sensor</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() =>
                recordSensorEvent(DeviceEventType.SensorChange, "Sensor Change")}
              class="w-full justify-start"
            >
              <RefreshCw class="size-5 shrink-0" />
              <span>Change Sensor</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() =>
                recordSensorEvent(DeviceEventType.SensorStop, "Sensor Stop")}
              class="w-full justify-start"
            >
              <Square class="size-5 shrink-0" />
              <span>Stop Sensor</span>
            </Button>
          </div>
        {:else if screen === "bg-check"}
          <h3 class="text-center text-lg font-medium text-foreground mb-2">
            BG Check
          </h3>

          {@render stepperRow(
            `Glucose (${bgLabel()}):`,
            bgCheckValue,
            () =>
              (bgCheckValue = stepValue(
                bgCheckValue,
                glucoseUnits.current === "mmol" ? -0.5 : -5,
                20,
                600,
                glucoseUnits.current === "mmol" ? 1 : 0,
              )),
            () =>
              (bgCheckValue = stepValue(
                bgCheckValue,
                glucoseUnits.current === "mmol" ? 0.5 : 5,
                20,
                600,
                glucoseUnits.current === "mmol" ? 1 : 0,
              )),
            (v) => (bgCheckValue = Math.max(1, v)),
            "blue",
            glucoseUnits.current === "mmol" ? 0.5 : 5,
          )}
        {:else if screen === "settings-menu"}
          <div class="flex flex-col gap-2">
            <Button
              variant="default"
              size="lg"
              onclick={() => {
                open = false;
                goto(resolve("/cgmsim/subject"));
              }}
              class="w-full justify-start"
            >
              <Settings class="size-5 shrink-0" />
              <span>Subject (CGMSIM)</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => {
                open = false;
                goto(resolve("/cgmsim/parameters"));
              }}
              class="w-full justify-start"
            >
              <Settings class="size-5 shrink-0" />
              <span>Parameters (CGMSIM)</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => {
                open = false;
                goto(resolve("/settings/profile"));
              }}
              class="w-full justify-start"
            >
              <Calendar class="size-5 shrink-0" />
              <span>Therapy Profile</span>
            </Button>

            <Button
              variant="default"
              size="lg"
              onclick={() => {
                open = false;
                goto(resolve("/settings/trackers"));
              }}
              class="w-full justify-start"
            >
              <Droplet class="size-5 shrink-0" />
              <span>Trackers & Consumables</span>
            </Button>
          </div>
        {/if}
      </div>

      <div class="flex items-center justify-around pt-0.5">
        <Button
          variant="brand"
          size="lg"
          brand={cancelBrand}
          onclick={handleBackOrCancel}
        >
          {#if screen === "root"}
            <CircleX class="size-5" />
            <span>CANC</span>
          {:else}
            <ArrowLeft class="size-5" />
            <span>BACK</span>
          {/if}
        </Button>

        <Button
          variant={canConfirm ? "brand" : "secondary"}
          size="lg"
          brand={canConfirm ? okBrand : undefined}
          disabled={!canConfirm}
          onclick={handleConfirm}
        >
          <Check class="size-5" />
          <span>OK</span>
        </Button>
      </div>
    </div>
  </Dialog.Content>
</Dialog.Root>
