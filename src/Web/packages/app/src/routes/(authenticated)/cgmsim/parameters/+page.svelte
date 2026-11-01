<script lang="ts">
  import * as Card from "#lib/components/ui/card/index.js";
  import { Button, buttonVariants } from "#lib/components/ui/button/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import { Label } from "#lib/components/ui/label/index.js";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import Save from "@lucide/svelte/icons/save";
  import ExternalLink from "@lucide/svelte/icons/external-link";
  import ShieldCheck from "@lucide/svelte/icons/shield-check";
  import { toast } from "svelte-sonner";
  import { describeSubmitError } from "#lib/forms/index.js";
  import { getParameters, updateParameters } from "#lib/api/generated/cgmSims.generated.remote.js";
  import { resolve } from "$app/paths";

  const paramsQuery = getParameters();
  const paramsData = $derived(paramsQuery.current);

  let isf = $state<string>("");
  let dia = $state<string>("");
  let carbsAbsTime = $state<string>("");
  let insulinPeak = $state<string>("");
  let cr = $state<string>("");
  let isSaving = $state(false);
  let initialized = $state(false);

  $effect(() => {
    if (paramsData && !initialized) {
      isf = paramsData.isf !== undefined && paramsData.isf !== null ? paramsData.isf.toString() : "";
      dia = paramsData.dia !== undefined && paramsData.dia !== null ? paramsData.dia.toString() : "";
      carbsAbsTime = paramsData.carbsAbsTime !== undefined && paramsData.carbsAbsTime !== null ? paramsData.carbsAbsTime.toString() : "";
      insulinPeak = paramsData.insulinPeak !== undefined && paramsData.insulinPeak !== null ? paramsData.insulinPeak.toString() : "";
      cr = paramsData.cr !== undefined && paramsData.cr !== null ? paramsData.cr.toString() : "";
      initialized = true;
    }
  });

  async function handleSave(e: SubmitEvent) {
    e.preventDefault();

    const isfNum = Number(isf) || 50;
    const diaNum = Number(dia) || 4;
    const crNum = Number(cr) || 10;

    const carbsNum = Number(carbsAbsTime);
    if (!carbsAbsTime || isNaN(carbsNum) || carbsNum <= 0) {
      toast.error("Enter a valid carb absorption time.");
      return;
    }

    const peakNum = Number(insulinPeak);
    if (!insulinPeak || isNaN(peakNum) || peakNum <= 0) {
      toast.error("Enter a valid insulin peak time.");
      return;
    }

    isSaving = true;

    try {
      await updateParameters({
        isf: isfNum,
        dia: diaNum,
        carbsAbsTime: Math.round(carbsNum),
        insulinPeak: Math.round(peakNum),
        cr: crNum,
      });

      toast.success("Simulation kinetics saved successfully.");
    } catch (err) {
      toast.error(describeSubmitError(err, "Failed to save simulation parameters."));
    } finally {
      isSaving = false;
    }
  }
</script>

<svelte:head>
  <title>Parameters - CGMSIM - Nocturne</title>
</svelte:head>

<Card.Root>
  <Card.Header>
    <div class="flex items-center gap-2">
      <SlidersHorizontal class="h-5 w-5 text-primary" />
      <Card.Title>Metabolic & Kinetic Parameters</Card.Title>
    </div>
    <Card.Description>
      Tune insulin kinetics and carb absorption dynamics. Clinical sensitivity and duration are synchronized directly from your Therapy Profile.
    </Card.Description>
  </Card.Header>

  <form onsubmit={handleSave}>
    <Card.Content class="space-y-6">
      <!-- Section 1: Therapy Profile Parameters (Read-only source of truth) -->
      <div class="rounded-lg border bg-muted/40 p-4 space-y-4">
        <div class="flex items-center justify-between gap-2">
          <div class="flex items-center gap-2">
            <ShieldCheck class="h-4 w-4 text-primary" />
            <span class="text-sm font-medium">Therapy Profile (Source of Truth)</span>
          </div>
          <a
            href={resolve("/(authenticated)/settings/profile")}
            class={buttonVariants({ variant: "ghost", size: "sm" })}
          >
            Edit in Profile Settings
            <ExternalLink class="ml-1.5 h-3.5 w-3.5" />
          </a>
        </div>
        <p class="text-xs text-muted-foreground">
          The simulator actively respects your schedule profiles for ISF, CR, DIA and Basal rates set in your Therapy Profile.
        </p>

        <div class="grid gap-4 sm:grid-cols-3">
          <div class="space-y-1">
            <Label size="sm" variant="muted">ISF (Sensitivity)</Label>
            <div class="text-base font-semibold">{isf || "—"} <span class="text-xs font-normal text-muted-foreground">mg/dL/U</span></div>
          </div>
          <div class="space-y-1">
            <Label size="sm" variant="muted">Carb Ratio (CR)</Label>
            <div class="text-base font-semibold">{cr || "—"} <span class="text-xs font-normal text-muted-foreground">g/U</span></div>
          </div>
          <div class="space-y-1">
            <Label size="sm" variant="muted">DIA (Insulin Action)</Label>
            <div class="text-base font-semibold">{dia || "—"} <span class="text-xs font-normal text-muted-foreground">hours</span></div>
          </div>
        </div>
      </div>

      <!-- Section 2: Simulator Kinetic Dynamics -->
      <div class="grid gap-6 sm:grid-cols-2">
        <!-- Carbs Absorption Time -->
        <div class="space-y-2">
          <Label for="param-carbs-abs">Carb Absorption Time <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="param-carbs-abs"
              type="number"
              step="1"
              min="5"
              max="720"
              placeholder="e.g. 180"
              required
              bind:value={carbsAbsTime}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              min
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Total duration of meal carb absorption curve in the physiological engine.</p>
        </div>

        <!-- Insulin Peak -->
        <div class="space-y-2">
          <Label for="param-insulin-peak">Insulin Peak Time <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="param-insulin-peak"
              type="number"
              step="1"
              min="5"
              max="240"
              placeholder="e.g. 75"
              required
              bind:value={insulinPeak}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              min
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Time in minutes when rapid-acting insulin reaches peak activity (for example: 55m Fiasp/Lyumjev, 75m Novorapid/Humalog).</p>
        </div>
      </div>
    </Card.Content>

    <Card.Footer class="border-t justify-end">
      <Button type="submit" disabled={isSaving} class="min-w-32">
        {#if isSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
          Saving...
        {:else}
          <Save class="mr-2 h-4 w-4" />
          Save Kinetics
        {/if}
      </Button>
    </Card.Footer>
  </form>
</Card.Root>
