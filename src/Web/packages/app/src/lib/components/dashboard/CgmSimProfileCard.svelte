<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import User from "@lucide/svelte/icons/user";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
  import Plus from "@lucide/svelte/icons/plus";
  import ExternalLink from "@lucide/svelte/icons/external-link";
  import { resolve } from "$app/paths";
  import { lazyComponent } from "$lib/utils/lazy-component.svelte";
  import { getSettings } from "$lib/api/generated/cgmSims.generated.remote";
  import type { EntryRecord } from "$lib/constants/entry-categories";

  const settingsQuery = getSettings();
  const settings = $derived(settingsQuery.current);
  const subject = $derived(settings?.subject);
  const params = $derived(settings?.parameters);

  const sexLabels: Record<string, string> = {
    male: "Male",
    female: "Female",
    other: "Other",
  };

  const entryEditDialog = lazyComponent(
    () => import("$lib/components/entries/EntryEditDialog.svelte"),
  );

  let selectedEntry = $state<EntryRecord | null>(null);

  function openEntryDialog() {
    selectedEntry = null;
    entryEditDialog.open = true;
  }
</script>

<Card.Root class="@container h-full flex flex-col justify-between" data-testid="cgmsim-profile-card">
  <Card.Header class="p-3 pb-2 flex flex-row items-center justify-between gap-2 flex-wrap">
    <div class="flex items-center gap-1.5">
      <div class="flex h-6 w-6 items-center justify-center rounded bg-primary/10 text-primary">
        <SlidersHorizontal class="h-3.5 w-3.5" />
      </div>
      <div>
        <Card.Title class="text-sm font-semibold flex items-center gap-1.5 leading-none">
          CGMSIM Profile
        </Card.Title>
      </div>
    </div>

    <Button
      variant="default"
      size="sm"
      onclick={openEntryDialog}
      data-testid="insert-food-treatment-btn"
    >
      <Plus class="h-3.5 w-3.5 mr-1" />
      <span>CarePortal</span>
    </Button>
  </Card.Header>

  <Card.Content class="p-3 pt-0 flex-1 flex flex-col justify-center">
    <div class="grid grid-cols-1 @sm:grid-cols-2 gap-2">
      <!-- Subject section -->
      <div class="rounded-md border border-border/60 bg-muted/20 p-2 space-y-1.5">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1 text-xs font-semibold text-foreground/80">
            <User class="h-3 w-3 text-primary" />
            <span>Subject</span>
          </div>
          <Button variant="ghost" size="icon-xs" href={resolve("/cgmsim/subject")} title="Configure Subject" aria-label="Configure Subject">
            <ExternalLink class="h-2.5 w-2.5 text-muted-foreground" />
          </Button>
        </div>

        <div class="grid grid-cols-2 gap-1.5 text-xs">
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">Sex</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-subject-sex">
              {subject?.sex ? (sexLabels[subject.sex] ?? subject.sex) : "-"}
            </span>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">Weight</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-subject-weight">
              {subject?.weight != null ? `${subject.weight} kg` : "-"}
            </span>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">Age</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-subject-age">
              {subject?.age != null ? `${subject.age} yrs` : "-"}
            </span>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">Height</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-subject-height">
              {subject?.height != null ? `${subject.height} cm` : "-"}
            </span>
          </div>
        </div>
      </div>

      <!-- Parameters section -->
      <div class="rounded-md border border-border/60 bg-muted/20 p-2 space-y-1.5">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1 text-xs font-semibold text-foreground/80">
            <SlidersHorizontal class="h-3 w-3 text-primary" />
            <span>Kinetics</span>
          </div>
          <Button variant="ghost" size="icon-xs" href={resolve("/cgmsim/parameters")} title="Configure Parameters" aria-label="Configure Parameters">
            <ExternalLink class="h-2.5 w-2.5 text-muted-foreground" />
          </Button>
        </div>

        <div class="grid grid-cols-3 gap-1.5 text-xs">
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">ISF</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-param-isf">
              {params?.isf != null ? `${params.isf} mg/dL/U` : "-"}
            </span>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">CR</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-param-cr">
              {params?.cr != null ? `${params.cr} g/U` : "-"}
            </span>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">DIA</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-param-dia">
              {params?.dia != null ? `${params.dia} h` : "-"}
            </span>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">Carb Abs</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-param-carbs-abs">
              {params?.carbsAbsTime != null ? `${params.carbsAbsTime} min` : "-"}
            </span>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground text-2xs block leading-none">Peak</span>
            <span class="font-medium text-foreground leading-none" data-testid="cgmsim-param-insulin-peak">
              {params?.insulinPeak != null ? `${params.insulinPeak} min` : "-"}
            </span>
          </div>
        </div>
      </div>
    </div>
  </Card.Content>
</Card.Root>

{#if entryEditDialog.component}
  <entryEditDialog.component
    bind:open={entryEditDialog.open}
    entry={selectedEntry}
    onClose={() => {
      entryEditDialog.open = false;
      selectedEntry = null;
    }}
  />
{/if}
