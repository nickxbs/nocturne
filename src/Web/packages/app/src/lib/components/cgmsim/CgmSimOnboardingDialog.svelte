<script lang="ts">
  import * as Dialog from "#lib/components/ui/dialog/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import { Label } from "#lib/components/ui/label/index.js";
  import * as Select from "#lib/components/ui/select/index.js";
  import Sparkles from "@lucide/svelte/icons/sparkles";
  import User from "@lucide/svelte/icons/user";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
  import ArrowRight from "@lucide/svelte/icons/arrow-right";
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import Check from "@lucide/svelte/icons/check";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import CircleAlert from "@lucide/svelte/icons/circle-alert";
  import LogOut from "@lucide/svelte/icons/log-out";
  import { toast } from "svelte-sonner";
  import { describeSubmitError } from "#lib/forms/index.js";
  import { initialize } from "#lib/api/generated/cgmSims.generated.remote.js";
  import { invalidate } from "$app/navigation";
  import { SHELL_DATA_KEY } from "#lib/constants/shell-data.js";

  interface Props {
    open: boolean;
    canWrite: boolean;
    onCompleted?: () => void;
  }

  let { open = $bindable(false), canWrite, onCompleted }: Props = $props();

  let currentStep = $state<1 | 2>(1);
  let isSubmitting = $state(false);

  // Step 1: Subject fields (initially empty)
  let sex = $state<string>("");
  let weight = $state<string>("");
  let age = $state<string>("");
  let height = $state<string>("");

  // Step 2: Simulation parameters (initially empty)
  let isf = $state<string>("");
  let dia = $state<string>("");
  let carbsAbsTime = $state<string>("");
  let insulinPeak = $state<string>("");
  let cr = $state<string>("");

  const sexOptions: Record<string, string> = {
    male: "Male",
    female: "Female",
    other: "Other / Unspecified",
  };

  function randomizeSubject() {
    const sexes = ["male", "female", "other"];
    sex = sexes[Math.floor(Math.random() * sexes.length)]!;
    weight = (Math.random() * 35 + 55).toFixed(1); // 55.0 - 90.0 kg
    age = Math.floor(Math.random() * 45 + 20).toString(); // 20 - 65 years
    height = (Math.random() * 35 + 155).toFixed(1); // 155.0 - 190.0 cm
    toast.info("Patient values generated randomly.");
  }

  function randomizeParameters() {
    isf = (Math.floor(Math.random() * 9 + 6) * 5).toString(); // 30 - 70 mg/dL/U
    dia = (Math.random() * 2.0 + 3.5).toFixed(1); // 3.5 - 5.5 h
    carbsAbsTime = (Math.floor(Math.random() * 8 + 8) * 15).toString(); // 120 - 225 min
    insulinPeak = (Math.floor(Math.random() * 7 + 10) * 5).toString(); // 50 - 80 min
    cr = (Math.random() * 7.0 + 8.0).toFixed(1); // 8.0 - 15.0 g/U
    toast.info("Simulation parameters generated randomly.");
  }

  const isStep1Valid = $derived.by(() => {
    const w = Number(weight);
    const a = Number(age);
    const h = Number(height);
    return (
      sex.trim().length > 0 &&
      !isNaN(w) &&
      w >= 1 &&
      w <= 500 &&
      !isNaN(a) &&
      a >= 1 &&
      a <= 150 &&
      !isNaN(h) &&
      h >= 20 &&
      h <= 300
    );
  });

  const isStep2Valid = $derived.by(() => {
    const isfNum = Number(isf);
    const diaNum = Number(dia);
    const carbsNum = Number(carbsAbsTime);
    const peakNum = Number(insulinPeak);
    const crNum = Number(cr);
    return (
      !isNaN(isfNum) &&
      isfNum >= 1 &&
      isfNum <= 1000 &&
      !isNaN(diaNum) &&
      diaNum >= 0.5 &&
      diaNum <= 24 &&
      !isNaN(carbsNum) &&
      carbsNum >= 5 &&
      carbsNum <= 1440 &&
      !isNaN(peakNum) &&
      peakNum >= 5 &&
      peakNum <= 360 &&
      !isNaN(crNum) &&
      crNum >= 0.1 &&
      crNum <= 200
    );
  });

  function goToStep2(e: Event) {
    e.preventDefault();
    if (!isStep1Valid) {
      toast.error("Please fill in all patient data with valid values before proceeding.");
      return;
    }
    currentStep = 2;
  }

  function goToStep1() {
    currentStep = 1;
  }

  async function handleFinalSubmit(e: SubmitEvent) {
    e.preventDefault();
    if (!isStep1Valid || !isStep2Valid || isSubmitting) return;

    isSubmitting = true;

    try {
      await initialize({
        subject: {
          sex,
          weight: Number(weight),
          age: Math.round(Number(age)),
          height: Number(height),
        },
        parameters: {
          isf: Number(isf),
          dia: Number(dia),
          carbsAbsTime: Math.round(Number(carbsAbsTime)),
          insulinPeak: Math.round(Number(insulinPeak)),
          cr: Number(cr),
        },
      });

      toast.success("Initialization completed successfully! Welcome to CGMSIM.");
      open = false;
      await invalidate(SHELL_DATA_KEY);
      onCompleted?.();
    } catch (err) {
      toast.error(describeSubmitError(err, "Failed to complete initialization."));
    } finally {
      isSubmitting = false;
    }
  }
</script>

<Dialog.Root bind:open>
  <Dialog.Content
    class="max-w-2xl max-h-[90vh] overflow-y-auto"
    showClose={false}
    escapeKeydownBehavior="ignore"
    interactOutsideBehavior="ignore"
  >
    {#if !canWrite}
      <!-- Read-only view -->
      <Dialog.Header class="text-center pt-2">
        <div class="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-warning/10 text-warning">
          <CircleAlert class="h-7 w-7" />
        </div>
        <Dialog.Title class="text-xl font-bold">Workspace Initialization Required</Dialog.Title>
        <Dialog.Description>
          This workspace has not been configured with CGMSIM model parameters yet.
          An administrator or member with write permissions must complete initial configuration.
        </Dialog.Description>
      </Dialog.Header>

      <div class="py-6 flex justify-center gap-3">
        <Button variant="outline" href="/auth/logout">
          <LogOut class="mr-2 h-4 w-4" />
          Sign out
        </Button>
        <Button variant="default" href="/">
          Overview
        </Button>
      </div>
    {:else}
      <!-- Wizard with write permissions -->
      <Dialog.Header>
        <div class="flex items-center justify-between border-b pb-3 mb-2">
          <div>
            <Dialog.Title class="text-xl font-bold">Initial CGMSIM Configuration</Dialog.Title>
            <Dialog.Description>
              Set patient and simulation parameters to activate this workspace.
            </Dialog.Description>
          </div>
          <!-- Stepper indicator -->
          <div class="flex items-center gap-2 text-xs font-medium">
            <span
              class="flex items-center gap-1.5 px-2.5 py-1 rounded-full {currentStep === 1
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground'}"
            >
              <User class="h-3.5 w-3.5" />
              1. Patient
            </span>
            <span class="text-muted-foreground">→</span>
            <span
              class="flex items-center gap-1.5 px-2.5 py-1 rounded-full {currentStep === 2
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground'}"
            >
              <SlidersHorizontal class="h-3.5 w-3.5" />
              2. Parameters
            </span>
          </div>
        </div>
      </Dialog.Header>

      {#if currentStep === 1}
        <!-- STEP 1: Patient Data / Subject -->
        <form onsubmit={goToStep2} class="space-y-5">
          <div class="flex items-center justify-between bg-muted/40 p-3 rounded-lg border">
            <div class="space-y-0.5">
              <h4 class="text-sm font-semibold flex items-center gap-2">
                <User class="h-4 w-4 text-primary" />
                Step 1: Patient Data (Subject)
              </h4>
              <p class="text-xs text-muted-foreground">
                Physiological traits of the virtual patient in the simulation model.
              </p>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onclick={randomizeSubject}
            >
              <Sparkles class="mr-1.5 h-3.5 w-3.5 text-warning" />
              Generate random values
            </Button>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <!-- Sex -->
            <div class="space-y-1.5">
              <Label for="onboarding-subject-sex" size="sm">
                Biological sex <span class="text-destructive">*</span>
              </Label>
              <Select.Root type="single" bind:value={sex} required>
                <Select.Trigger id="onboarding-subject-sex" class="w-full">
                  {sex ? (sexOptions[sex] ?? sex) : "Select sex..."}
                </Select.Trigger>
                <Select.Content>
                  {#each Object.entries(sexOptions) as [val, lbl] (val)}
                    <Select.Item value={val} label={lbl} />
                  {/each}
                </Select.Content>
              </Select.Root>
            </div>

            <!-- Weight -->
            <div class="space-y-1.5">
              <Label for="onboarding-subject-weight" size="sm">
                Body weight (kg) <span class="text-destructive">*</span>
              </Label>
              <div class="relative">
                <Input
                  id="onboarding-subject-weight"
                  type="number"
                  step="0.1"
                  min="1"
                  max="500"
                  placeholder="e.g. 70.0"
                  required
                  bind:value={weight}
                />
                <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                  kg
                </span>
              </div>
            </div>

            <!-- Age -->
            <div class="space-y-1.5">
              <Label for="onboarding-subject-age" size="sm">
                Age (years) <span class="text-destructive">*</span>
              </Label>
              <div class="relative">
                <Input
                  id="onboarding-subject-age"
                  type="number"
                  step="1"
                  min="1"
                  max="150"
                  placeholder="e.g. 30"
                  required
                  bind:value={age}
                />
                <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                  years
                </span>
              </div>
            </div>

            <!-- Height -->
            <div class="space-y-1.5">
              <Label for="onboarding-subject-height" size="sm">
                Height (cm) <span class="text-destructive">*</span>
              </Label>
              <div class="relative">
                <Input
                  id="onboarding-subject-height"
                  type="number"
                  step="0.5"
                  min="20"
                  max="300"
                  placeholder="e.g. 170.0"
                  required
                  bind:value={height}
                />
                <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                  cm
                </span>
              </div>
            </div>
          </div>

          <Dialog.Footer class="pt-3 border-t flex justify-end">
            <Button type="submit" disabled={!isStep1Valid}>
              Next: Simulation Parameters
              <ArrowRight class="ml-2 h-4 w-4" />
            </Button>
          </Dialog.Footer>
        </form>
      {:else}
        <!-- STEP 2: Simulation Parameters -->
        <form onsubmit={handleFinalSubmit} class="space-y-5">
          <div class="flex items-center justify-between bg-muted/40 p-3 rounded-lg border">
            <div class="space-y-0.5">
              <h4 class="text-sm font-semibold flex items-center gap-2">
                <SlidersHorizontal class="h-4 w-4 text-primary" />
                Step 2: Simulation Parameters
              </h4>
              <p class="text-xs text-muted-foreground">
                Insulin kinetics, carbohydrate absorption, and sensitivity.
              </p>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onclick={randomizeParameters}
            >
              <Sparkles class="mr-1.5 h-3.5 w-3.5 text-warning" />
              Generate random values
            </Button>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <!-- ISF -->
            <div class="space-y-1.5">
              <Label for="onboarding-param-isf" size="sm">
                ISF (Insulin Sensitivity Factor) <span class="text-destructive">*</span>
              </Label>
              <div class="relative">
                <Input
                  id="onboarding-param-isf"
                  type="number"
                  step="0.5"
                  min="1"
                  max="1000"
                  placeholder="e.g. 50.0"
                  required
                  bind:value={isf}
                />
                <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                  mg/dL/U
                </span>
              </div>
            </div>

            <!-- DIA -->
            <div class="space-y-1.5">
              <Label for="onboarding-param-dia" size="sm">
                DIA (Duration of Insulin Action) <span class="text-destructive">*</span>
              </Label>
              <div class="relative">
                <Input
                  id="onboarding-param-dia"
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
            </div>

            <!-- Carbs Absorption Time -->
            <div class="space-y-1.5">
              <Label for="onboarding-param-carbs" size="sm">
                Carb Absorption Time <span class="text-destructive">*</span>
              </Label>
              <div class="relative">
                <Input
                  id="onboarding-param-carbs"
                  type="number"
                  step="5"
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
            </div>

            <!-- Insulin Peak -->
            <div class="space-y-1.5">
              <Label for="onboarding-param-peak" size="sm">
                Insulin Peak <span class="text-destructive">*</span>
              </Label>
              <div class="relative">
                <Input
                  id="onboarding-param-peak"
                  type="number"
                  step="5"
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
            </div>

            <!-- CR -->
            <div class="space-y-1.5 sm:col-span-2">
              <Label for="onboarding-param-cr" size="sm">
                CR (Carb Ratio) <span class="text-destructive">*</span>
              </Label>
              <div class="relative sm:max-w-[50%]">
                <Input
                  id="onboarding-param-cr"
                  type="number"
                  step="0.1"
                  min="0.1"
                  max="200"
                  placeholder="e.g. 10.0"
                  required
                  bind:value={cr}
                />
                <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                  g/U
                </span>
              </div>
            </div>
          </div>

          <Dialog.Footer class="pt-3 border-t flex items-center justify-between">
            <Button
              type="button"
              variant="outline"
              onclick={goToStep1}
              disabled={isSubmitting}
            >
              <ArrowLeft class="mr-2 h-4 w-4" />
              Back
            </Button>
            <Button
              type="submit"
              disabled={!isStep2Valid || isSubmitting}
              class="min-w-36"
            >
              {#if isSubmitting}
                <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                Initializing...
              {:else}
                <Check class="mr-2 h-4 w-4" />
                Complete and Start
              {/if}
            </Button>
          </Dialog.Footer>
        </form>
      {/if}
    {/if}
  </Dialog.Content>
</Dialog.Root>
