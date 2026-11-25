<script lang="ts">
  import { browser } from "$app/env";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { tick } from "svelte";
  import ArrowRight from "@lucide/svelte/icons/arrow-right";
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import Sprout from "@lucide/svelte/icons/sprout";
  import ShieldAlert from "@lucide/svelte/icons/shield-alert";
  import User from "@lucide/svelte/icons/user";
  import Activity from "@lucide/svelte/icons/activity";
  import Flame from "@lucide/svelte/icons/flame";
  import Trophy from "@lucide/svelte/icons/trophy";
  import Sofa from "@lucide/svelte/icons/armchair";
  import TriangleAlert from "@lucide/svelte/icons/triangle-alert";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import { Button } from "#lib/components/ui/button/index.js";
  import { Badge } from "#lib/components/ui/badge/index.js";
  import { Progress } from "#lib/components/ui/progress/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import { Label } from "#lib/components/ui/label/index.js";
  import * as Select from "#lib/components/ui/select/index.js";
  import * as RadioGroup from "#lib/components/ui/radio-group/index.js";
  import * as Alert from "#lib/components/ui/alert/index.js";
  import { markSetupComplete } from "#routes/(unauthenticated)/setup/setup.remote.js";
  import {
    setPatientRelationship,
    getUnitsAndTimezone,
    setUnitsAndTimezone,
  } from "#lib/api/generated/tenantSettings.generated.remote.js";
  import {
    initialize,
  } from "#lib/api/generated/cgmSims.generated.remote.js";
  import type { CgmSimSettings } from "#lib/api/generated/schemas.js";
  import { PatientRelationship } from "#lib/api/index.js";
  import { applyPreferences, type GlucoseUnits } from "#lib/stores/appearance-store.svelte.js";
  import { isGlucoseUnits } from "#lib/utils/formatting.js";
  import { patientVoice } from "#lib/onboarding/patient-voice.svelte.js";
  import { describeSubmitError } from "#lib/forms/submit-error.js";
  import { retainQuery } from "#lib/api/retain-query.svelte.js";
  import StepSidebar from "#routes/(unauthenticated)/setup/StepSidebar.svelte";
  import SetupChrome from "#routes/(unauthenticated)/setup/SetupChrome.svelte";
  import UnitsAndTimezone from "#routes/(unauthenticated)/setup/steps/UnitsAndTimezone.svelte";
  import StepArtwork, { type StepArt } from "#routes/(unauthenticated)/setup/StepArtwork.svelte";

  const httpsRequired = $derived(
    browser &&
      window.location.protocol !== "https:" &&
      !window.location.hostname.match(/^(localhost|127\.0\.0\.1|::1|\[::1\])$/)
  );

  type StepDef = { id: "units" | "patient" | "finish"; label: string; short: string; art: StepArt };

  const STEPS: readonly StepDef[] = [
    { id: "units", label: "Units and timezone", short: "Units", art: "units" },
    { id: "patient", label: "Patient profile", short: "Patient", art: "who" },
    { id: "finish", label: "Finish", short: "Done", art: "done" },
  ] as const;

  // Relationship is always Self and path is always fresh
  const voice = patientVoice({ relationship: PatientRelationship.Self, patientName: "" });

  let stepIndex = $state(0);
  const currentStep = $derived(STEPS[stepIndex]);
  const progressPct = $derived(STEPS.length <= 1 ? 100 : (stepIndex / (STEPS.length - 1)) * 100);

  let stepCard = $state<HTMLElement>();
  let shownStepId: string | undefined;

  $effect(() => {
    const id = currentStep?.id;
    const previous = shownStepId;
    shownStepId = id;
    if (previous === undefined || previous === id) return;
    void tick().then(() => {
      if (!stepCard) return;
      if (stepCard.getBoundingClientRect().top < 0) {
        const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
        stepCard.scrollIntoView({ block: "start", behavior: reduceMotion ? "auto" : "smooth" });
      }
      const heading = stepCard.querySelector<HTMLElement>("h1");
      if (heading) {
        heading.tabIndex = -1;
        heading.focus({ preventScroll: true });
      }
    });
  });

  // ── Step 1: Units & Timezone ──────────────────────────────────────
  const unitsQuery = $derived(
    currentStep?.id === "units"
      ? getUnitsAndTimezone({
          locale: browser ? navigator.language : undefined,
          fromNightscout: false,
        })
      : null
  );
  retainQuery(() => unitsQuery);
  const unitsAnswer = $derived(unitsQuery?.current);
  const browserTimezone = browser
    ? Intl.DateTimeFormat().resolvedOptions().timeZone
    : "";

  let chosenUnits = $state<GlucoseUnits | undefined>(undefined);
  let chosenTimezone = $state<string | undefined>(undefined);
  let unitsError = $state<string | undefined>(undefined);
  let unitsSaving = $state(false);

  const units = $derived(
    chosenUnits ??
      (isGlucoseUnits(unitsAnswer?.glucoseUnits) ? unitsAnswer.glucoseUnits : undefined)
  );
  const timezone = $derived(
    chosenTimezone ?? unitsAnswer?.timezone ?? browserTimezone
  );

  async function handleUnitsContinue() {
    if (unitsSaving) return;
    unitsSaving = true;
    try {
      unitsError = undefined;
      await setPatientRelationship({
        relationship: PatientRelationship.Self,
      }).catch(() => undefined);

      if (units && timezone) {
        await setUnitsAndTimezone({ glucoseUnits: units, timezone });
        applyPreferences({ glucoseUnits: units }, { refreshCookie: true });
      }
      stepIndex = 1;
    } catch (err) {
      unitsError = describeSubmitError(err, "We could not save your units and timezone.");
    } finally {
      unitsSaving = false;
    }
  }

  // ── Step 2: Patient Parameters ────────────────────────────────────
  let sex = $state<string>("male");
  let age = $state<string>("30");
  let weight = $state<string>("70");
  let height = $state<string>("170");
  let activityLevel = $state<string>("moderate");
  let patientSaving = $state(false);
  let patientError = $state<string | undefined>(undefined);
  let initializedSettings = $state<CgmSimSettings | null>(null);

  const sexOptions: Record<string, string> = {
    male: "Uomo (Male)",
    female: "Donna (Female)",
    other: "Altro (Other)",
  };

  const ACTIVITY_OPTIONS = [
    {
      value: "sedentary",
      icon: Sofa,
      title: "Sedentario",
      description: "Lavoro d'ufficio, poca o nessuna attività fisica programmata.",
    },
    {
      value: "moderate",
      icon: User,
      title: "Moderato",
      description: "Camminate regolari o esercizio leggero 2-3 volte a settimana.",
    },
    {
      value: "active",
      icon: Activity,
      title: "Attivo",
      description: "Allenamento aerobico o muscolare 4-5 volte a settimana.",
    },
    {
      value: "athlete",
      icon: Trophy,
      title: "Atleta",
      description: "Allenamento intenso o agonistico quotidiano, alta sensibilità insulinica.",
    },
  ] as const;

  const isPatientValid = $derived.by(() => {
    const a = Number(age);
    const w = Number(weight);
    const h = Number(height);
    return (
      sex.trim().length > 0 &&
      activityLevel.trim().length > 0 &&
      !isNaN(a) && a >= 1 && a <= 150 &&
      !isNaN(w) && w >= 1 && w <= 500 &&
      !isNaN(h) && h >= 20 && h <= 300
    );
  });

  async function handlePatientContinue() {
    if (!isPatientValid || patientSaving) return;
    patientSaving = true;
    patientError = undefined;

    try {
      const saved = await initialize({
        subject: {
          sex,
          age: Math.round(Number(age)),
          weight: Number(weight),
          height: Number(height),
          activityLevel,
        },
      });
      initializedSettings = saved;
      stepIndex = 2;
    } catch (err) {
      patientError = describeSubmitError(err, "Impossibile salvare i parametri del paziente.");
    } finally {
      patientSaving = false;
    }
  }

  // ── Step 3: Finish ────────────────────────────────────────────────
  let finishFailed = $state(false);
  let finishing = $state(false);

  async function handleFinishToDashboard() {
    if (finishing) return;
    finishing = true;
    finishFailed = false;
    try {
      const result = await markSetupComplete().catch(() => undefined);
      if (!result?.completed) {
        finishFailed = true;
        return;
      }
      await goto(resolve(""), { refreshAll: true });
    } finally {
      finishing = false;
    }
  }

  async function handleTakeTour() {
    if (finishing) return;
    finishing = true;
    try {
      await markSetupComplete();
      // eslint-disable-next-line svelte/no-navigation-without-resolve
      await goto(`${resolve("")}?coach=quick-tour`, { refreshAll: true });
    } finally {
      finishing = false;
    }
  }

  function handleBack() {
    if (stepIndex > 0) stepIndex -= 1;
  }

  function handleJumpToStep(index: number) {
    if (index <= stepIndex) stepIndex = index;
  }
</script>

<svelte:head>
  <title>Setup CGMSIM - Nocturne</title>
</svelte:head>

<SetupChrome>
  {#snippet actions()}
    <span class="hidden font-mono text-xs text-muted-foreground sm:inline">
      Step {stepIndex + 1} of {STEPS.length}
    </span>
  {/snippet}

  {#if httpsRequired}
    <div class="w-full max-w-lg mx-auto text-center py-20">
      <div class="rounded-2xl border border-destructive/20 bg-destructive/5 p-8">
        <ShieldAlert class="mx-auto mb-4 h-12 w-12 text-destructive" />
        <h2 class="text-xl font-semibold mb-3">HTTPS required</h2>
        <p class="text-muted-foreground text-sm leading-relaxed">
          Nocturne requires a secure connection. Open this site using
          <strong class="text-foreground">https://</strong> instead of http://.
        </p>
      </div>
    </div>
  {:else}
    <div class="@container/wizard">
      <div
        class="w-full max-w-280 mx-auto grid grid-cols-1 gap-6 @4xl/wizard:grid-cols-[240px_minmax(0,1fr)] @4xl/wizard:gap-10 @6xl/wizard:grid-cols-[320px_minmax(0,1fr)] @6xl/wizard:gap-14 items-start"
      >
        <aside class="@4xl/wizard:sticky @4xl/wizard:top-8">
          <StepSidebar
            path="fresh"
            currentStep={stepIndex}
            steps={STEPS}
            voice={voice}
            onJumpToStep={handleJumpToStep}
          />
        </aside>

        <section
          bind:this={stepCard}
          class="@container/card relative rounded-3xl border bg-card text-card-foreground shadow-sm overflow-clip @4xl/wizard:min-h-135 flex flex-col"
        >
          <div
            class="flex items-center justify-between flex-wrap gap-2.5 px-4 py-3.5 @xl/card:px-7 @xl/card:py-5 border-b"
          >
            <div class="flex items-center gap-3 text-xs text-muted-foreground">
              <span class="font-mono uppercase tracking-wide">
                Step {String(stepIndex + 1).padStart(2, "0")} / {String(STEPS.length).padStart(2, "0")}
              </span>
              <span aria-hidden="true">·</span>
              <span>{currentStep?.short}</span>
            </div>
            <div class="flex items-center gap-3">
              <Badge variant="secondary">
                <Sprout />
                CGMSIM Setup
              </Badge>
              <Progress
                value={progressPct}
                class="h-1 w-30"
                aria-label="Setup progress"
              />
            </div>
          </div>

          <div class="flex-1 px-1 py-3 @lg/card:px-5 @max-4xl/wizard:[&_:is(input,select,textarea,[role=combobox],[role=radio])]:scroll-mb-44">
            <StepArtwork
              art={currentStep?.art ?? "welcome"}
              class="mx-auto mt-4 size-28 @xl/card:size-40 @4xl/wizard:size-48"
            />

            {#if currentStep?.id === "units"}
              <UnitsAndTimezone
                bind:units={() => units, (value) => (chosenUnits = value)}
                bind:timezone={() => timezone, (value) => (chosenTimezone = value)}
                timezoneDetected={chosenTimezone === undefined && !unitsAnswer?.timezone}
                nightscout={unitsAnswer?.nightscout}
                nightscoutUnavailable={unitsAnswer?.nightscoutUnavailable}
                voice={voice}
                error={unitsError}
              />
            {:else if currentStep?.id === "patient"}
              <div class="@container flex flex-col items-center gap-8 px-4 py-8">
                <div class="flex flex-col items-center gap-3 text-center">
                  <h1
                    class="font-brand font-hairline leading-tight tracking-tight text-foreground text-3xl @lg:text-4xl @min-[40rem]:text-5xl"
                  >
                    Parametri del <em class="not-italic font-light text-primary">paziente</em>.
                  </h1>
                  <p class="max-w-140 text-base leading-relaxed text-muted-foreground">
                    Inserisci le caratteristiche fisiologiche e il livello di attività fisica. Da questi valori verranno calcolati automaticamente il fattore di sensibilità insulinica (ISF) e il rapporto insulina/carboidrati (CR).
                  </p>
                </div>

                {#if patientError}
                  <Alert.Root variant="destructive" class="w-full max-w-200">
                    <TriangleAlert />
                    <Alert.Description>{patientError}</Alert.Description>
                  </Alert.Root>
                {/if}

                <div class="w-full max-w-200 grid grid-cols-1 gap-5 @lg:grid-cols-2">
                  <div class="space-y-2">
                    <Label for="cgmsim-sex">Sesso biologico</Label>
                    <Select.Root type="single" bind:value={sex}>
                      <Select.Trigger id="cgmsim-sex" class="w-full h-10">
                        {sexOptions[sex] ?? sex}
                      </Select.Trigger>
                      <Select.Content>
                        {#each Object.entries(sexOptions) as [val, label] (val)}
                          <Select.Item value={val} {label}>{label}</Select.Item>
                        {/each}
                      </Select.Content>
                    </Select.Root>
                  </div>

                  <div class="space-y-2">
                    <Label for="cgmsim-age">Età (anni)</Label>
                    <Input
                      id="cgmsim-age"
                      type="number"
                      min="1"
                      max="150"
                      step="1"
                      bind:value={age}
                      placeholder="30"
                      class="h-10"
                      required
                    />
                  </div>

                  <div class="space-y-2">
                    <Label for="cgmsim-weight">Peso (kg)</Label>
                    <Input
                      id="cgmsim-weight"
                      type="number"
                      min="1"
                      max="500"
                      step="0.5"
                      bind:value={weight}
                      placeholder="70"
                      class="h-10"
                      required
                    />
                  </div>

                  <div class="space-y-2">
                    <Label for="cgmsim-height">Altezza (cm)</Label>
                    <Input
                      id="cgmsim-height"
                      type="number"
                      min="20"
                      max="300"
                      step="1"
                      bind:value={height}
                      placeholder="170"
                      class="h-10"
                      required
                    />
                  </div>
                </div>

                <div class="w-full max-w-200 space-y-3">
                  <Label>Tipo di attività fisica</Label>
                  <RadioGroup.Root
                    class="grid grid-cols-1 gap-4 @lg:grid-cols-2"
                    value={activityLevel}
                    onValueChange={(val) => {
                      if (val) activityLevel = val;
                    }}
                  >
                    {#each ACTIVITY_OPTIONS as option (option.value)}
                      <RadioGroup.Card value={option.value}>
                        {#snippet children({ checked })}
                          <span class="flex w-full flex-col gap-3 p-2">
                            <span
                              class="absolute right-4 top-4 block h-5 w-5 rounded-full border-2 transition-colors {checked
                                ? 'border-primary bg-primary inset-ring-4 inset-ring-card'
                                : 'border-border'}"
                            ></span>
                            <span class="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10">
                              <option.icon class="h-5 w-5 text-primary" />
                            </span>
                            <span class="font-brand text-lg font-normal leading-snug text-foreground">
                              {option.title}
                            </span>
                            <span class="text-xs leading-relaxed text-muted-foreground">
                              {option.description}
                            </span>
                          </span>
                        {/snippet}
                      </RadioGroup.Card>
                    {/each}
                  </RadioGroup.Root>
                </div>
              </div>
            {:else if currentStep?.id === "finish"}
              <div class="@container px-4 py-8">
                <div class="flex flex-col gap-8 max-w-180 mx-auto">
                  <div class="space-y-3">
                    <h1 class="font-brand font-hairline text-4xl @xl:text-5xl leading-tight text-foreground">
                      Il tuo simulatore è <em class="not-italic font-light text-primary">pronto.</em>
                    </h1>
                    <p class="text-lg leading-relaxed text-muted-foreground">
                      Abbiamo configurato il paziente virtuale e calcolato i parametri metabolici iniziali per la simulazione CGMSIM.
                    </p>
                  </div>

                  {#if initializedSettings?.parameters}
                    <div class="grid grid-cols-2 @lg:grid-cols-5 gap-3 rounded-2xl border bg-muted/30 p-5">
                      <div class="space-y-1">
                        <span class="text-xs uppercase font-mono text-muted-foreground">ISF</span>
                        <p class="text-2xl font-semibold text-foreground">
                          {initializedSettings.parameters.isf}
                          <span class="text-xs font-normal text-muted-foreground">mg/dL/U</span>
                        </p>
                      </div>
                      <div class="space-y-1">
                        <span class="text-xs uppercase font-mono text-muted-foreground">CR (I:C)</span>
                        <p class="text-2xl font-semibold text-foreground">
                          {initializedSettings.parameters.cr}
                          <span class="text-xs font-normal text-muted-foreground">g/U</span>
                        </p>
                      </div>
                      <div class="space-y-1">
                        <span class="text-xs uppercase font-mono text-muted-foreground">DIA</span>
                        <p class="text-2xl font-semibold text-foreground">
                          {initializedSettings.parameters.dia}
                          <span class="text-xs font-normal text-muted-foreground">h</span>
                        </p>
                      </div>
                      <div class="space-y-1">
                        <span class="text-xs uppercase font-mono text-muted-foreground">Picco Insulina</span>
                        <p class="text-2xl font-semibold text-foreground">
                          {initializedSettings.parameters.insulinPeak}
                          <span class="text-xs font-normal text-muted-foreground">min</span>
                        </p>
                      </div>
                      <div class="space-y-1">
                        <span class="text-xs uppercase font-mono text-muted-foreground">Assorb. Carbo</span>
                        <p class="text-2xl font-semibold text-foreground">
                          {initializedSettings.parameters.carbsAbsTime}
                          <span class="text-xs font-normal text-muted-foreground">min</span>
                        </p>
                      </div>
                    </div>
                  {/if}

                  {#if finishFailed}
                    <Alert.Root variant="warning">
                      <TriangleAlert />
                      <Alert.Title>Impossibile completare il setup</Alert.Title>
                      <Alert.Description>
                        Verifica la connessione e riprova.
                      </Alert.Description>
                    </Alert.Root>
                  {/if}

                  <div class="flex flex-row flex-wrap items-center gap-3">
                    <Button onclick={handleFinishToDashboard} disabled={finishing}>
                      {#if finishing}
                        <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                      {/if}
                      Vai alla dashboard
                      <ArrowRight class="ml-2 h-4 w-4" />
                    </Button>
                    <Button variant="ghost" onclick={handleTakeTour} disabled={finishing}>
                      Fai il tour di 60 secondi
                    </Button>
                  </div>
                </div>
              </div>
            {/if}
          </div>

          <div
            class="flex flex-col-reverse gap-3 @lg/card:flex-row @lg/card:justify-between @lg/card:items-center px-4 py-3.5 @xl/card:px-7 @xl/card:py-4.5 border-t bg-background @4xl/wizard:bg-muted/40 sticky bottom-0 z-10 @4xl/wizard:static *:@lg/card:w-auto *:w-full"
          >
            <div class="empty:hidden [&>button]:w-full @lg/card:[&>button]:w-auto">
              {#if stepIndex > 0 && currentStep?.id !== "finish"}
                <Button variant="outline" onclick={handleBack}>
                  <ArrowLeft class="h-4 w-4" />
                  Back
                </Button>
              {/if}
            </div>
            <div class="flex flex-col-reverse gap-3 @lg/card:flex-row @lg/card:items-center *:w-full *:@lg/card:w-auto">
              {#if currentStep?.id === "units"}
                <Button
                  onclick={handleUnitsContinue}
                  disabled={unitsSaving || !units || !timezone}
                >
                  {#if unitsSaving}
                    <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                  {/if}
                  Continue
                  <ArrowRight class="h-4 w-4" />
                </Button>
              {:else if currentStep?.id === "patient"}
                <Button
                  onclick={handlePatientContinue}
                  disabled={patientSaving || !isPatientValid}
                >
                  {#if patientSaving}
                    <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                  {/if}
                  Calcola e continua
                  <ArrowRight class="h-4 w-4" />
                </Button>
              {:else if currentStep?.id === "finish"}
                <Button onclick={handleFinishToDashboard} disabled={finishing}>
                  Vai alla dashboard
                  <ArrowRight class="h-4 w-4" />
                </Button>
              {/if}
            </div>
          </div>
        </section>
      </div>
    </div>
  {/if}
</SetupChrome>
