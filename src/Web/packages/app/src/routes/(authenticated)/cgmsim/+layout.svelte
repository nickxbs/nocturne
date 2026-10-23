<script lang="ts">
  import { page } from "$app/state";
  import { resolve } from "$app/paths";
  import Activity from "@lucide/svelte/icons/activity";
  import User from "@lucide/svelte/icons/user";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
  import type { Snippet } from "svelte";

  interface Props {
    children: Snippet;
  }

  const { children }: Props = $props();

  const isSubject = $derived(page.url.pathname.startsWith("/cgmsim/subject"));
  const isParameters = $derived(page.url.pathname.startsWith("/cgmsim/parameters"));
</script>

<div class="@container container mx-auto max-w-4xl p-3 @md:p-6 space-y-6">
  <div class="flex items-center gap-3">
    <div class="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10">
      <Activity class="h-6 w-6 text-primary" />
    </div>
    <div>
      <h1 class="text-2xl font-bold tracking-tight">CGM Simulator</h1>
      <p class="text-muted-foreground">
        Configure simulated subject characteristics and metabolic kinetics
      </p>
    </div>
  </div>

  <nav class="flex border-b border-border gap-2" aria-label="CGMSIM navigation">
    <a
      href={resolve("/cgmsim/subject")}
      class="inline-flex items-center gap-2 px-4 py-2 border-b-2 font-medium text-sm transition-colors {isSubject ? 'border-primary text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground hover:border-muted-foreground/30'}"
    >
      <User class="h-4 w-4" />
      Subject
    </a>
    <a
      href={resolve("/cgmsim/parameters")}
      class="inline-flex items-center gap-2 px-4 py-2 border-b-2 font-medium text-sm transition-colors {isParameters ? 'border-primary text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground hover:border-muted-foreground/30'}"
    >
      <SlidersHorizontal class="h-4 w-4" />
      Parameters
    </a>
  </nav>

  {@render children()}
</div>
