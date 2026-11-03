<script lang="ts">
  import { Key } from "@nocturne/ui/keys";
  import { lazyComponent } from "#lib/utils/lazy-component.svelte.js";
  import type { EntryRecord } from "#lib/constants/entry-categories.js";
  import { ENTRY_CATEGORIES } from "#lib/constants/entry-categories.js";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "#lib/components/ui/card/index.js";
  import { Badge } from "#lib/components/ui/badge/index.js";
  import { EmptyState } from "#lib/components/shared/index.js";
  import { time } from "#lib/utils/formatting.js";
  import { entryDetails, entryLabel } from "#lib/utils/entry-summary.js";
  import { getRealtimeStore } from "#lib/stores/realtime-store.svelte.js";
  import { glucoseUnits } from "#lib/stores/appearance-store.svelte.js";

  interface ComponentProps {
    entries?: EntryRecord[];
    maxEntries?: number;
    title?: string;
    subtitle?: string;
  }

  let {
    entries,
    maxEntries = 5,
    title = "Recent treatments",
    subtitle = "Last 24 hours",
  }: ComponentProps = $props();

  const realtimeStore = getRealtimeStore();

  const displayEntries = $derived(
    (entries ?? realtimeStore.recentEntries).slice(0, maxEntries),
  );

  let selectedEntry = $state<EntryRecord | null>(null);
  let correlatedRecords = $state<EntryRecord[]>([]);
  const treatmentInspectionDialog = lazyComponent(
    () => import("./glucose-chart/dialogs/TreatmentInspectionDialog.svelte"),
  );

  const selectedTimestamp = $derived(
    new Date(selectedEntry?.data.mills ?? Date.now())
  );

  const selectedBolusInsulin = $derived.by(() => {
    if (selectedEntry?.kind === "bolus") {
      return selectedEntry.data.insulin;
    }
    const correlated = correlatedRecords.find(
      (r): r is Extract<EntryRecord, { kind: "bolus" }> => r.kind === "bolus",
    );
    return correlated?.data.insulin;
  });

  const selectedBolusType = $derived.by(() => {
    if (selectedEntry?.kind === "bolus") {
      return selectedEntry.data.bolusType;
    }
    const correlated = correlatedRecords.find(
      (r): r is Extract<EntryRecord, { kind: "bolus" }> => r.kind === "bolus",
    );
    return correlated?.data.bolusType;
  });

  const selectedBolusDataSource = $derived.by(() => {
    if (selectedEntry?.kind === "bolus") {
      return selectedEntry.data.dataSource;
    }
    const correlated = correlatedRecords.find(
      (r): r is Extract<EntryRecord, { kind: "bolus" }> => r.kind === "bolus",
    );
    return correlated?.data.dataSource;
  });

  const selectedCarbGrams = $derived.by(() => {
    if (selectedEntry?.kind === "carbs") {
      return selectedEntry.data.carbs;
    }
    const correlated = correlatedRecords.find(
      (r): r is Extract<EntryRecord, { kind: "carbs" }> => r.kind === "carbs",
    );
    return correlated?.data.carbs;
  });

  const selectedCarbLabel = $derived.by(() => {
    if (selectedEntry?.kind === "carbs") {
      return selectedEntry.data.label;
    }
    const correlated = correlatedRecords.find(
      (r): r is Extract<EntryRecord, { kind: "carbs" }> => r.kind === "carbs",
    );
    return correlated?.data.label;
  });

  const selectedCarbDataSource = $derived.by(() => {
    if (selectedEntry?.kind === "carbs") {
      return selectedEntry.data.dataSource;
    }
    const correlated = correlatedRecords.find(
      (r): r is Extract<EntryRecord, { kind: "carbs" }> => r.kind === "carbs",
    );
    return correlated?.data.dataSource;
  });

  function handleEntryClick(entry: EntryRecord) {
    selectedEntry = entry;
    correlatedRecords = realtimeStore.findCorrelatedEntries(entry);
    treatmentInspectionDialog.open = true;
  }
</script>

<Card class="@container">
  <svelte:boundary>
    {#snippet pending()}
      <div class="flex items-center justify-center h-full">
        <div
          class="animate-spin rounded-full h-8 w-8 border-b-2 border-foreground"
        ></div>
      </div>
    {/snippet}
    {#snippet failed(_error)}
      <p class="text-destructive text-center">Error loading recent entries.</p>
    {/snippet}
    <CardHeader class="px-3 @md:px-6">
      <CardTitle>{title}</CardTitle>
      <p class="text-sm text-muted-foreground">{subtitle}</p>
    </CardHeader>
    <CardContent class="px-3 @md:px-6">
      {#if displayEntries.length > 0}
        <ul class="m-0 list-none divide-y divide-border p-0">
          {#each displayEntries as entry, i (entry.data.id ?? `${entry.data.mills}-${i}`)}
            {@const category = ENTRY_CATEGORIES[entry.kind]}
            <li>
            <div
              class="-mx-2 flex cursor-pointer items-center justify-between rounded-md px-2 py-2.5 transition-colors hover:bg-accent/50"
              onclick={() => handleEntryClick(entry)}
              role="button"
              tabindex="0"
              onkeydown={(e) => {
                if (e.key === Key.Enter || e.key === Key.Space) {
                  e.preventDefault();
                  handleEntryClick(entry);
                }
              }}
            >
              <div class="flex items-center gap-2 @md:gap-3">
                <Badge variant={category.badge}>
                  {category.name}
                </Badge>
                <div>
                  <div class="font-medium">
                    {entryLabel(entry)}
                    {#if entryDetails(entry)}
                      <span class="text-muted-foreground"> - {entryDetails(entry)}</span>
                    {/if}
                  </div>
                  <div class="text-sm text-muted-foreground">
                    {time(entry.data.mills!)}
                  </div>
                </div>
              </div>
              <div class="text-sm text-muted-foreground">
                {entry.data.dataSource || ""}
              </div>
            </div>
            </li>
          {/each}
        </ul>
      {:else}
        <EmptyState art="clock" size="compact" title="No recent entries" />
      {/if}
    </CardContent>
  </svelte:boundary>
</Card>

{#if treatmentInspectionDialog.component && selectedEntry}
  <treatmentInspectionDialog.component
    glucoseUnits={glucoseUnits.current}
    bind:open={treatmentInspectionDialog.open}
    timestamp={selectedTimestamp}
    bolusInsulin={selectedBolusInsulin}
    bolusType={selectedBolusType}
    bolusDataSource={selectedBolusDataSource}
    carbGrams={selectedCarbGrams}
    carbLabel={selectedCarbLabel}
    carbDataSource={selectedCarbDataSource}
    entry={selectedEntry}
    {correlatedRecords}
    glucoseData={[]}
    highThreshold={180}
    lowThreshold={70}
    hasGlucoseContext={false}
    hasDeliveryContext={false}
    onClose={() => {
      treatmentInspectionDialog.open = false;
      selectedEntry = null;
      correlatedRecords = [];
    }}
  />
{/if}
