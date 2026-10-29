<script lang="ts">
  import Plus from "@lucide/svelte/icons/plus";
  import { Button } from "$lib/components/ui/button";
  import GlucoseChart from "$lib/components/dashboard/glucose-chart/GlucoseChart.svelte";
  import CareportalPumpDialog from "$lib/components/careportal/CareportalPumpDialog.svelte";
  import {
    glucoseChartLookback,
    predictionEnabled,
  } from "$lib/stores/appearance-store.svelte";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import type { PageData } from "./$types";

  let { data }: { data: PageData } = $props();

  const realtimeStore = getRealtimeStore();
  let pumpDialogOpen = $state(false);

  const currentReservoir = $derived(
    realtimeStore.currentReservoir,
  );
</script>

<svelte:head>
  <title>Careportal - Nocturne</title>
</svelte:head>

<div
  class="relative flex h-[calc(100dvh-var(--app-sticky-top,0px))] w-full flex-col overflow-hidden p-2 @md:p-3 select-none"
>
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

  <Button
    variant="default"
    size="xl"
    onclick={() => (pumpDialogOpen = true)}
    aria-label="Open Careportal Pump Menu"
    data-testid="careportal-fab-button"
    class="absolute bottom-5 left-1/2 z-30 -translate-x-1/2"
  >
    <Plus class="size-6" />
  </Button>
</div>

<CareportalPumpDialog
  bind:open={pumpDialogOpen}
  {currentReservoir}
/>
