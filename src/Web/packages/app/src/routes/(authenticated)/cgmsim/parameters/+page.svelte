<script lang="ts">
  import * as Card from "#lib/components/ui/card/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import { Label } from "#lib/components/ui/label/index.js";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import Save from "@lucide/svelte/icons/save";
  import { toast } from "svelte-sonner";
  import { describeSubmitError } from "#lib/forms/index.js";
  import { getParameters, updateParameters } from "#lib/api/generated/cgmSims.generated.remote.js";

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

    const isfNum = Number(isf);
    if (!isf || isNaN(isfNum) || isfNum < 1 || isfNum > 1000) {
      toast.error("Enter an ISF between 1 and 1000 mg/dL/U.");
      return;
    }

    const crNum = Number(cr);
    if (!cr || isNaN(crNum) || crNum < 0.1 || crNum > 200) {
      toast.error("Enter a carb ratio between 0.1 and 200 g/U.");
      return;
    }

    const diaNum = Number(dia);
    if (!dia || isNaN(diaNum) || diaNum < 0.5 || diaNum > 24) {
      toast.error("Enter a DIA between 0.5 and 24 hours.");
      return;
    }

    const carbsNum = Number(carbsAbsTime);
    if (!carbsAbsTime || isNaN(carbsNum) || carbsNum < 5 || carbsNum > 1440) {
      toast.error("Enter a carb absorption time between 5 and 1440 minutes.");
      return;
    }

    const peakNum = Number(insulinPeak);
    if (!insulinPeak || isNaN(peakNum) || peakNum < 5 || peakNum > 360) {
      toast.error("Enter an insulin peak time between 5 and 360 minutes.");
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

      toast.success("Simulation parameters saved successfully.");
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
      <Card.Title>Simulation Parameters</Card.Title>
    </div>
    <Card.Description>
      Configure metabolic and kinetic parameters for the CGM simulator. These settings operate independently from your therapy profiles.
    </Card.Description>
  </Card.Header>

  <form onsubmit={handleSave}>
    <Card.Content class="space-y-6">
      <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <!-- ISF -->
        <div class="space-y-2">
          <Label for="param-isf">Insulin Sensitivity Factor (ISF) <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="param-isf"
              type="number"
              step="0.1"
              min="1"
              max="1000"
              placeholder="e.g. 50"
              required
              bind:value={isf}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              mg/dL/U
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Blood glucose drop per unit of insulin.</p>
        </div>

        <!-- Carb Ratio -->
        <div class="space-y-2">
          <Label for="param-cr">Carb Ratio (CR) <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="param-cr"
              type="number"
              step="0.1"
              min="0.1"
              max="200"
              placeholder="e.g. 10"
              required
              bind:value={cr}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              g/U
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Grams of carbohydrates covered by one unit of insulin.</p>
        </div>

        <!-- DIA -->
        <div class="space-y-2">
          <Label for="param-dia">Duration of Insulin Action (DIA) <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="param-dia"
              type="number"
              step="0.1"
              min="0.5"
              max="24"
              placeholder="e.g. 4.0"
              required
              bind:value={dia}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              hours
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Active duration of rapid-acting insulin.</p>
        </div>

        <!-- Carb Absorption Time -->
        <div class="space-y-2">
          <Label for="param-carbs-abs">Carb Absorption Time <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="param-carbs-abs"
              type="number"
              step="1"
              min="5"
              max="1440"
              placeholder="e.g. 180"
              required
              bind:value={carbsAbsTime}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              min
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Total duration of meal carb absorption curve.</p>
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
              max="360"
              placeholder="e.g. 75"
              required
              bind:value={insulinPeak}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              min
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Time in minutes when rapid-acting insulin reaches peak activity.</p>
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
          Save Parameters
        {/if}
      </Button>
    </Card.Footer>
  </form>
</Card.Root>
