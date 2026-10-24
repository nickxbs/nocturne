<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import User from "@lucide/svelte/icons/user";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
  import Plus from "@lucide/svelte/icons/plus";
  import UtensilsCrossed from "@lucide/svelte/icons/utensils-crossed";
  import Syringe from "@lucide/svelte/icons/syringe";
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

<Card.Root class="@container" data-testid="cgmsim-profile-card">
  <Card.Header class="pb-3 flex flex-row items-center justify-between gap-3 flex-wrap">
    <div class="flex items-center gap-2">
      <div class="flex h-8 w-8 items-center justify-center rounded-md bg-primary/10 text-primary">
        <SlidersHorizontal class="h-4 w-4" />
      </div>
      <div>
        <Card.Title class="text-base font-semibold flex items-center gap-2">
          CGMSIM Subject & Parameters
        </Card.Title>
        <Card.Description>
          Virtual patient profile and simulation kinetics (read-only)
        </Card.Description>
      </div>
    </div>

    <div class="flex items-center gap-2">
      <Button
        variant="default"
        size="sm"
        onclick={openEntryDialog}
        data-testid="insert-food-treatment-btn"
      >
        <Plus class="h-3.5 w-3.5 mr-1" />
        <UtensilsCrossed class="h-3.5 w-3.5 mr-1" />
        <Syringe class="h-3.5 w-3.5 mr-1.5" />
        <span>Insert Food & Treatment</span>
      </Button>
    </div>
  </Card.Header>

  <Card.Content class="pt-0">
    <div class="grid grid-cols-1 @md:grid-cols-2 gap-4">
      <!-- Subject section -->
      <div class="rounded-lg border border-border/60 bg-muted/20 p-3 space-y-2.5">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1.5 text-xs font-semibold text-foreground/80">
            <User class="h-3.5 w-3.5 text-primary" />
            <span>Simulated Subject</span>
          </div>
          <Button variant="ghost" size="icon-xs" href={resolve("/cgmsim/subject")} title="Configure Subject" aria-label="Configure Subject">
            <ExternalLink class="h-3 w-3 text-muted-foreground" />
          </Button>
        </div>

        <div class="grid grid-cols-2 gap-2 text-xs">
          <div class="space-y-0.5">
            <span class="text-muted-foreground">Sex</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-subject-sex">
              {subject?.sex ? (sexLabels[subject.sex] ?? subject.sex) : "-"}
            </div>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground">Weight</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-subject-weight">
              {subject?.weight != null ? `${subject.weight} kg` : "-"}
            </div>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground">Age</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-subject-age">
              {subject?.age != null ? `${subject.age} yrs` : "-"}
            </div>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground">Height</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-subject-height">
              {subject?.height != null ? `${subject.height} cm` : "-"}
            </div>
          </div>
        </div>
      </div>

      <!-- Parameters section -->
      <div class="rounded-lg border border-border/60 bg-muted/20 p-3 space-y-2.5">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1.5 text-xs font-semibold text-foreground/80">
            <SlidersHorizontal class="h-3.5 w-3.5 text-primary" />
            <span>Kinetics & Metabolism</span>
          </div>
          <Button variant="ghost" size="icon-xs" href={resolve("/cgmsim/parameters")} title="Configure Parameters" aria-label="Configure Parameters">
            <ExternalLink class="h-3 w-3 text-muted-foreground" />
          </Button>
        </div>

        <div class="grid grid-cols-3 gap-2 text-xs">
          <div class="space-y-0.5">
            <span class="text-muted-foreground">ISF</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-param-isf">
              {params?.isf != null ? `${params.isf} mg/dL/U` : "-"}
            </div>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground">CR</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-param-cr">
              {params?.cr != null ? `${params.cr} g/U` : "-"}
            </div>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground">DIA</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-param-dia">
              {params?.dia != null ? `${params.dia} h` : "-"}
            </div>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground">Carb Abs.</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-param-carbs-abs">
              {params?.carbsAbsTime != null ? `${params.carbsAbsTime} min` : "-"}
            </div>
          </div>
          <div class="space-y-0.5">
            <span class="text-muted-foreground">Insulin Peak</span>
            <div class="font-medium text-foreground" data-testid="cgmsim-param-insulin-peak">
              {params?.insulinPeak != null ? `${params.insulinPeak} min` : "-"}
            </div>
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
