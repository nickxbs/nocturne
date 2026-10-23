<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Select from "$lib/components/ui/select";
  import User from "@lucide/svelte/icons/user";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import Save from "@lucide/svelte/icons/save";
  import { toast } from "svelte-sonner";
  import { describeSubmitError } from "$lib/forms";
  import { getSubject, updateSubject } from "$lib/api/generated/cgmSims.generated.remote";

  const subjectQuery = getSubject();
  const subjectData = $derived(subjectQuery.current);

  let sex = $state<string>("");
  let weight = $state<string>("");
  let age = $state<string>("");
  let height = $state<string>("");
  let isSaving = $state(false);
  let initialized = $state(false);

  const sexOptions: Record<string, string> = {
    male: "Male",
    female: "Female",
    other: "Other / Unspecified",
  };

  $effect(() => {
    if (subjectData && !initialized) {
      sex = subjectData.sex ?? "";
      weight = subjectData.weight !== undefined && subjectData.weight !== null ? subjectData.weight.toString() : "";
      age = subjectData.age !== undefined && subjectData.age !== null ? subjectData.age.toString() : "";
      height = subjectData.height !== undefined && subjectData.height !== null ? subjectData.height.toString() : "";
      initialized = true;
    }
  });

  async function handleSave(e: SubmitEvent) {
    e.preventDefault();

    if (!sex) {
      toast.error("Please select a biological sex.");
      return;
    }

    const weightNum = Number(weight);
    if (!weight || isNaN(weightNum) || weightNum <= 0) {
      toast.error("Please enter a valid weight in kg.");
      return;
    }

    const ageNum = Number(age);
    if (!age || isNaN(ageNum) || ageNum <= 0) {
      toast.error("Please enter a valid age in years.");
      return;
    }

    const heightNum = Number(height);
    if (!height || isNaN(heightNum) || heightNum <= 0) {
      toast.error("Please enter a valid height in cm.");
      return;
    }

    isSaving = true;

    try {
      await updateSubject({
        sex,
        weight: weightNum,
        age: Math.round(ageNum),
        height: heightNum,
      });

      toast.success("Subject parameters saved successfully.");
    } catch (err) {
      toast.error(describeSubmitError(err, "Failed to save subject parameters."));
    } finally {
      isSaving = false;
    }
  }
</script>

<svelte:head>
  <title>Subject - CGMSIM - Nocturne</title>
</svelte:head>

<Card.Root>
  <Card.Header>
    <div class="flex items-center gap-2">
      <User class="h-5 w-5 text-primary" />
      <Card.Title>Simulated Subject Profile</Card.Title>
    </div>
    <Card.Description>
      Define physiological traits of the virtual patient used in the simulation model. All fields are required.
    </Card.Description>
  </Card.Header>

  <form onsubmit={handleSave}>
    <Card.Content class="space-y-6">
      <div class="grid gap-6 @sm:grid-cols-2">
        <!-- Sex -->
        <div class="space-y-2">
          <Label for="subject-sex">Biological Sex <span class="text-destructive">*</span></Label>
          <Select.Root type="single" bind:value={sex} required>
            <Select.Trigger id="subject-sex" class="w-full">
              {sex ? (sexOptions[sex] ?? sex) : "Select sex"}
            </Select.Trigger>
            <Select.Content>
              {#each Object.entries(sexOptions) as [value, label] (value)}
                <Select.Item {value} {label} />
              {/each}
            </Select.Content>
          </Select.Root>
          <p class="text-xs text-muted-foreground">Used for metabolic baseline and distribution models.</p>
        </div>

        <!-- Weight -->
        <div class="space-y-2">
          <Label for="subject-weight">Body Weight (kg) <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="subject-weight"
              type="number"
              step="0.1"
              min="1"
              max="350"
              placeholder="e.g. 70"
              required
              bind:value={weight}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              kg
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Subject body weight in kilograms.</p>
        </div>

        <!-- Age -->
        <div class="space-y-2">
          <Label for="subject-age">Age (years) <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="subject-age"
              type="number"
              step="1"
              min="1"
              max="120"
              placeholder="e.g. 30"
              required
              bind:value={age}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              years
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Subject age in years.</p>
        </div>

        <!-- Height -->
        <div class="space-y-2">
          <Label for="subject-height">Height (cm) <span class="text-destructive">*</span></Label>
          <div class="relative">
            <Input
              id="subject-height"
              type="number"
              step="0.5"
              min="30"
              max="260"
              placeholder="e.g. 175"
              required
              bind:value={height}
            />
            <span class="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-muted-foreground pointer-events-none">
              cm
            </span>
          </div>
          <p class="text-xs text-muted-foreground">Subject height in centimeters.</p>
        </div>
      </div>
    </Card.Content>

    <Card.Footer class="border-t justify-end">
      <Button type="submit" disabled={isSaving} class="min-w-32">
        {#if isSaving}
          <Loader2 class="mr-2 h-4 w-4 animate-spin" />
          Saving...
        {:else}
          <Save class="mr-2 h-4 w-4" />
          Save Subject
        {/if}
      </Button>
    </Card.Footer>
  </form>
</Card.Root>
