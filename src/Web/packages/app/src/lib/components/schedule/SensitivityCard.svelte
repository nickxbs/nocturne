<script lang="ts">
  import { EmptyState } from "#lib/components/shared/index.js";
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "#lib/components/ui/card/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import TrendingUp from "@lucide/svelte/icons/trending-up";
  import Pencil from "@lucide/svelte/icons/pencil";
  import ScheduleView from "./ScheduleView.svelte";
  import { createSensitivitySchedule } from "#lib/api/generated/profiles.generated.remote.js";
  import { describeSubmitError } from "#lib/forms/submit-error.js";
  import { glucoseUnits } from "#lib/stores/appearance-store.svelte.js";
  import {
    bgLabel,
    convertToDisplayUnits,
    convertFromDisplayUnits,
    timeStringToSeconds,
  } from "#lib/utils/formatting.js";
  import type { SensitivitySchedule } from "#lib/api/generated/nocturne-api-client.js";

  interface Props {
    profileName: string;
    schedule: SensitivitySchedule | null;
    readOnly?: boolean;
  }

  interface DraftEntry {
    time?: string;
    value?: number;
  }

  let { profileName, schedule, readOnly = false }: Props = $props();

  let editing = $state(false);
  let saving = $state(false);
  let saveError = $state<string | null>(null);
  let draft = $state<DraftEntry[]>([]);

  const displayUnits = $derived(glucoseUnits.current);

  function startEdit() {
    if (schedule?.entries?.length) {
      // Schedule values are mg/dL; convert to display units for editing
      draft = schedule.entries.map((e) => ({
        time: e.time ?? "00:00",
        value: convertToDisplayUnits(e.value ?? 0, displayUnits),
      }));
    } else {
      draft = [
        { time: "00:00", value: convertToDisplayUnits(50, displayUnits) },
      ];
    }
    saveError = null;
    editing = true;
  }

  const validationError = $derived.by(() => {
    if (draft.some((e) => !e.time))
      return "Every time block needs a start time.";
    if (draft.some((e) => e.value === undefined || e.value <= 0))
      return "Sensitivity values must be greater than zero.";
    if (new Set(draft.map((e) => e.time)).size !== draft.length)
      return "Time blocks must have unique start times.";
    return null;
  });

  async function save() {
    if (validationError) return;
    saving = true;
    saveError = null;
    try {
      await createSensitivitySchedule({
        profileName,
        timestamp: new Date().toISOString(),
        dataSource: "manual",
        entries: draft.map((e) => ({
          time: e.time,
          timeAsSeconds: timeStringToSeconds(e.time),
          value: convertFromDisplayUnits(e.value ?? 0, displayUnits),
        })),
      });
      editing = false;
    } catch (err) {
      saveError = describeSubmitError(
        err,
        "Failed to save sensitivity schedule"
      );
    } finally {
      saving = false;
    }
  }
</script>

{#if editing}
  <Card>
    <CardContent class="space-y-4 pt-6">
      <ScheduleView
        title="Insulin sensitivity (ISF)"
        description="BG drop per unit of insulin"
        unit="{bgLabel()}/U"
        icon={TrendingUp}
        entries={draft}
        onchange={(entries) => (draft = entries)}
        step={displayUnits === "mmol" ? 0.1 : 1}
        min={displayUnits === "mmol" ? 0.1 : 1}
      />
      {#if validationError}
        <p class="text-sm text-destructive">{validationError}</p>
      {/if}
      {#if saveError}
        <p class="text-sm text-destructive">{saveError}</p>
      {/if}
      <div class="flex justify-end gap-2">
        <Button
          variant="outline"
          disabled={saving}
          onclick={() => (editing = false)}
        >
          Cancel
        </Button>
        <Button disabled={saving || !!validationError} onclick={save}>
          {saving ? "Saving..." : "Save"}
        </Button>
      </div>
    </CardContent>
  </Card>
{:else if schedule?.entries?.length}
  <ScheduleView
    title="Insulin sensitivity (ISF)"
    description="BG drop per unit of insulin"
    unit="{bgLabel()}/U"
    icon={TrendingUp}
    entries={schedule.entries}
    sourceUnits="mg/dl"
  >
    {#snippet actions()}
      {#if !readOnly}
        <Button variant="ghost" size="sm" onclick={startEdit}>
          <Pencil class="mr-1 h-4 w-4" />
          Edit
        </Button>
      {/if}
    {/snippet}
  </ScheduleView>
{:else}
  <Card>
    <CardHeader class="pb-3">
      <div class="flex items-center gap-3">
        <div
          class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10"
        >
          <TrendingUp class="h-5 w-5 text-primary" />
        </div>
        <div>
          <CardTitle class="text-base">Insulin sensitivity (ISF)</CardTitle>
          <CardDescription size="sm">
            BG drop per unit of insulin
          </CardDescription>
        </div>
      </div>
    </CardHeader>
    <CardContent>
      <EmptyState
        art="report-pages"
        size="compact"
        title="No insulin sensitivity configured"
        action={readOnly ? undefined : setSensitivityAction}
      />
    </CardContent>
  </Card>
  {#snippet setSensitivityAction()}
    <Button variant="outline" size="sm" onclick={startEdit}>
      Set insulin sensitivity
    </Button>
  {/snippet}
{/if}
