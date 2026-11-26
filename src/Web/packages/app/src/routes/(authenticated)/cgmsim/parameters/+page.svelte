<script lang="ts">
  import * as Card from "#lib/components/ui/card/index.js";
  import * as Select from "#lib/components/ui/select/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import { Label } from "#lib/components/ui/label/index.js";
  import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
  import User from "@lucide/svelte/icons/user";
  import Calculator from "@lucide/svelte/icons/calculator";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import Save from "@lucide/svelte/icons/save";
  import { toast } from "svelte-sonner";
  import { describeSubmitError } from "#lib/forms/index.js";
  import {
    getSettings,
    updateSubject,
    updateParameters,
    initialize,
  } from "#lib/api/generated/cgmSims.generated.remote.js";

  const settingsQuery = getSettings();
  const settingsData = $derived(settingsQuery.current);

  let sex = $state<string>("male");
  let age = $state<string>("30");
  let weight = $state<string>("70");
  let height = $state<string>("170");
  let activityLevel = $state<string>("moderate");

  let isf = $state<string>("");
  let dia = $state<string>("");
  let carbsAbsTime = $state<string>("");
  let insulinPeak = $state<string>("");
  let cr = $state<string>("");

  let isSavingSubject = $state(false);
  let isRecalculating = $state(false);
  let isSavingParams = $state(false);
  let initialized = $state(false);

  const sexOptions: Record<string, string> = {
    male: "Maschile",
    female: "Femminile",
    other: "Altro / Non specificato",
  };

  const activityOptions: Record<string, string> = {
    sedentary: "Sedentario (lavoro d'ufficio, poco movimento)",
    moderate: "Moderato (camminate o attività leggera 1-3 volte/settimana)",
    active: "Attivo (allenamento regolare 3-5 volte/settimana)",
    athlete: "Atleta (allenamento intenso o quotidiano)",
  };

  $effect(() => {
    if (settingsData && !initialized) {
      const subj = settingsData.subject;
      if (subj) {
        sex = subj.sex || "male";
        age = subj.age !== undefined && subj.age !== null ? subj.age.toString() : "30";
        weight = subj.weight !== undefined && subj.weight !== null ? subj.weight.toString() : "70";
        const subjAny = subj as Record<string, unknown>;
        height = typeof subjAny.height === "number" ? subjAny.height.toString() : "170";
        activityLevel = typeof subjAny.activityLevel === "string" && subjAny.activityLevel ? subjAny.activityLevel : "moderate";
      }

      const params = settingsData.parameters;
      if (params) {
        isf = params.isf !== undefined && params.isf !== null ? params.isf.toString() : "50";
        dia = params.dia !== undefined && params.dia !== null ? params.dia.toString() : "6";
        carbsAbsTime = params.carbsAbsTime !== undefined && params.carbsAbsTime !== null ? params.carbsAbsTime.toString() : "360";
        insulinPeak = params.insulinPeak !== undefined && params.insulinPeak !== null ? params.insulinPeak.toString() : "75";
        cr = params.cr !== undefined && params.cr !== null ? params.cr.toString() : "10";
      }
      initialized = true;
    }
  });

  function validateSubject(): { ageNum: number; weightNum: number; heightNum: number } | null {
    const ageNum = Number(age);
    if (!age || isNaN(ageNum) || ageNum < 1 || ageNum > 150) {
      toast.error("Inserisci un'età valida tra 1 e 150 anni.");
      return null;
    }

    const weightNum = Number(weight);
    if (!weight || isNaN(weightNum) || weightNum < 1 || weightNum > 500) {
      toast.error("Inserisci un peso valido tra 1 e 500 kg.");
      return null;
    }

    const heightNum = Number(height);
    if (!height || isNaN(heightNum) || heightNum < 30 || heightNum > 300) {
      toast.error("Inserisci un'altezza valida tra 30 e 300 cm.");
      return null;
    }

    return { ageNum: Math.round(ageNum), weightNum, heightNum };
  }

  async function handleSaveSubject(e: SubmitEvent) {
    e.preventDefault();
    const valid = validateSubject();
    if (!valid) return;

    isSavingSubject = true;
    try {
      await updateSubject({
        sex,
        age: valid.ageNum,
        weight: valid.weightNum,
        height: valid.heightNum,
        activityLevel,
      } as Parameters<typeof updateSubject>[0]);

      toast.success("Profilo paziente aggiornato con successo.");
    } catch (err) {
      toast.error(describeSubmitError(err, "Impossibile salvare il profilo del paziente."));
    } finally {
      isSavingSubject = false;
    }
  }

  async function handleRecalculateFromSubject() {
    const valid = validateSubject();
    if (!valid) return;

    isRecalculating = true;
    try {
      const updated = await initialize({
        subject: {
          sex,
          age: valid.ageNum,
          weight: valid.weightNum,
          height: valid.heightNum,
          activityLevel,
        },
      } as Parameters<typeof initialize>[0]);

      if (updated?.parameters) {
        isf = updated.parameters.isf.toString();
        cr = updated.parameters.cr.toString();
        dia = updated.parameters.dia.toString();
        carbsAbsTime = updated.parameters.carbsAbsTime.toString();
        insulinPeak = updated.parameters.insulinPeak.toString();
      }

      toast.success("Profilo salvato e parametri ISF/CR ricalcolati.");
    } catch (err) {
      toast.error(describeSubmitError(err, "Impossibile ricalcolare i parametri dal profilo."));
    } finally {
      isRecalculating = false;
    }
  }

  async function handleSaveParameters(e: SubmitEvent) {
    e.preventDefault();

    const isfNum = Number(isf);
    if (!isf || isNaN(isfNum) || isfNum < 1 || isNaN(isfNum) || isfNum > 1000) {
      toast.error("Inserisci un ISF tra 1 e 1000 mg/dL/U.");
      return;
    }

    const crNum = Number(cr);
    if (!cr || isNaN(crNum) || crNum < 0.1 || crNum > 200) {
      toast.error("Inserisci un rapporto carboidrati (CR) tra 0.1 e 200 g/U.");
      return;
    }

    const diaNum = Number(dia);
    if (!dia || isNaN(diaNum) || diaNum < 0.5 || diaNum > 24) {
      toast.error("Inserisci una durata insulina (DIA) tra 0.5 e 24 ore.");
      return;
    }

    const carbsNum = Number(carbsAbsTime);
    if (!carbsAbsTime || isNaN(carbsNum) || carbsNum < 5 || carbsNum > 1440) {
      toast.error("Inserisci un tempo di assorbimento carboidrati tra 5 e 1440 minuti.");
      return;
    }

    const peakNum = Number(insulinPeak);
    if (!insulinPeak || isNaN(peakNum) || peakNum < 5 || peakNum > 360) {
      toast.error("Inserisci un picco insulinico tra 5 e 360 minuti.");
      return;
    }

    isSavingParams = true;

    try {
      await updateParameters({
        isf: isfNum,
        dia: diaNum,
        carbsAbsTime: Math.round(carbsNum),
        insulinPeak: Math.round(peakNum),
        cr: crNum,
      });

      toast.success("Parametri di simulazione salvati con successo.");
    } catch (err) {
      toast.error(describeSubmitError(err, "Impossibile salvare i parametri di simulazione."));
    } finally {
      isSavingParams = false;
    }
  }
</script>

<svelte:head>
  <title>CGMSIM Settings - Nocturne</title>
</svelte:head>

<div class="space-y-6">
  <!-- Subject / Patient Characteristics Card -->
  <Card.Root>
    <Card.Header>
      <div class="flex items-center gap-2">
        <User class="h-5 w-5 text-primary" />
        <Card.Title>Profilo Paziente (Subject)</Card.Title>
      </div>
      <Card.Description>
        Caratteristiche fisiologiche e livello di attività fisica del paziente virtuale usato dal simulatore CGMSIM.
      </Card.Description>
    </Card.Header>

    <form onsubmit={handleSaveSubject}>
      <Card.Content class="space-y-6">
        <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <!-- Sex -->
          <div class="space-y-2">
            <Label for="subject-sex">Sesso biologico <span class="text-destructive">*</span></Label>
            <Select.Root type="single" bind:value={sex}>
              <Select.Trigger id="subject-sex" class="w-full">
                {sexOptions[sex] ?? sex}
              </Select.Trigger>
              <Select.Content>
                {#each Object.entries(sexOptions) as [val, label] (val)}
                  <Select.Item value={val} {label}>{label}</Select.Item>
                {/each}
              </Select.Content>
            </Select.Root>
          </div>

          <!-- Age -->
          <div class="space-y-2">
            <Label for="subject-age">Età <span class="text-destructive">*</span></Label>
            <div class="relative">
              <Input
                id="subject-age"
                type="number"
                step="1"
                min="1"
                max="150"
                placeholder="30"
                required
                bind:value={age}
              />
              <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                anni
              </span>
            </div>
          </div>

          <!-- Weight -->
          <div class="space-y-2">
            <Label for="subject-weight">Peso <span class="text-destructive">*</span></Label>
            <div class="relative">
              <Input
                id="subject-weight"
                type="number"
                step="0.5"
                min="1"
                max="500"
                placeholder="70"
                required
                bind:value={weight}
              />
              <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                kg
              </span>
            </div>
          </div>

          <!-- Height -->
          <div class="space-y-2">
            <Label for="subject-height">Altezza <span class="text-destructive">*</span></Label>
            <div class="relative">
              <Input
                id="subject-height"
                type="number"
                step="1"
                min="30"
                max="300"
                placeholder="170"
                required
                bind:value={height}
              />
              <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                cm
              </span>
            </div>
          </div>
        </div>

        <!-- Activity Level -->
        <div class="space-y-2 sm:max-w-md">
          <Label for="subject-activity">Livello di attività fisica <span class="text-destructive">*</span></Label>
          <Select.Root type="single" bind:value={activityLevel}>
            <Select.Trigger id="subject-activity" class="w-full">
              {activityOptions[activityLevel] ?? activityLevel}
            </Select.Trigger>
            <Select.Content>
              {#each Object.entries(activityOptions) as [val, label] (val)}
                <Select.Item value={val} {label}>{label}</Select.Item>
              {/each}
            </Select.Content>
          </Select.Root>
          <p class="text-xs text-muted-foreground">
            Influisce sul fabbisogno giornaliero totale (TDD) quando ricalcoli ISF e CR dal profilo paziente.
          </p>
        </div>
      </Card.Content>

      <Card.Footer class="border-t flex flex-wrap items-center justify-between gap-3 pt-6">
        <Button
          type="button"
          variant="outline"
          disabled={isRecalculating || isSavingSubject}
          onclick={() => void handleRecalculateFromSubject()}
        >
          {#if isRecalculating}
            <Loader2 class="mr-2 h-4 w-4 animate-spin" />
            Ricalcolo in corso...
          {:else}
            <Calculator class="mr-2 h-4 w-4" />
            Salva e ricalcola ISF e CR
          {/if}
        </Button>

        <Button type="submit" disabled={isSavingSubject || isRecalculating} class="min-w-36">
          {#if isSavingSubject}
            <Loader2 class="mr-2 h-4 w-4 animate-spin" />
            Salvataggio...
          {:else}
            <Save class="mr-2 h-4 w-4" />
            Salva profilo
          {/if}
        </Button>
      </Card.Footer>
    </form>
  </Card.Root>

  <!-- Simulation Parameters Card -->
  <Card.Root>
    <Card.Header>
      <div class="flex items-center gap-2">
        <SlidersHorizontal class="h-5 w-5 text-primary" />
        <Card.Title>Parametri Metabolici e Cinetici</Card.Title>
      </div>
      <Card.Description>
        Configura i parametri metabolici (ISF, CR) e cinetici (DIA, assorbimento carboidrati, picco insulinico) per il simulatore CGMSIM.
      </Card.Description>
    </Card.Header>

    <form onsubmit={handleSaveParameters}>
      <Card.Content class="space-y-6">
        <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          <!-- ISF -->
          <div class="space-y-2">
            <Label for="param-isf">Insulin Sensitivity Factor (ISF) <span class="text-destructive">*</span></Label>
            <div class="relative">
              <Input
                id="param-isf"
                type="number"
                step="0.5"
                min="1"
                max="1000"
                placeholder="e.g. 50"
                required
                bind:value={isf}
              />
              <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                mg/dL/U
              </span>
            </div>
            <p class="text-xs text-muted-foreground">Discesa glicemica per unità di insulina.</p>
          </div>

          <!-- Carb Ratio -->
          <div class="space-y-2">
            <Label for="param-cr">Carb Ratio (CR) <span class="text-destructive">*</span></Label>
            <div class="relative">
              <Input
                id="param-cr"
                type="number"
                step="0.1"
                min="0.1"
                max="200"
                placeholder="e.g. 10"
                required
                bind:value={cr}
              />
              <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                g/U
              </span>
            </div>
            <p class="text-xs text-muted-foreground">Grammi di carboidrati coperti da 1 unità di insulina.</p>
          </div>

          <!-- DIA -->
          <div class="space-y-2">
            <Label for="param-dia">Duration of Insulin Action (DIA) <span class="text-destructive">*</span></Label>
            <div class="relative">
              <Input
                id="param-dia"
                type="number"
                step="0.1"
                min="0.5"
                max="24"
                placeholder="e.g. 6.0"
                required
                bind:value={dia}
              />
              <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                hours
              </span>
            </div>
            <p class="text-xs text-muted-foreground">Durata d'azione dell'insulina rapida (default: 6.0 h).</p>
          </div>

          <!-- Carb Absorption Time -->
          <div class="space-y-2">
            <Label for="param-carbs-abs">Carb Absorption Time <span class="text-destructive">*</span></Label>
            <div class="relative">
              <Input
                id="param-carbs-abs"
                type="number"
                step="1"
                min="5"
                max="1440"
                placeholder="e.g. 360"
                required
                bind:value={carbsAbsTime}
              />
              <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
                min
              </span>
            </div>
            <p class="text-xs text-muted-foreground">Durata totale dell'assorbimento dei carboidrati (default: 360 min).</p>
          </div>

          <!-- Insulin Peak -->
          <div class="space-y-2">
            <Label for="param-insulin-peak">Insulin Peak Time <span class="text-destructive">*</span></Label>
            <div class="relative">
              <Input
                id="param-insulin-peak"
                type="number"
                step="1"
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
            <p class="text-xs text-muted-foreground">Tempo di picco d'azione dell'insulina rapida (default: 75 min).</p>
          </div>
        </div>
      </Card.Content>

      <Card.Footer class="border-t justify-end pt-6">
        <Button type="submit" disabled={isSavingParams} class="min-w-36">
          {#if isSavingParams}
            <Loader2 class="mr-2 h-4 w-4 animate-spin" />
            Salvataggio...
          {:else}
            <Save class="mr-2 h-4 w-4" />
            Salva parametri
          {/if}
        </Button>
      </Card.Footer>
    </form>
  </Card.Root>
</div>
