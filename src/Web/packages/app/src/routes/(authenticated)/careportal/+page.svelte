<script lang="ts">
  import ArrowRight from "@lucide/svelte/icons/arrow-right";
  import Droplet from "@lucide/svelte/icons/droplet";
  import Plus from "@lucide/svelte/icons/plus";
  import { Button } from "#lib/components/ui/button/index.js";
  import GlucoseChart from "#lib/components/dashboard/glucose-chart/GlucoseChart.svelte";
  import CareportalPumpDialog from "#lib/components/careportal/CareportalPumpDialog.svelte";
  import {
    glucoseChartLookback,
    predictionEnabled,
  } from "#lib/stores/appearance-store.svelte.js";
  import { getRealtimeStore } from "#lib/stores/realtime-store.svelte.js";
  import { bg, bgDelta, bgLabel } from "#lib/utils/formatting.js";
  import { trendAngle } from "@nocturne/ui/glucose";
  import type { PageData } from "./$types";

  let { data }: { data: PageData } = $props();

  const realtimeStore = getRealtimeStore();
  let pumpDialogOpen = $state(false);

  const currentReservoir = $derived(realtimeStore.currentReservoir);
  const arrowDegrees = $derived(trendAngle(realtimeStore.bgDelta ?? 0));
  const isDoubleTrend = $derived(Math.abs(realtimeStore.bgDelta ?? 0) >= 10);

  const statusTone = $derived.by(() => {
    const current = realtimeStore.currentBG;
    const thresholds = realtimeStore.chartThresholds;
    const veryLow = thresholds?.veryLow ?? 54;
    const low = thresholds?.low ?? 70;
    const high = thresholds?.high ?? 180;
    const veryHigh = thresholds?.veryHigh ?? 250;
    if (current <= veryLow || current >= veryHigh) {
      return {
        card: "border-red-500/50 bg-red-950/40",
        value: "text-red-400",
        arrowBox: "bg-red-500/20 border-red-500/50 text-red-300",
      };
    }
    if (current <= low || current >= high) {
      return {
        card: "border-amber-500/50 bg-amber-950/40",
        value: "text-amber-400",
        arrowBox: "bg-amber-500/20 border-amber-500/50 text-amber-300",
      };
    }
    return {
      card: "border-emerald-500/40 bg-emerald-950/30",
      value: "text-emerald-400",
      arrowBox: "bg-emerald-500/20 border-emerald-500/40 text-emerald-300",
    };
  });
</script>

<svelte:head>
  <title>Careportal - Nocturne</title>
</svelte:head>

<div
  class="relative flex h-[calc(100dvh-var(--app-sticky-top,0px))] w-full flex-col gap-2 overflow-hidden p-2 @md:p-3 select-none"
>
  <!-- Prominent Current Glucose & Trend Arrows Header Bar -->
  <div
    class="flex flex-wrap items-center justify-between gap-3 rounded-2xl border px-4 py-2.5 shadow-lg backdrop-blur-md {statusTone.card}"
  >
    <div class="flex items-center gap-3.5">
      <!-- Hero Glucose Value -->
      <div class="flex items-baseline gap-1.5">
        <span
          class="font-mono text-4xl sm:text-5xl font-black tracking-tight leading-none {statusTone.value}"
        >
          {bg(realtimeStore.currentBG)}
        </span>
        <span class="text-xs sm:text-sm font-mono font-bold text-muted-foreground uppercase">
          {bgLabel()}
        </span>
      </div>

      <!-- High-Visibility Trend Arrow(s) & Delta -->
      <div class="flex items-center gap-2.5">
        <div
          class="flex h-11 min-w-11 px-2 items-center justify-center rounded-xl border shadow-inner {statusTone.arrowBox}"
          title="Glucose trend direction"
        >
          <div class="flex items-center -space-x-1.5">
            <ArrowRight
              class="size-7 stroke-[3] transition-transform duration-500"
              style="transform: rotate({arrowDegrees}deg)"
            />
            {#if isDoubleTrend}
              <ArrowRight
                class="size-7 stroke-[3] transition-transform duration-500"
                style="transform: rotate({arrowDegrees}deg)"
              />
            {/if}
          </div>
        </div>

        <div class="flex flex-col">
          <span class="font-mono text-base sm:text-lg font-extrabold leading-tight {statusTone.value}">
            {bgDelta(realtimeStore.bgDelta ?? 0)} {bgLabel()}
          </span>
          <span class="text-[11px] font-mono text-muted-foreground">
            {realtimeStore.timeSinceReading || "Live CGM"}
          </span>
        </div>
      </div>
    </div>

    <!-- Active Therapy Telemetry & Quick PDM Trigger -->
    <div class="flex items-center gap-2 sm:gap-3 ml-auto">
      {#if currentReservoir != null}
        <div
          class="hidden sm:flex items-center gap-1.5 rounded-xl border border-border/60 bg-background/60 px-3 py-1.5 font-mono text-xs"
        >
          <Droplet class="size-3.5 text-sky-400" />
          <span class="text-muted-foreground">RES:</span>
          <span class="font-bold text-foreground">{currentReservoir.toFixed(0)}U</span>
        </div>
      {/if}

      <Button
        variant="default"
        size="lg"
        onclick={() => (pumpDialogOpen = true)}
        aria-label="Open Careportal Pump Menu"
        data-testid="careportal-fab-button"
        class="rounded-xl bg-gradient-to-b from-sky-400 to-sky-600 hover:from-sky-300 hover:to-sky-500 text-slate-950 font-black shadow-lg shadow-sky-500/25 border border-sky-300/40 px-4 gap-2"
      >
        <Plus class="size-4 stroke-[2.5]" />
        <span class="text-xs font-mono uppercase tracking-wider">PDM Touch</span>
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
