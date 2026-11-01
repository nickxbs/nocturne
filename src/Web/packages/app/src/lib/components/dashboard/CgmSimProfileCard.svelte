<script lang="ts">
  import { Card } from "#lib/components/ui/card/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import User from "@lucide/svelte/icons/user";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
  import ExternalLink from "@lucide/svelte/icons/external-link";
  import { resolve } from "$app/paths";
  import { getSettings } from "#lib/api/generated/cgmSims.generated.remote.js";

  const settingsQuery = getSettings();
  const settings = $derived(settingsQuery.current);
  const subject = $derived(settings?.subject);
  const params = $derived(settings?.parameters);

  const sexLabels: Record<string, string> = {
    male: "Male",
    female: "Female",
    other: "Other",
  };
</script>

<Card size="flush" class="@container h-full" data-testid="cgmsim-profile-card">
  <section class="flex h-full flex-col justify-between gap-3 px-5 py-4">
    <header class="flex min-h-7 items-center justify-between gap-2">
      <h2 class="text-sm font-medium text-muted-foreground">CGMSIM Profile</h2>
      <span class="text-xs text-muted-foreground">Virtual Patient</span>
    </header>

    <div class="flex-1 grid grid-cols-1 @sm:grid-cols-2 gap-4 items-center">
      <!-- Subject section -->
      <div class="flex flex-col justify-between space-y-2">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1.5 text-xs font-semibold text-foreground/80">
            <User class="size-3.5 text-primary" />
            <span>Subject</span>
          </div>
          <Button
            variant="ghost-muted"
            size="icon-xs"
            href={resolve("/(authenticated)/settings/patient")}
            title="Configure Patient"
            aria-label="Configure Patient"
          >
            <ExternalLink class="size-3" />
          </Button>
        </div>

        <div class="grid grid-cols-2 gap-x-3 gap-y-1.5 text-xs">
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

      <!-- Kinetics section -->
      <div class="flex flex-col justify-between space-y-2 @sm:border-l @sm:border-border/60 @sm:pl-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-1.5 text-xs font-semibold text-foreground/80">
            <SlidersHorizontal class="size-3.5 text-primary" />
            <span>Kinetics</span>
          </div>
          <Button
            variant="ghost-muted"
            size="icon-xs"
            href={resolve("/(authenticated)/cgmsim/parameters")}
            title="Configure Parameters"
            aria-label="Configure Parameters"
          >
            <ExternalLink class="size-3" />
          </Button>
        </div>

        <div class="grid grid-cols-3 gap-x-2 gap-y-1.5 text-xs">
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
  </section>
</Card>
