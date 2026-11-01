<script lang="ts">
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as Dialog from "#lib/components/ui/dialog/index.js";
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
    bg,
    bgDelta,
    bgLabel,
    convertFromDisplayUnits,
    timeStringToSeconds,
  } from "#lib/utils/formatting.js";
  import { trendAngle } from "@nocturne/ui/glucose";
  import { toast } from "svelte-sonner";
  import Activity from "@lucide/svelte/icons/activity";
  import AlertTriangle from "@lucide/svelte/icons/alert-triangle";
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import ArrowRight from "@lucide/svelte/icons/arrow-right";
  import BatteryCharging from "@lucide/svelte/icons/battery-charging";
  import Calendar from "@lucide/svelte/icons/calendar";
  import Check from "@lucide/svelte/icons/check";
  import ChevronRight from "@lucide/svelte/icons/chevron-right";
  import CircleX from "@lucide/svelte/icons/circle-x";
  import Cpu from "@lucide/svelte/icons/cpu";
  import Droplet from "@lucide/svelte/icons/droplet";
  import Hourglass from "@lucide/svelte/icons/hourglass";
  import Martini from "@lucide/svelte/icons/martini";
  import Minus from "@lucide/svelte/icons/minus";
  import OctagonX from "@lucide/svelte/icons/octagon-x";
  import Pill from "@lucide/svelte/icons/pill";
  import Play from "@lucide/svelte/icons/play";
  import Plus from "@lucide/svelte/icons/plus";
  import RefreshCw from "@lucide/svelte/icons/refresh-cw";
  import Settings from "@lucide/svelte/icons/settings";
  import ShieldAlert from "@lucide/svelte/icons/shield-alert";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
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
    | "basal-mdi"
    | "basal-profile"
    | "temp-basal"
    | "refill-reservoir"
    | "medication"
    | "sensor-menu"
    | "bg-check"
    | "settings-menu";

  type BasalDrugCode = "GLA" | "DET" | "TOU" | "DEG";

  const basalInsulinTypes: Array<{
    code: BasalDrugCode;
    label: string;
    brand: string;
    kinetics: string;
  }> = [
    { code: "GLA", label: "Glargine", brand: "Lantus", kinetics: "24h" },
    { code: "DET", label: "Detemir", brand: "Levemir", kinetics: "16-24h" },
    { code: "TOU", label: "Toujeo", brand: "Glargine U300", kinetics: "24-36h" },
    { code: "DEG", label: "Degludec", brand: "Tresiba", kinetics: "42h" },
  ];

  const realtimeStore = getRealtimeStore();

  let screen = $state<ScreenId>("wizard");
  let submitting = $state(false);
  let togglingPump = $state(false);
  let pumpEnabled = $state(true);
  let pumpStateSynced = $state(false);
  let selectedBasalDrug = $state<BasalDrugCode>("GLA");
  let basalMdiUnits = $state(18);
  let confirmingBolus = $state(false);
  let deliveringBolus = $state(false);
  let deliveryProgress = $state(0);
  let deliveryTimer: ReturnType<typeof setInterval> | null = null;

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

  const activeBasalScheduleEntries = $derived.by(() => {
    const schedule = (profileSummary?.basalSchedules ?? []).find(
      (b) => b.profileName === activeProfileName,
    );
    return schedule?.entries ?? [];
  });

  const hasActivePumpSchedule = $derived(
    activeBasalScheduleEntries.some((e) => typeof e.value === "number" && e.value > 0),
  );

  const activeBasalRate = $derived.by(() => {
    const pillRate = realtimeStore.pillsData?.basal?.totalBasal;
    if (typeof pillRate === "number" && pillRate > 0) {
      return pillRate.toFixed(2);
    }
    const first = activeBasalScheduleEntries.find(
      (e) => typeof e.value === "number" && e.value > 0,
    )?.value;
    return typeof first === "number" ? first.toFixed(2) : "0.80";
  });

  const formattedClock = $derived.by(() => {
    const d = new Date(realtimeStore.now);
    return d.toLocaleTimeString([], {
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    });
  });

  const liveBgMgdl = $derived(
    wizardPreview?.currentBg ?? realtimeStore.currentBG ?? 100,
  );
  const targetBgMgdl = $derived(wizardPreview?.targetBg ?? 100);
  const arrowDegrees = $derived(trendAngle(realtimeStore.bgDelta ?? 0));
  const isDoubleTrend = $derived(Math.abs(realtimeStore.bgDelta ?? 0) >= 10);

  const bgBadgeTone = $derived.by(() => {
    const current = realtimeStore.currentBG;
    const thresholds = realtimeStore.chartThresholds;
    const veryLow = thresholds?.veryLow ?? 54;
    const low = thresholds?.low ?? 70;
    const high = thresholds?.high ?? 180;
    const veryHigh = thresholds?.veryHigh ?? 250;
    if (current <= veryLow || current >= veryHigh) {
      return "bg-red-950/90 border-red-500/60 text-red-200";
    }
    if (current <= low || current >= high) {
      return "bg-amber-950/90 border-amber-500/60 text-amber-200";
    }
    return "bg-emerald-950/90 border-emerald-500/50 text-emerald-200";
  });

  function clearDeliveryTimer() {
    if (deliveryTimer !== null) {
      clearInterval(deliveryTimer);
      deliveryTimer = null;
    }
  }

  $effect(() => {
    if (!open) {
      clearDeliveryTimer();
      screen = "wizard";
      submitting = false;
      confirmingBolus = false;
      deliveringBolus = false;
      deliveryProgress = 0;
      basalInitialized = false;
      pumpStateSynced = false;
      return;
    }

    if (profileSummary && !pumpStateSynced) {
      pumpEnabled = hasActivePumpSchedule;
      pumpStateSynced = true;
    }

    if (screen === "basal-profile" && !basalInitialized && profileSummary) {
      const entries = activeBasalScheduleEntries;
      if (entries.some((e) => typeof e.value === "number" && e.value > 0)) {
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

  async function setPumpActiveState(nextEnabled: boolean) {
    if (togglingPump || submitting) return;
    togglingPump = true;
    pumpEnabled = nextEnabled;
    try {
      const meta = commonMeta();
      if (nextEnabled) {
        const morning = basalMorning > 0 ? basalMorning : 0.8;
        const afternoon = basalAfternoon > 0 ? basalAfternoon : 0.8;
        const evening = basalEvening > 0 ? basalEvening : 0.8;
        const night = basalNight > 0 ? basalNight : 0.8;
        await createEntry({
          kind: "basalSchedule",
          data: {
            profileName: activeProfileName,
            timestamp: meta.timestamp,
            dataSource: "manual",
            entries: [
              { time: "00:00", timeAsSeconds: 0, value: night },
              { time: "06:00", timeAsSeconds: 6 * 3600, value: morning },
              { time: "11:00", timeAsSeconds: 11 * 3600, value: afternoon },
              { time: "18:00", timeAsSeconds: 18 * 3600, value: evening },
              { time: "23:00", timeAsSeconds: 23 * 3600, value: night },
            ],
          },
        });
        await profileQuery.refresh();
        realtimeStore.noteTreatmentWrite();
        toast.success("Insulin pump enabled");
        if (screen === "basal-mdi") {
          screen = "pump-menu";
        }
      } else {
        await createEntry({
          kind: "basalSchedule",
          data: {
            profileName: activeProfileName,
            timestamp: meta.timestamp,
            dataSource: "manual",
            entries: [{ time: "00:00", timeAsSeconds: 0, value: 0 }],
          },
        });
        await profileQuery.refresh();
        realtimeStore.noteTreatmentWrite();
        toast.success("Insulin pump disabled (MDI basal mode)");
        screen = "basal-mdi";
      }
    } catch (err) {
      toast.error(remoteErrorMessage(err, "Could not update pump mode"));
    } finally {
      togglingPump = false;
    }
  }

  const parentScreen: Record<ScreenId, ScreenId | null> = {
    root: null,
    "meal-menu": "root",
    wizard: null,
    food: "meal-menu",
    "carbs-correction": "meal-menu",
    "extended-bolus": "meal-menu",
    drink: "meal-menu",
    "correction-bolus": "root",
    "pump-menu": "root",
    "basal-mdi": "root",
    "basal-profile": "pump-menu",
    "temp-basal": "pump-menu",
    "refill-reservoir": "pump-menu",
    medication: "root",
    "sensor-menu": "root",
    "bg-check": "sensor-menu",
    "settings-menu": "root",
  };

  const canConfirm = $derived.by(() => {
    if (submitting || deliveringBolus) return false;
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
      case "basal-mdi":
        return basalMdiUnits > 0;
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

  const pendingBolusUnits = $derived.by(() => {
    if (screen === "wizard") return Number(recommendedBolus.toFixed(2));
    if (screen === "correction-bolus") return Number(correctionBolusUnits.toFixed(2));
    if (screen === "extended-bolus") return Number(extBolusUnits.toFixed(2));
    return 0;
  });

  const pendingCarbsGrams = $derived.by(() => {
    if (screen === "wizard") return wizardCarbs;
    if (screen === "extended-bolus") return extBolusCarbs;
    return 0;
  });

  const requiresSafetyConfirmation = $derived(
    (screen === "wizard" && recommendedBolus > 0) ||
      (screen === "correction-bolus" && correctionBolusUnits > 0) ||
      (screen === "extended-bolus" && extBolusUnits > 0),
  );

  function formatSignedUnits(val: number, decimals = 1): string {
    const fixed = Math.abs(val).toFixed(decimals);
    if (val > 0) return `+${fixed}`;
    if (val < 0) return `-${fixed}`;
    return `0.${"0".repeat(decimals)}`;
  }

  function handleBackOrCancel() {
    if (confirmingBolus) {
      confirmingBolus = false;
      return;
    }
    if (deliveringBolus) {
      stopBolusDelivery();
      return;
    }
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
      toast.error(remoteErrorMessage(err, `Could not record ${label.toLowerCase()}`));
    } finally {
      submitting = false;
    }
  }

  function handlePrimaryAction() {
    if (!canConfirm) return;
    if (requiresSafetyConfirmation && !confirmingBolus) {
      confirmingBolus = true;
      return;
    }
    void executeConfirm();
  }

  function startBolusDelivery() {
    confirmingBolus = false;
    deliveringBolus = true;
    deliveryProgress = 0;
    clearDeliveryTimer();

    deliveryTimer = setInterval(() => {
      deliveryProgress = Math.min(100, deliveryProgress + 20);
      if (deliveryProgress >= 100) {
        clearDeliveryTimer();
        void executeConfirm();
      }
    }, 180);
  }

  function stopBolusDelivery() {
    clearDeliveryTimer();
    deliveringBolus = false;
    deliveryProgress = 0;
    toast.warning("Bolus delivery stopped");
  }

  async function executeConfirm() {
    if (submitting) return;
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
          toast.success(`Bolus wizard: ${bolus.toFixed(1)}U / ${wizardCarbs}g recorded`);
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
            `Food recorded (${foodCarbs}g carbs, ${foodProteins}g protein, ${foodFats}g fat)`,
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

        case "basal-mdi": {
          const noteText = `${selectedBasalDrug} ${basalMdiUnits}`;
          await createEntry({
            kind: "basalInjection",
            data: {
              ...meta,
              units: basalMdiUnits,
              notes: noteText,
            },
          });
          await createEntry({
            kind: "note",
            data: {
              ...meta,
              text: noteText,
              eventType: "Announcement",
              isAnnouncement: true,
            },
          });
          realtimeStore.noteTreatmentWrite();
          toast.success(`Basal (${selectedBasalDrug} ${basalMdiUnits}U) recorded`);
          break;
        }

        case "basal-profile": {
          pumpEnabled = true;
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
          toast.success(`BG check (${bgCheckValue} ${bgLabel()}) recorded`);
          break;
        }
      }

      confirmingBolus = false;
      deliveringBolus = false;
      open = false;
      onSaved?.();
    } catch (err) {
      deliveringBolus = false;
      toast.error(remoteErrorMessage(err, "Could not save the entry"));
    } finally {
      submitting = false;
    }
  }
</script>

{#snippet stepperRow(
  label: string,
  value: number,
  unit: string,
  onMinus: () => void,
  onPlus: () => void,
  onInput: (val: number) => void,
  variant: "blue" | "gray" = "blue",
  step: number = 1,
)}
  <div class="bg-[#101726] border border-slate-800 rounded-xl p-2.5 flex flex-col gap-1.5">
    <div class="flex items-center justify-between">
      <span class="text-xs font-semibold text-slate-300">{label}</span>
      <span class="text-[10px] font-mono text-slate-400">Step {step}{unit}</span>
    </div>
    <div class="flex items-center justify-between gap-2">
      <button
        type="button"
        onclick={onMinus}
        aria-label="Decrease {label}"
        class="tactile-btn w-10 h-10 rounded-lg {variant === 'blue'
          ? 'bg-sky-600 hover:bg-sky-500 text-white'
          : 'bg-slate-800 hover:bg-slate-700 text-slate-200'} font-bold text-lg flex items-center justify-center shadow shrink-0"
      >
        <Minus class="size-4" />
      </button>

      <div class="flex-1 bg-black/60 rounded-lg py-1 px-2 border border-slate-800 flex items-center justify-center gap-1">
        <input
          type="number"
          {step}
          value={value}
          oninput={(e) => {
            const parsed = Number(e.currentTarget.value);
            if (!Number.isNaN(parsed)) onInput(parsed);
          }}
          aria-label={label}
          class="w-20 bg-transparent text-center text-lg font-black font-mono text-white focus:outline-none"
        />
        {#if unit}
          <span class="text-[10px] text-slate-400 font-mono">{unit}</span>
        {/if}
      </div>

      <button
        type="button"
        onclick={onPlus}
        aria-label="Increase {label}"
        class="tactile-btn w-10 h-10 rounded-lg {variant === 'blue'
          ? 'bg-sky-600 hover:bg-sky-500 text-white'
          : 'bg-slate-800 hover:bg-slate-700 text-slate-200'} font-bold text-lg flex items-center justify-center shadow shrink-0"
      >
        <Plus class="size-4" />
      </button>
    </div>
  </div>
{/snippet}

<Dialog.Root bind:open>
  <Dialog.Content
    showClose={false}
    class="dark max-w-xl w-[calc(100vw-1rem)] sm:w-full bg-transparent border-0 p-0 shadow-none select-none"
  >
    <Dialog.Title class="sr-only">Careportal Pump Menu</Dialog.Title>
    <Dialog.Description class="sr-only">
      Touch-oriented insulin pump careportal interface
    </Dialog.Description>

    <!-- Medical PDM Outer Titanium Chassis -->
    <div
      class="titanium-edge p-1.5 sm:p-2.5 rounded-[28px] sm:rounded-[36px] shadow-chassis w-full border border-slate-700/60 relative transition-all my-auto"
    >
      <!-- Top Speaker Slit & Optical Status Bar -->
      <div class="w-full flex justify-between items-center px-6 sm:px-8 pt-1 pb-1.5">
        <div class="flex items-center gap-1.5">
          <span
            class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse shadow-glow-emerald"
            title="Pump Linked (Encrypted BLE)"
          ></span>
          <span class="text-[9px] font-mono tracking-widest text-slate-400 uppercase">
            {deliveringBolus ? "SYS DELIVERY" : "SYS READY"}
          </span>
        </div>
        <div
          class="w-14 h-1 bg-black/80 rounded-full border-b border-white/10 shadow-inner"
        ></div>
        <div class="text-[9px] font-mono tracking-wider text-slate-400 uppercase">
          IPX8 • 3.0 mL
        </div>
      </div>

      <!-- Inner Black Glass Bezel -->
      <div
        class="bg-black p-2 sm:p-3 rounded-[24px] sm:rounded-[28px] shadow-bezel border border-slate-800/90 relative overflow-hidden"
      >
        <!-- Medical OLED Touchscreen -->
        <div
          class="oled-display rounded-[18px] sm:rounded-[20px] p-2.5 sm:p-3.5 border border-cyan-900/30 text-slate-100 flex flex-col justify-between shadow-2xl relative overflow-hidden"
        >
          <!-- OLED System Status Header -->
          <header
            class="flex items-center justify-between pb-2 border-b border-slate-800/80 text-[11px] font-mono gap-2"
            data-purpose="oled-system-status-bar"
          >
            <div class="flex items-center gap-2 min-w-0">
              <span class="font-bold text-white tracking-wide text-xs">
                {formattedClock}
              </span>
              <button
                type="button"
                disabled={togglingPump}
                onclick={() => void setPumpActiveState(!pumpEnabled)}
                title={pumpEnabled
                  ? "Select to disable the insulin pump and switch to MDI basal"
                  : "Select to enable the insulin pump"}
                class="tactile-btn px-2 py-0.5 rounded text-[10px] font-semibold border truncate flex items-center gap-1 transition-colors {pumpEnabled
                  ? 'bg-slate-800/90 hover:bg-slate-700 text-sky-400 border-sky-400/30'
                  : 'bg-amber-950/80 hover:bg-amber-900/80 text-amber-300 border-amber-500/40'}"
              >
                <span
                  class="w-1.5 h-1.5 rounded-full {pumpEnabled
                    ? 'bg-emerald-400'
                    : 'bg-amber-400'}"
                ></span>
                {#if pumpEnabled}
                  <span>PUMP ON • {activeBasalRate} U/h</span>
                {:else}
                  <span>PUMP OFF • MDI</span>
                {/if}
              </button>
            </div>

            <div class="flex items-center gap-2 shrink-0">
              <!-- Insulin Cartridge Reservoir -->
              <div
                class="flex items-center gap-1 text-slate-300"
                title="Insulin reservoir"
              >
                <Droplet class="size-3.5 text-sky-400" />
                <span class="text-[11px] font-bold">
                  {currentReservoir != null ? `${currentReservoir.toFixed(0)}U` : "--U"}
                </span>
              </div>

              <!-- Battery Status -->
              <div class="hidden sm:flex items-center gap-0.5 text-emerald-400">
                <BatteryCharging class="size-3.5" />
                <span class="text-[10px]">88%</span>
              </div>
            </div>
          </header>

          <!-- Prominent Live CGM Glucose & Trend Arrow Banner -->
          <div
            class="mt-2 flex items-center justify-between rounded-xl border px-3 py-2 shadow-inner {bgBadgeTone}"
            data-purpose="prominent-cgm-banner"
          >
            <div class="flex items-center gap-3">
              <div class="flex items-baseline gap-1">
                <span class="font-mono text-2xl sm:text-3xl font-black tracking-tight leading-none">
                  {bg(realtimeStore.currentBG)}
                </span>
                <span class="text-[10px] font-mono font-bold opacity-80 uppercase">
                  {bgLabel()}
                </span>
              </div>

              <!-- High-Visibility Trend Arrow(s) -->
              <div
                class="flex h-9 min-w-9 px-1.5 items-center justify-center rounded-lg bg-black/40 border border-white/15 shadow"
                title="Glucose trend direction"
              >
                <div class="flex items-center -space-x-1">
                  <ArrowRight
                    class="size-5 sm:size-6 stroke-[3] transition-transform duration-500"
                    style="transform: rotate({arrowDegrees}deg)"
                  />
                  {#if isDoubleTrend}
                    <ArrowRight
                      class="size-5 sm:size-6 stroke-[3] transition-transform duration-500"
                      style="transform: rotate({arrowDegrees}deg)"
                    />
                  {/if}
                </div>
              </div>

              <div class="flex flex-col">
                <span class="font-mono text-xs sm:text-sm font-extrabold leading-tight">
                  {bgDelta(realtimeStore.bgDelta ?? 0)} {bgLabel()}
                </span>
                <span class="text-[9px] font-mono opacity-75">
                  {realtimeStore.timeSinceReading || "Live CGM"}
                </span>
              </div>
            </div>

            <div class="text-right font-mono text-[10px] opacity-85 hidden sm:block">
              <div class="uppercase tracking-wider text-[9px] opacity-75">Target BG</div>
              <div class="font-bold text-xs">{bg(targetBgMgdl)} {bgLabel()}</div>
            </div>
          </div>

          <!-- Mode Selector / Medical Wizard Header Tabs -->
          <div
            class="grid grid-cols-2 gap-1.5 mt-2 mb-2 p-1 bg-slate-900/90 rounded-xl border border-slate-800"
            data-purpose="device-navigation-tabs"
          >
            <button
              type="button"
              onclick={() => {
                confirmingBolus = false;
                screen = "wizard";
              }}
              class="py-1.5 text-xs rounded-lg flex items-center justify-center gap-1.5 transition-colors {screen ===
              'wizard'
                ? 'font-semibold bg-sky-600/30 text-sky-300 border border-sky-500/40 shadow'
                : 'font-medium text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'}"
            >
              <Zap class="size-3.5 {screen === 'wizard' ? 'text-sky-400' : 'text-slate-400'}" />
              <span>Bolus Calculator</span>
            </button>
            <button
              type="button"
              onclick={() => {
                confirmingBolus = false;
                screen = "root";
              }}
              class="py-1.5 text-xs rounded-lg flex items-center justify-center gap-1.5 transition-colors {screen !==
              'wizard'
                ? 'font-semibold bg-sky-600/30 text-sky-300 border border-sky-500/40 shadow'
                : 'font-medium text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'}"
            >
              <SlidersHorizontal
                class="size-3.5 {screen !== 'wizard' ? 'text-sky-400' : 'text-slate-400'}"
              />
              <span>Advanced Mode</span>
            </button>
          </div>

          {#if deliveringBolus}
            <!-- Bolus Delivery In Progress View -->
            <div class="flex flex-col gap-2.5 py-1">
              <div
                class="bg-[#101726] border border-sky-500/30 rounded-xl p-3 flex flex-col items-center text-center shadow-inner"
              >
                <div class="flex items-center gap-1.5 text-[10px] font-mono text-sky-400 uppercase tracking-wider mb-1">
                  <Activity class="size-3.5 animate-pulse" />
                  <span>INLINE INFUSION ACTIVE</span>
                </div>
                <div class="text-3xl font-extrabold font-mono text-sky-300">
                  {((pendingBolusUnits * deliveryProgress) / 100).toFixed(2)}
                  <span class="text-sm text-sky-400 font-bold">
                    / {pendingBolusUnits.toFixed(2)} U
                  </span>
                </div>
                <div class="w-full bg-slate-900 h-2.5 rounded-full overflow-hidden border border-slate-800 my-2">
                  <div
                    class="bg-gradient-to-r from-sky-500 to-emerald-400 h-full transition-all duration-150"
                    style="width: {deliveryProgress}%"
                  ></div>
                </div>
                <div class="w-full flex justify-between text-[10px] font-mono text-slate-400">
                  <span>Speed: 1.50 U/min</span>
                  <span class="text-emerald-400 font-bold">{deliveryProgress}%</span>
                  <span>Catheter: NORMAL</span>
                </div>
              </div>

              <button
                type="button"
                onclick={stopBolusDelivery}
                class="tactile-btn w-full bg-gradient-to-b from-red-500 to-red-700 hover:from-red-400 hover:to-red-600 text-white font-black py-3 px-4 rounded-xl border border-red-400/40 flex items-center justify-center gap-2 text-xs sm:text-sm tracking-wide shadow-lg"
              >
                <OctagonX class="size-4" />
                <span>STOP DELIVERY</span>
              </button>
              <p class="text-[10px] font-mono text-slate-400 text-center">
                Select to stop the pump immediately and cancel the bolus.
              </p>
            </div>
          {:else if screen === "wizard"}
            <!-- Primary Calculated Result Display (Hero OLED Readout) -->
            <section
              class="bg-gradient-to-b from-[#121c2e] to-[#0c1424] rounded-xl p-2.5 border border-sky-900/50 shadow-inner mb-2"
              data-purpose="bolus-calculation-summary-banner"
            >
              <div class="flex items-center justify-between px-0.5">
                <div>
                  <span
                    class="text-[10px] font-semibold text-sky-400 uppercase tracking-wider block"
                  >
                    Recommended Bolus
                  </span>
                  <span class="text-[9px] text-slate-400 font-mono tracking-tight">
                    Formula: (CHO / IC) + (BG - Target) / ISF - IOB
                  </span>
                </div>
                <div class="text-right flex items-baseline gap-1">
                  <span
                    class="text-2xl sm:text-3xl font-extrabold text-sky-300 font-mono tracking-tight drop-shadow-md"
                  >
                    {recommendedBolus.toFixed(2)}
                  </span>
                  <span class="text-xs font-bold text-sky-400">U</span>
                </div>
              </div>
              <div
                class="mt-1.5 flex items-center justify-between text-[10px] pt-1.5 border-t border-slate-700/50 text-slate-400"
              >
                <span>Nutritional Breakdown:</span>
                <span class="font-mono text-amber-300 font-semibold">
                  {wizardCarbs}g Carbs • {bg(liveBgMgdl)} {bgLabel()} Correction
                </span>
              </div>
            </section>

            <!-- Dual Interactive Input Modules (Carbs & Manual Bolus Adjustment) -->
            <div
              class="grid grid-cols-1 sm:grid-cols-2 gap-2 mb-2"
              data-purpose="stepper-controls"
            >
              <!-- Module 1: Carb Input Module -->
              <div
                class="bg-[#101726] border border-slate-800 rounded-xl p-2 flex flex-col justify-between"
              >
                <div class="flex justify-between items-center mb-0.5">
                  <label
                    class="text-[11px] font-semibold text-slate-300 flex items-center gap-1.5"
                    for="pdm-carbs-input"
                  >
                    <Utensils class="size-3.5 text-amber-400" />
                    <span>Carbs (g)</span>
                  </label>
                  <span class="text-[9px] font-mono text-slate-400">Step 5g</span>
                </div>
                <div class="flex items-center justify-between gap-1.5 my-1">
                  <button
                    type="button"
                    aria-label="Decrease carbs"
                    onclick={() => (wizardCarbs = stepValue(wizardCarbs, -5, 0, 300, 0))}
                    class="tactile-btn w-9 h-9 sm:w-10 sm:h-10 rounded-lg bg-sky-600 hover:bg-sky-500 text-white font-bold text-lg flex items-center justify-center shadow shrink-0"
                  >
                    <Minus class="size-4" />
                  </button>
                  <div
                    class="flex-1 bg-black/60 rounded-lg py-1 px-2 border border-slate-800 flex items-center justify-center"
                  >
                    <input
                      id="pdm-carbs-input"
                      type="number"
                      step="5"
                      value={wizardCarbs}
                      oninput={(e) => {
                        const parsed = Number(e.currentTarget.value);
                        if (!Number.isNaN(parsed)) {
                          wizardCarbs = Math.max(0, Math.round(parsed));
                        }
                      }}
                      class="w-14 bg-transparent text-center text-lg sm:text-xl font-black font-mono text-white focus:outline-none"
                    />
                    <span class="text-[9px] text-slate-400 font-mono ml-0.5">g</span>
                  </div>
                  <button
                    type="button"
                    aria-label="Increase carbs"
                    onclick={() => (wizardCarbs = stepValue(wizardCarbs, 5, 0, 300, 0))}
                    class="tactile-btn w-9 h-9 sm:w-10 sm:h-10 rounded-lg bg-sky-600 hover:bg-sky-500 text-white font-bold text-lg flex items-center justify-center shadow shrink-0"
                  >
                    <Plus class="size-4" />
                  </button>
                </div>
                <div class="flex gap-1 justify-center">
                  {#each [10, 20, 40] as preset (preset)}
                    <button
                      type="button"
                      onclick={() =>
                        (wizardCarbs = stepValue(wizardCarbs, preset, 0, 300, 0))}
                      class="px-2 py-0.5 rounded bg-slate-800/80 hover:bg-slate-700 text-[9px] font-mono text-slate-300"
                    >
                      +{preset}g
                    </button>
                  {/each}
                </div>
              </div>

              <!-- Module 2: Insulin Adjustment Module -->
              <div
                class="bg-[#101726] border border-slate-800 rounded-xl p-2 flex flex-col justify-between"
              >
                <div class="flex justify-between items-center mb-0.5">
                  <label
                    class="text-[11px] font-semibold text-slate-300 flex items-center gap-1.5"
                    for="pdm-adj-input"
                  >
                    <SlidersHorizontal class="size-3.5 text-sky-400" />
                    <span>Adjustment (U)</span>
                  </label>
                  <span class="text-[9px] font-mono text-slate-400">Step 0.1U</span>
                </div>
                <div class="flex items-center justify-between gap-1.5 my-1">
                  <button
                    type="button"
                    aria-label="Decrease insulin adjustment"
                    onclick={() => (wizardAdj = stepValue(wizardAdj, -0.1, -20, 20, 1))}
                    class="tactile-btn w-9 h-9 sm:w-10 sm:h-10 rounded-lg bg-sky-600 hover:bg-sky-500 text-white font-bold text-lg flex items-center justify-center shadow shrink-0"
                  >
                    <Minus class="size-4" />
                  </button>
                  <div
                    class="flex-1 bg-black/60 rounded-lg py-1 px-2 border border-slate-800 flex items-center justify-center"
                  >
                    <input
                      id="pdm-adj-input"
                      type="number"
                      step="0.1"
                      value={wizardAdj}
                      oninput={(e) => {
                        const parsed = Number(e.currentTarget.value);
                        if (!Number.isNaN(parsed)) {
                          wizardAdj = parsed;
                        }
                      }}
                      class="w-14 bg-transparent text-center text-lg sm:text-xl font-black font-mono text-white focus:outline-none"
                    />
                    <span class="text-[9px] text-slate-400 font-mono ml-0.5">U</span>
                  </div>
                  <button
                    type="button"
                    aria-label="Increase insulin adjustment"
                    onclick={() => (wizardAdj = stepValue(wizardAdj, 0.1, -20, 20, 1))}
                    class="tactile-btn w-9 h-9 sm:w-10 sm:h-10 rounded-lg bg-sky-600 hover:bg-sky-500 text-white font-bold text-lg flex items-center justify-center shadow shrink-0"
                  >
                    <Plus class="size-4" />
                  </button>
                </div>
                <div class="flex gap-1 justify-center">
                  <button
                    type="button"
                    onclick={() => (wizardAdj = stepValue(wizardAdj, -0.5, -20, 20, 1))}
                    class="px-2 py-0.5 rounded bg-slate-800/80 hover:bg-slate-700 text-[9px] font-mono text-slate-300"
                  >
                    -0.5
                  </button>
                  <button
                    type="button"
                    onclick={() => (wizardAdj = 0)}
                    class="px-2 py-0.5 rounded bg-slate-800/80 hover:bg-slate-700 text-[9px] font-mono text-slate-300"
                  >
                    RESET
                  </button>
                  <button
                    type="button"
                    onclick={() => (wizardAdj = stepValue(wizardAdj, 0.5, -20, 20, 1))}
                    class="px-2 py-0.5 rounded bg-slate-800/80 hover:bg-slate-700 text-[9px] font-mono text-slate-300"
                  >
                    +0.5
                  </button>
                </div>
              </div>
            </div>

            <!-- High Contrast Clinical Transparency Breakdown Table -->
            <section
              class="bg-[#0b101c] rounded-xl border border-slate-800 p-2 text-[11px] font-mono shadow-inner mb-2"
              data-purpose="clinical-transparency-card"
            >
              <div
                class="flex justify-between items-center border-b border-slate-800 pb-1 mb-1.5"
              >
                <span
                  class="text-slate-400 font-semibold tracking-wide uppercase text-[10px]"
                >
                  Clinical Parameter
                </span>
                <span
                  class="text-slate-400 font-semibold tracking-wide uppercase text-[10px]"
                >
                  Calculated Units
                </span>
              </div>
              <div class="space-y-1">
                <div class="flex justify-between items-center text-slate-300">
                  <div class="flex items-center gap-1.5">
                    <span class="w-1.5 h-1.5 rounded-full bg-red-400"></span>
                    <span>
                      BG Correction ({bg(liveBgMgdl)} → {bg(targetBgMgdl)} {bgLabel()}):
                    </span>
                  </div>
                  <span class="font-bold text-slate-100">
                    {formatSignedUnits(wizardPreview?.bgUnits ?? 0)} U
                  </span>
                </div>

                <div class="flex justify-between items-center text-slate-300">
                  <div class="flex items-center gap-1.5">
                    <span class="w-1.5 h-1.5 rounded-full bg-amber-400"></span>
                    <span>Meal Bolus ({wizardCarbs}g):</span>
                  </div>
                  <span class="font-bold text-slate-100">
                    {formatSignedUnits(wizardPreview?.choUnits ?? 0)} U
                  </span>
                </div>

                <div class="flex justify-between items-center text-slate-300">
                  <div class="flex items-center gap-1.5">
                    <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
                    <span>Active Carbs (COB):</span>
                  </div>
                  <span class="font-bold text-emerald-300">
                    {formatSignedUnits(wizardPreview?.cobUnits ?? 0)} U
                  </span>
                </div>

                <div class="flex justify-between items-center text-slate-300">
                  <div class="flex items-center gap-1.5">
                    <span class="w-1.5 h-1.5 rounded-full bg-sky-400"></span>
                    <span>Active Insulin (IOB):</span>
                  </div>
                  <span class="font-bold text-sky-400">
                    {formatSignedUnits(wizardPreview?.iobUnits ?? 0)} U
                  </span>
                </div>

                <div class="flex justify-between items-center text-slate-300">
                  <div class="flex items-center gap-1.5">
                    <span class="w-1.5 h-1.5 rounded-full bg-purple-400"></span>
                    <span>Manual Adjustment (Adj):</span>
                  </div>
                  <span class="font-bold text-purple-300">
                    {formatSignedUnits(wizardPreview?.adjustmentUnits ?? wizardAdj)} U
                  </span>
                </div>
              </div>

              <div
                class="mt-2 pt-1.5 border-t border-slate-700/80 flex justify-between items-center text-sm font-bold"
              >
                <span class="text-white">Total Bolus:</span>
                <span class="text-sky-300 text-base">
                  {recommendedBolus.toFixed(2)} U
                </span>
              </div>
            </section>
          {:else if screen === "root"}
            <!-- Pump / Basal Mode Switcher Bar -->
            <div
              class="bg-[#101726] border border-slate-800 rounded-xl p-1.5 mb-2 flex items-center justify-between gap-2"
            >
              <div class="flex items-center gap-2 pl-1.5">
                <Syringe class="size-3.5 {pumpEnabled ? 'text-sky-400' : 'text-amber-400'}" />
                <span class="text-[11px] font-bold text-slate-200">
                  {pumpEnabled ? "Insulin Pump Active" : "Pump Disabled (MDI Basal)"}
                </span>
              </div>
              <div class="flex items-center gap-1">
                <button
                  type="button"
                  disabled={togglingPump}
                  onclick={() => void setPumpActiveState(true)}
                  class="tactile-btn px-2.5 py-1 rounded-lg text-[10px] font-mono font-bold transition-colors {pumpEnabled
                    ? 'bg-emerald-600 text-white shadow'
                    : 'bg-slate-800/80 text-slate-400 hover:text-slate-200'}"
                >
                  PUMP ON
                </button>
                <button
                  type="button"
                  disabled={togglingPump}
                  onclick={() => void setPumpActiveState(false)}
                  class="tactile-btn px-2.5 py-1 rounded-lg text-[10px] font-mono font-bold transition-colors {!pumpEnabled
                    ? 'bg-amber-600 text-white shadow'
                    : 'bg-slate-800/80 text-slate-400 hover:text-slate-200'}"
                >
                  PUMP OFF
                </button>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-2 my-1">
              <button
                type="button"
                onclick={() => (screen = "meal-menu")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 hover:border-sky-500/40 rounded-xl p-3 flex items-center justify-between text-left transition-all"
              >
                <div class="flex items-center gap-2.5">
                  <div class="p-2 rounded-lg bg-amber-500/10 text-amber-400 border border-amber-500/20">
                    <Utensils class="size-4" />
                  </div>
                  <div>
                    <div class="text-xs font-bold text-white">Meal</div>
                    <div class="text-[10px] text-slate-400">Carbs & Bolus</div>
                  </div>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "correction-bolus")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 hover:border-sky-500/40 rounded-xl p-3 flex items-center justify-between text-left transition-all"
              >
                <div class="flex items-center gap-2.5">
                  <div class="p-2 rounded-lg bg-sky-500/10 text-sky-400 border border-sky-500/20">
                    <Zap class="size-4" />
                  </div>
                  <div>
                    <div class="text-xs font-bold text-white">Correction Bolus</div>
                    <div class="text-[10px] text-slate-400">Fast Insulin</div>
                  </div>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              {#if pumpEnabled}
                <button
                  type="button"
                  onclick={() => (screen = "pump-menu")}
                  class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 hover:border-sky-500/40 rounded-xl p-3 flex items-center justify-between text-left transition-all"
                >
                  <div class="flex items-center gap-2.5">
                    <div class="p-2 rounded-lg bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                      <Syringe class="size-4" />
                    </div>
                    <div>
                      <div class="text-xs font-bold text-white">Insulin Pump</div>
                      <div class="text-[10px] text-slate-400">Basal & Reservoir</div>
                    </div>
                  </div>
                  <ChevronRight class="size-4 text-slate-500" />
                </button>
              {:else}
                <button
                  type="button"
                  onclick={() => (screen = "basal-mdi")}
                  class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-amber-500/40 hover:border-amber-400 rounded-xl p-3 flex items-center justify-between text-left transition-all"
                >
                  <div class="flex items-center gap-2.5">
                    <div class="p-2 rounded-lg bg-amber-500/10 text-amber-400 border border-amber-500/30">
                      <Syringe class="size-4" />
                    </div>
                    <div>
                      <div class="text-xs font-bold text-amber-300">Basal Menu</div>
                      <div class="text-[10px] text-slate-400">GLA • DET • TOU • DEG</div>
                    </div>
                  </div>
                  <ChevronRight class="size-4 text-amber-400" />
                </button>
              {/if}

              <button
                type="button"
                onclick={() => (screen = "medication")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 hover:border-sky-500/40 rounded-xl p-3 flex items-center justify-between text-left transition-all"
              >
                <div class="flex items-center gap-2.5">
                  <div class="p-2 rounded-lg bg-purple-500/10 text-purple-400 border border-purple-500/20">
                    <Pill class="size-4" />
                  </div>
                  <div>
                    <div class="text-xs font-bold text-white">Medication</div>
                    <div class="text-[10px] text-slate-400">Basal & Notes</div>
                  </div>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "sensor-menu")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 hover:border-sky-500/40 rounded-xl p-3 flex items-center justify-between text-left transition-all"
              >
                <div class="flex items-center gap-2.5">
                  <div class="p-2 rounded-lg bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                    <Cpu class="size-4" />
                  </div>
                  <div>
                    <div class="text-xs font-bold text-white">CGM Sensor</div>
                    <div class="text-[10px] text-slate-400">BG & Calibration</div>
                  </div>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "settings-menu")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 hover:border-sky-500/40 rounded-xl p-3 flex items-center justify-between text-left transition-all"
              >
                <div class="flex items-center gap-2.5">
                  <div class="p-2 rounded-lg bg-slate-500/10 text-slate-300 border border-slate-500/20">
                    <Settings class="size-4" />
                  </div>
                  <div>
                    <div class="text-xs font-bold text-white">Settings</div>
                    <div class="text-[10px] text-slate-400">Profile & Sim</div>
                  </div>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>
            </div>
          {:else if screen === "meal-menu"}
            <div class="flex flex-col gap-1.5 my-1">
              <button
                type="button"
                onclick={() => (screen = "wizard")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <WandSparkles class="size-4 text-sky-400" />
                  <span class="text-xs font-bold text-white">Wizard</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "food")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Utensils class="size-4 text-amber-400" />
                  <span class="text-xs font-bold text-white">Food</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "carbs-correction")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Wheat class="size-4 text-emerald-400" />
                  <span class="text-xs font-bold text-white">Carbs Correction</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "extended-bolus")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Timer class="size-4 text-purple-400" />
                  <span class="text-xs font-bold text-white">Extended Bolus</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "drink")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Martini class="size-4 text-cyan-400" />
                  <span class="text-xs font-bold text-white">Drink</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>
            </div>
          {:else if screen === "food"}
            <div class="flex flex-col gap-2 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                Food Entry
              </div>
              {@render stepperRow(
                "Carbs (g)",
                foodCarbs,
                "g",
                () => (foodCarbs = stepValue(foodCarbs, -5, 0, 300, 0)),
                () => (foodCarbs = stepValue(foodCarbs, 5, 0, 300, 0)),
                (v) => (foodCarbs = Math.max(0, Math.round(v))),
                "blue",
                5,
              )}

              {@render stepperRow(
                "Proteins (g)",
                foodProteins,
                "g",
                () => (foodProteins = stepValue(foodProteins, -5, 0, 300, 0)),
                () => (foodProteins = stepValue(foodProteins, 5, 0, 300, 0)),
                (v) => (foodProteins = Math.max(0, Math.round(v))),
                "gray",
                5,
              )}

              {@render stepperRow(
                "Fats (g)",
                foodFats,
                "g",
                () => (foodFats = stepValue(foodFats, -5, 0, 300, 0)),
                () => (foodFats = stepValue(foodFats, 5, 0, 300, 0)),
                (v) => (foodFats = Math.max(0, Math.round(v))),
                "gray",
                5,
              )}
            </div>
          {:else if screen === "carbs-correction"}
            <div class="flex flex-col gap-2 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                Carbs Correction
              </div>
              {@render stepperRow(
                "Fast Carbs (g)",
                fastCarbs,
                "g",
                () => (fastCarbs = stepValue(fastCarbs, -5, 0, 200, 0)),
                () => (fastCarbs = stepValue(fastCarbs, 5, 0, 200, 0)),
                (v) => (fastCarbs = Math.max(0, Math.round(v))),
                "blue",
                5,
              )}
            </div>
          {:else if screen === "extended-bolus"}
            <div class="flex flex-col gap-2 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                Extended Bolus
              </div>
              {@render stepperRow(
                "Insulin (U)",
                extBolusUnits,
                "U",
                () => (extBolusUnits = stepValue(extBolusUnits, -0.5, 0, 50, 1)),
                () => (extBolusUnits = stepValue(extBolusUnits, 0.5, 0, 50, 1)),
                (v) => (extBolusUnits = Math.max(0, v)),
                "blue",
                0.5,
              )}

              {@render stepperRow(
                "Carbs (g)",
                extBolusCarbs,
                "g",
                () => (extBolusCarbs = stepValue(extBolusCarbs, -5, 0, 300, 0)),
                () => (extBolusCarbs = stepValue(extBolusCarbs, 5, 0, 300, 0)),
                (v) => (extBolusCarbs = Math.max(0, Math.round(v))),
                "blue",
                5,
              )}

              {@render stepperRow(
                "Duration (min)",
                extBolusDuration,
                "m",
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
            <div class="flex flex-col gap-2 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                Drink
              </div>
              {@render stepperRow(
                "Carbs (g)",
                drinkCarbs,
                "g",
                () => (drinkCarbs = stepValue(drinkCarbs, -5, 0, 200, 0)),
                () => (drinkCarbs = stepValue(drinkCarbs, 5, 0, 200, 0)),
                (v) => (drinkCarbs = Math.max(0, Math.round(v))),
                "blue",
                5,
              )}
            </div>
          {:else if screen === "correction-bolus"}
            <div class="flex flex-col gap-2 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                Correction Bolus
              </div>
              {@render stepperRow(
                "Insulin (U)",
                correctionBolusUnits,
                "U",
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
            </div>
          {:else if screen === "pump-menu"}
            <div class="flex flex-col gap-2 my-1">
              <div
                class="bg-[#101726] border border-slate-800 rounded-xl p-2 flex items-center justify-between"
              >
                <div>
                  <div class="text-xs font-bold text-emerald-400">Pump Mode: ACTIVE</div>
                  <div class="text-[10px] text-slate-400">
                    Continuous basal infusion ({activeBasalRate} U/h)
                  </div>
                </div>
                <button
                  type="button"
                  disabled={togglingPump}
                  onclick={() => void setPumpActiveState(false)}
                  class="tactile-btn px-2.5 py-1.5 rounded-lg bg-amber-600/20 hover:bg-amber-600/30 text-amber-300 border border-amber-500/40 text-[10px] font-mono font-bold"
                >
                  Disable Pump (MDI)
                </button>
              </div>

              <button
                type="button"
                onclick={() => (screen = "basal-profile")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-3 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Calendar class="size-4 text-sky-400" />
                  <span class="text-xs font-bold text-white">Basal Profile</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "temp-basal")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-3 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Hourglass class="size-4 text-amber-400" />
                  <span class="text-xs font-bold text-white">Temp Basal</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => (screen = "refill-reservoir")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-3 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Droplet class="size-4 text-cyan-400" />
                  <span class="text-xs font-bold text-white">Refill Reservoir</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>
            </div>
          {:else if screen === "basal-mdi"}
            <div class="flex flex-col gap-2 my-1">
              <div
                class="bg-[#101726] border border-amber-500/30 rounded-xl p-2 flex items-center justify-between"
              >
                <div>
                  <div class="text-xs font-bold text-amber-400">
                    Pump Disabled • Basal Menu (MDI)
                  </div>
                  <div class="text-[10px] text-slate-400">
                    Select one of the 4 long-acting basal insulins
                  </div>
                </div>
                <button
                  type="button"
                  disabled={togglingPump}
                  onclick={() => void setPumpActiveState(true)}
                  class="tactile-btn px-2.5 py-1.5 rounded-lg bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/40 text-[10px] font-mono font-bold"
                >
                  Enable Pump
                </button>
              </div>

              <!-- 4 Basal Insulin Types Selector -->
              <div class="grid grid-cols-2 gap-1.5">
                {#each basalInsulinTypes as item (item.code)}
                  <button
                    type="button"
                    onclick={() => (selectedBasalDrug = item.code)}
                    class="tactile-btn rounded-xl p-2.5 border text-left transition-all flex flex-col justify-between {selectedBasalDrug ===
                    item.code
                      ? 'bg-sky-600/25 border-sky-400 text-white shadow'
                      : 'bg-[#101726] border-slate-800 text-slate-300 hover:bg-slate-800/70'}"
                  >
                    <div class="flex items-center justify-between w-full mb-0.5">
                      <span
                        class="text-xs font-mono font-black {selectedBasalDrug ===
                        item.code
                          ? 'text-sky-300'
                          : 'text-slate-200'}"
                      >
                        {item.code}
                      </span>
                      <span
                        class="text-[9px] font-mono px-1.5 py-0.5 rounded bg-black/50 text-slate-400"
                      >
                        {item.kinetics}
                      </span>
                    </div>
                    <div class="text-xs font-bold text-white">{item.label}</div>
                    <div class="text-[10px] text-slate-400">{item.brand}</div>
                  </button>
                {/each}
              </div>

              {@render stepperRow(
                `Basal Dose (${selectedBasalDrug})`,
                basalMdiUnits,
                "U",
                () => (basalMdiUnits = stepValue(basalMdiUnits, -1, 1, 100, 0)),
                () => (basalMdiUnits = stepValue(basalMdiUnits, 1, 1, 100, 0)),
                (v) => (basalMdiUnits = Math.max(1, Math.round(v))),
                "blue",
                1,
              )}

              <div class="flex gap-1.5 justify-center">
                {#each [10, 15, 20, 25] as preset (preset)}
                  <button
                    type="button"
                    onclick={() => (basalMdiUnits = preset)}
                    class="px-2.5 py-1 rounded-lg bg-slate-800/80 hover:bg-slate-700 text-[10px] font-mono text-slate-200 border border-slate-700/60"
                  >
                    {preset}U
                  </button>
                {/each}
              </div>
            </div>
          {:else if screen === "basal-profile"}
            <div class="flex flex-col gap-1.5 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                Basal Profile ({activeProfileName})
              </div>
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
                {@render stepperRow(
                  "Morning 06-11",
                  basalMorning,
                  "U/h",
                  () => (basalMorning = stepValue(basalMorning, -0.05, 0, 20, 2)),
                  () => (basalMorning = stepValue(basalMorning, 0.05, 0, 20, 2)),
                  (v) => (basalMorning = Math.max(0, v)),
                  "blue",
                  0.05,
                )}

                {@render stepperRow(
                  "Afternoon 11-18",
                  basalAfternoon,
                  "U/h",
                  () => (basalAfternoon = stepValue(basalAfternoon, -0.05, 0, 20, 2)),
                  () => (basalAfternoon = stepValue(basalAfternoon, 0.05, 0, 20, 2)),
                  (v) => (basalAfternoon = Math.max(0, v)),
                  "blue",
                  0.05,
                )}

                {@render stepperRow(
                  "Evening 18-23",
                  basalEvening,
                  "U/h",
                  () => (basalEvening = stepValue(basalEvening, -0.05, 0, 20, 2)),
                  () => (basalEvening = stepValue(basalEvening, 0.05, 0, 20, 2)),
                  (v) => (basalEvening = Math.max(0, v)),
                  "blue",
                  0.05,
                )}

                {@render stepperRow(
                  "Night 23-06",
                  basalNight,
                  "U/h",
                  () => (basalNight = stepValue(basalNight, -0.05, 0, 20, 2)),
                  () => (basalNight = stepValue(basalNight, 0.05, 0, 20, 2)),
                  (v) => (basalNight = Math.max(0, v)),
                  "blue",
                  0.05,
                )}
              </div>
            </div>
          {:else if screen === "temp-basal"}
            <div class="flex flex-col gap-2 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                Temp Basal
              </div>
              {@render stepperRow(
                "Rate (U/h)",
                tempBasalRate,
                "U/h",
                () => (tempBasalRate = stepValue(tempBasalRate, -0.1, 0, 20, 1)),
                () => (tempBasalRate = stepValue(tempBasalRate, 0.1, 0, 20, 1)),
                (v) => (tempBasalRate = Math.max(0, v)),
                "blue",
                0.1,
              )}

              {@render stepperRow(
                "Duration (min)",
                tempBasalDuration,
                "m",
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
            <div class="flex flex-col items-center gap-2.5 my-1 bg-[#101726] border border-slate-800 rounded-xl p-3">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider">
                Refill Reservoir
              </div>
              <span class="text-xs text-slate-400">Reservoir Level:</span>
              <div
                class="w-40 py-2 rounded-lg border border-sky-500/30 bg-black/60 text-center text-base font-mono font-bold text-sky-300"
              >
                {currentReservoir != null ? `${currentReservoir.toFixed(0)}U` : "N/A"}
              </div>

              <span class="text-xs text-slate-400 mt-1">Select new capacity:</span>
              <div class="flex items-center justify-center gap-2 w-full">
                {#each [100, 200, 300] as const as cap (cap)}
                  <button
                    type="button"
                    onclick={() => (reservoirCapacity = cap)}
                    class="tactile-btn px-3.5 py-1.5 rounded-lg font-mono text-xs font-bold border transition-colors {reservoirCapacity ===
                    cap
                      ? 'bg-sky-600 text-white border-sky-400 shadow'
                      : 'bg-slate-900 text-slate-300 border-slate-800 hover:bg-slate-800'}"
                  >
                    {cap}U
                  </button>
                {/each}
              </div>
            </div>
          {:else if screen === "medication"}
            <div class="flex flex-col gap-2 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                Medication
              </div>
              {@render stepperRow(
                "Long-Acting Insulin (U)",
                medicationUnits,
                "U",
                () => (medicationUnits = stepValue(medicationUnits, -1, 0, 100, 0)),
                () => (medicationUnits = stepValue(medicationUnits, 1, 0, 100, 0)),
                (v) => (medicationUnits = Math.max(0, v)),
                "blue",
                1,
              )}

              <div class="bg-[#101726] border border-slate-800 rounded-xl p-2.5 flex flex-col gap-1.5">
                <Label for="pump-med-note" size="sm" class="text-xs text-slate-300">
                  Medication Note
                </Label>
                <Input
                  id="pump-med-note"
                  type="text"
                  bind:value={medicationNote}
                  placeholder="Lantus, Metformin..."
                  class="w-full bg-black/60 border-slate-800 text-white text-xs"
                />
              </div>
            </div>
          {:else if screen === "sensor-menu"}
            <div class="flex flex-col gap-2 my-1">
              <button
                type="button"
                onclick={() => (screen = "bg-check")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Droplet class="size-4 text-red-400" />
                  <span class="text-xs font-bold text-white">BG Check / Calibration</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() =>
                  recordSensorEvent(DeviceEventType.SensorStart, "Sensor Start")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Play class="size-4 text-emerald-400" />
                  <span class="text-xs font-bold text-white">Start Sensor</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() =>
                  recordSensorEvent(DeviceEventType.SensorChange, "Sensor Change")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <RefreshCw class="size-4 text-sky-400" />
                  <span class="text-xs font-bold text-white">Change Sensor</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() =>
                  recordSensorEvent(DeviceEventType.SensorStop, "Sensor Stop")}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Square class="size-4 text-amber-400" />
                  <span class="text-xs font-bold text-white">Stop Sensor</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>
            </div>
          {:else if screen === "bg-check"}
            <div class="flex flex-col gap-2 my-1">
              <div class="text-xs font-bold text-sky-400 uppercase tracking-wider px-1">
                BG Check
              </div>
              {@render stepperRow(
                `Glucose (${bgLabel()})`,
                bgCheckValue,
                bgLabel(),
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
            </div>
          {:else if screen === "settings-menu"}
            <div class="flex flex-col gap-2 my-1">
              <button
                type="button"
                onclick={() => {
                  open = false;
                  goto(resolve("/(authenticated)/settings/patient"));
                }}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Settings class="size-4 text-sky-400" />
                  <span class="text-xs font-bold text-white">Patient Record</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => {
                  open = false;
                  goto(resolve("/(authenticated)/cgmsim/parameters"));
                }}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <SlidersHorizontal class="size-4 text-amber-400" />
                  <span class="text-xs font-bold text-white">Parameters (CGMSIM)</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => {
                  open = false;
                  goto(resolve("/(authenticated)/settings/profile"));
                }}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Calendar class="size-4 text-emerald-400" />
                  <span class="text-xs font-bold text-white">Therapy Profile</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>

              <button
                type="button"
                onclick={() => {
                  open = false;
                  goto(resolve("/(authenticated)/settings/trackers"));
                }}
                class="tactile-btn bg-[#101726] hover:bg-slate-800/90 border border-slate-800 rounded-xl p-2.5 flex items-center justify-between text-left"
              >
                <div class="flex items-center gap-2.5">
                  <Droplet class="size-4 text-cyan-400" />
                  <span class="text-xs font-bold text-white">Trackers & Consumables</span>
                </div>
                <ChevronRight class="size-4 text-slate-500" />
              </button>
            </div>
          {/if}

          <!-- Dual Action / Hardware Deliver Controls -->
          {#if !deliveringBolus}
            <footer
              class="pt-1 select-none flex flex-col gap-2"
              data-purpose="safety-confirmation-actions"
            >
              <div class="grid grid-cols-3 gap-2">
                <button
                  type="button"
                  onclick={handleBackOrCancel}
                  class="tactile-btn bg-slate-800/90 hover:bg-slate-700 text-slate-300 hover:text-white border border-slate-700 font-bold py-2.5 px-3 rounded-xl shadow flex items-center justify-center gap-1.5 text-xs transition-colors"
                >
                  {#if parentScreen[screen] === null}
                    <CircleX class="size-3.5 text-slate-400" />
                    <span>Cancel</span>
                  {:else}
                    <ArrowLeft class="size-3.5 text-slate-400" />
                    <span>Back</span>
                  {/if}
                </button>

                <button
                  type="button"
                  disabled={!canConfirm}
                  onclick={handlePrimaryAction}
                  class="tactile-btn col-span-2 font-black py-2.5 px-3 rounded-xl flex items-center justify-center gap-1.5 text-xs sm:text-sm tracking-wide transition-all {canConfirm
                    ? 'bg-gradient-to-b from-emerald-400 to-emerald-600 hover:from-emerald-300 hover:to-emerald-500 text-slate-950 shadow-glow-emerald border border-emerald-300/40 active:scale-95'
                    : 'bg-slate-900 text-slate-600 border border-slate-800 cursor-not-allowed'}"
                >
                  {#if requiresSafetyConfirmation}
                    <Droplet class="size-3.5 fill-current" />
                    <span>Confirm and Deliver {pendingBolusUnits.toFixed(2)} U</span>
                  {:else}
                    <Check class="size-4" />
                    <span>Save Entry</span>
                  {/if}
                </button>
              </div>
            </footer>
          {/if}

          <!-- Step 2/2 Bolus Safety Confirmation Overlay -->
          {#if confirmingBolus}
            <div
              class="absolute inset-0 bg-[#080e1b]/95 backdrop-blur-md z-50 flex items-center justify-center p-3"
              data-purpose="bolus-safety-confirmation-dialog"
            >
              <div
                class="bg-[#101726] border border-sky-500/40 rounded-2xl w-full p-3.5 shadow-2xl flex flex-col justify-between text-center relative"
              >
                <div
                  class="flex items-center justify-between pb-2 mb-2 border-b border-slate-800/80"
                >
                  <div class="flex items-center gap-1.5">
                    <span class="w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span>
                    <span
                      class="text-[10px] font-mono font-bold tracking-wider text-amber-400 uppercase"
                    >
                      CAUTION • CLINICAL SAFETY
                    </span>
                  </div>
                  <span class="text-[10px] font-mono text-slate-400">STEP 2/2</span>
                </div>

                <div class="my-1">
                  <div
                    class="w-10 h-10 mx-auto rounded-full bg-sky-500/10 border border-sky-400/30 flex items-center justify-center text-sky-400 mb-1.5 shadow-inner"
                  >
                    <ShieldAlert class="size-5" />
                  </div>
                  <h3 class="text-sm font-bold text-white uppercase tracking-wider mb-0.5">
                    Confirm Bolus Delivery
                  </h3>
                  <p class="text-xs text-slate-300 font-sans">
                    Start rapid-acting insulin delivery?
                  </p>
                </div>

                <div
                  class="bg-[#090e18] border border-slate-800 rounded-xl p-2.5 my-2 text-center shadow-inner"
                >
                  <div class="text-2xl sm:text-3xl font-extrabold font-mono text-sky-300 tracking-tight">
                    {pendingBolusUnits.toFixed(2)}
                    <span class="text-sm font-bold text-sky-400">U</span>
                  </div>
                  <div
                    class="mt-2 pt-2 border-t border-slate-800/80 grid grid-cols-3 gap-1 text-[10px] font-mono text-slate-400"
                  >
                    <div>
                      <span class="block text-slate-500">Correction</span>
                      <span class="text-slate-200 font-semibold">
                        {formatSignedUnits(wizardPreview?.bgUnits ?? pendingBolusUnits, 2)} U
                      </span>
                    </div>
                    <div>
                      <span class="block text-slate-500">Carbs</span>
                      <span class="text-slate-200 font-semibold">{pendingCarbsGrams} g</span>
                    </div>
                    <div>
                      <span class="block text-slate-500">Active IOB</span>
                      <span class="text-sky-400 font-semibold">
                        {formatSignedUnits(wizardPreview?.iobUnits ?? 0, 2)} U
                      </span>
                    </div>
                  </div>
                </div>

                <p
                  class="text-[10px] font-mono text-amber-300/90 mb-2.5 flex items-center justify-center gap-1.5"
                >
                  <AlertTriangle class="size-3.5 shrink-0 text-amber-400" />
                  <span>CAUTION: Check the target glucose before you confirm.</span>
                </p>

                <div class="grid grid-cols-2 gap-2 pt-0.5">
                  <button
                    type="button"
                    onclick={() => (confirmingBolus = false)}
                    class="tactile-btn py-2.5 px-3 rounded-xl bg-slate-800/90 hover:bg-slate-700 text-slate-300 hover:text-white border border-slate-700 font-bold text-xs tracking-wide transition-colors flex items-center justify-center gap-1"
                  >
                    <CircleX class="size-3.5 text-slate-400" />
                    <span>Cancel</span>
                  </button>
                  <button
                    type="button"
                    onclick={startBolusDelivery}
                    class="tactile-btn py-2.5 px-3 rounded-xl bg-gradient-to-b from-emerald-400 to-emerald-600 hover:from-emerald-300 hover:to-emerald-500 text-slate-950 font-black text-xs sm:text-sm tracking-wide shadow-glow-emerald border border-emerald-300/40 transition-all flex items-center justify-center gap-1.5 active:scale-95"
                  >
                    <Droplet class="size-3.5 fill-current" />
                    <span>Confirm and Deliver</span>
                  </button>
                </div>
              </div>
            </div>
          {/if}
        </div>
      </div>

      <!-- Bottom Hardware Elements: USB-C & Serial Number -->
      <div
        class="flex justify-between items-center px-8 sm:px-10 py-1 text-[9px] font-mono text-slate-500"
      >
        <span>SN: NC-8849-01</span>
        <div
          class="w-8 h-1 bg-slate-700/60 rounded-full"
          title="USB-C Charging and Data Interface"
        ></div>
        <span>MED-DEVICE CE 0459</span>
      </div>
    </div>
  </Dialog.Content>
</Dialog.Root>

<style>
  .oled-display {
    background: radial-gradient(ellipse at 50% 20%, #0d1527 0%, #05080f 85%);
  }
  .titanium-edge {
    background: linear-gradient(145deg, #2b3548 0%, #151a24 45%, #0b0e14 100%);
  }
  .shadow-chassis {
    box-shadow:
      0 25px 60px -15px rgba(0, 0, 0, 0.9),
      0 0 0 1px rgba(255, 255, 255, 0.08),
      inset 0 1px 1px rgba(255, 255, 255, 0.2);
  }
  .shadow-bezel {
    box-shadow:
      inset 0 2px 6px rgba(0, 0, 0, 0.8),
      inset 0 -1px 2px rgba(255, 255, 255, 0.05);
  }
  .shadow-glow-emerald {
    box-shadow: 0 0 22px -2px rgba(16, 185, 129, 0.45);
  }
  .tactile-btn {
    transition: all 0.15s cubic-bezier(0.4, 0, 0.2, 1);
  }
  .tactile-btn:active {
    transform: scale(0.97);
    filter: brightness(0.92);
  }
</style>
