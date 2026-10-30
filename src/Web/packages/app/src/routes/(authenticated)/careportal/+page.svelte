<script lang="ts">
  import ArrowRight from "@lucide/svelte/icons/arrow-right";
  import Droplet from "@lucide/svelte/icons/droplet";
  import Plus from "@lucide/svelte/icons/plus";
  import { Button } from "#lib/components/ui/button/index.js";
  import { GlucoseValueIndicator } from "#lib/components/shared/index.js";
  import GlucoseChart from "#lib/components/dashboard/glucose-chart/GlucoseChart.svelte";
  import CareportalPumpDialog from "#lib/components/careportal/CareportalPumpDialog.svelte";
  import {
    glucoseChartLookback,
    glucoseUnits,
    predictionEnabled,
  } from "#lib/stores/appearance-store.svelte.js";
  import { displayedGlucose } from "#lib/stores/current-glucose-status.svelte.js";
  import { getRealtimeStore } from "#lib/stores/realtime-store.svelte.js";
  import { STALE_THRESHOLD_MS } from "#lib/constants/staleness.js";
  import {
    formatGlucoseDelta,
    formatGlucoseValue,
    getUnitLabel,
    relativeAge,
  } from "#lib/utils/formatting.js";
  import { getGlucoseTileVariant } from "#lib/utils/glucose-status.js";
  import { trendAngle } from "@nocturne/ui/glucose";
  import type { PageData } from "./$types";

  let { data }: { data: PageData } = $props();

  const realtimeStore = getRealtimeStore();
  let pumpDialogOpen = $state(false);

  const units = $derived(glucoseUnits.current);
  const unitLabel = $derived(getUnitLabel(units));
  const glucose = displayedGlucose(realtimeStore);
  const tileVariant = $derived(getGlucoseTileVariant(glucose.status));
  const lastUpdated = $derived(realtimeStore.lastUpdated);
  const now = $derived(realtimeStore.now);
  const isLoading = $derived(
    glucose.currentBG === 0 && realtimeStore.entries.length === 0
  );
  const isStale = $derived(now - lastUpdated > STALE_THRESHOLD_MS);
  const isDisconnected = $derived(realtimeStore.connectionUnavailable);

  const currentReservoir = $derived(realtimeStore.currentReservoir);
  const arrowDegrees = $derived(trendAngle(glucose.bgDelta ?? 0));
  const isDoubleTrend = $derived(Math.abs(glucose.bgDelta ?? 0) >= 10);
  const readingAge = $derived(
    relativeAge(lastUpdated, { now, fallback: "Live CGM" })
  );
</script>

<svelte:head>
  <title>Careportal - Nocturne</title>
</svelte:head>

{#snippet trend()}
  <span class="ml-1 flex items-center gap-1.5">
    {#if !isStale}
      <span
        class="inline-flex h-8 min-w-8 items-center justify-center rounded-md bg-black/15 px-1.5"
        title="Glucose trend direction"
      >
        <span class="inline-flex items-center -space-x-1.5">
          <ArrowRight
            class="size-5 sm:size-6 stroke-[2.75] transition-transform duration-500"
            style="transform: rotate({arrowDegrees}deg)"
            aria-hidden="true"
          />
          {#if isDoubleTrend}
            <ArrowRight
              class="size-5 sm:size-6 stroke-[2.75] transition-transform duration-500"
              style="transform: rotate({arrowDegrees}deg)"
              aria-hidden="true"
            />
          {/if}
        </span>
      </span>
    {/if}
    <span class="hidden sm:inline text-xs font-semibold uppercase opacity-90">
      {unitLabel}
    </span>
  </span>
{/snippet}

<div
  class="relative flex h-[calc(100dvh-var(--app-sticky-top,0px))] w-full flex-col gap-2 overflow-hidden p-2 @md:p-3 select-none"
>
  <!-- Prominent Current Glucose & Trend Arrows Header Bar -->
  <div
    class="flex items-center justify-between gap-2 sm:gap-4 rounded-xl border border-border bg-card text-card-foreground px-3 py-2 sm:px-4 sm:py-2.5 shadow-sm"
  >
    <div class="flex min-w-0 items-center gap-2.5 sm:gap-3.5">
      <GlucoseValueIndicator
        displayValue={formatGlucoseValue(glucose.currentBG, units)}
        variant={tileVariant}
        {isLoading}
        {isStale}
        {isDisconnected}
        size="lg"
        trailing={isLoading ? undefined : trend}
      />

      <div class="flex min-w-0 flex-col justify-center">
        <span
          class="font-mono text-sm sm:text-base font-bold leading-tight text-foreground tabular-nums truncate"
        >
          {formatGlucoseDelta(glucose.bgDelta, units)} {unitLabel}
        </span>
        <span
          class="text-xs font-mono text-muted-foreground tabular-nums truncate"
        >
          {readingAge}
        </span>
      </div>
    </div>

    <!-- Active Therapy Telemetry & Quick PDM Trigger -->
    <div class="ml-auto flex shrink-0 items-center gap-2 sm:gap-3">
      {#if currentReservoir != null}
        <div
          class="hidden md:flex items-center gap-1.5 rounded-lg border border-border bg-muted/60 px-3 py-1.5 font-mono text-xs"
        >
          <Droplet class="size-3.5 text-primary" />
          <span class="text-muted-foreground">RES:</span>
          <span class="font-bold text-foreground">{currentReservoir.toFixed(0)}U</span>
        </div>
      {/if}

      <Button
        variant="default"
        size="default"
        onclick={() => (pumpDialogOpen = true)}
        aria-label="Open Careportal Pump Menu"
        data-testid="careportal-fab-button"
        class="shrink-0 rounded-lg font-mono text-xs font-bold uppercase tracking-wider shadow-sm gap-1.5 px-3 sm:px-4"
      >
        <Plus class="size-4 stroke-[2.5]" />
        <span>PDM Touch</span>
      </Button>
    </div>
  </div>

  <div class="min-h-0 flex-1 w-full">
    <GlucoseChart
      heightClass="h-full"
      focusHours={glucoseChartLookback.current}
      initialChartData={data.initialChartData}
      initialWindowStart={data.initialWindowStart}
      streamedHistoricalData={data.streamed?.historicalChartData}
      enablePredictions={predictionEnabled.current}
      demoMode={realtimeStore.demoMode}
    />
  </div>
</div>

<CareportalPumpDialog
  bind:open={pumpDialogOpen}
  {currentReservoir}
/>
