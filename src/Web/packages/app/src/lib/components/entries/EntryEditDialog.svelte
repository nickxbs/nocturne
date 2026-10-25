<script lang="ts">
  import type { Bolus, CarbIntake, BGCheck, Note, DeviceEvent, BasalInjection } from "$lib/api";
  import type { EntryRecord, EntryCategoryId } from "$lib/constants/entry-categories";
  import { ENTRY_CATEGORIES } from "$lib/constants/entry-categories";
  import * as Dialog from "$lib/components/ui/dialog";
  import * as Sheet from "$lib/components/ui/sheet";
  import { IsMobile } from "$lib/hooks/is-mobile.svelte";
  import { useDialogHistory } from "$lib/hooks/dialog-history.svelte";
  import * as DropdownMenu from "$lib/components/ui/dropdown-menu";
  import { Separator } from "$lib/components/ui/separator";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import BolusSection from "./BolusSection.svelte";
  import CarbIntakeSection, { type PendingFood } from "./CarbIntakeSection.svelte";
  import BGCheckSection from "./BGCheckSection.svelte";
  import NoteSection from "./NoteSection.svelte";
  import DeviceEventSection from "./DeviceEventSection.svelte";
  import BasalInjectionSection from "./BasalInjectionSection.svelte";
  import Plus from "@lucide/svelte/icons/plus";
  import Trash2 from "@lucide/svelte/icons/trash-2";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import Syringe from "@lucide/svelte/icons/syringe";
  import Apple from "@lucide/svelte/icons/apple";
  import Droplet from "@lucide/svelte/icons/droplet";
  import FileText from "@lucide/svelte/icons/file-text";
  import Smartphone from "@lucide/svelte/icons/smartphone";
  import { getDataSourceDisplayName } from "$lib/utils/data-source-display";
  import { toast } from "svelte-sonner";
  import { tryGetRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import { useToastSubmission } from "$lib/forms";
  import { createEntry, updateEntry } from "$api/entries.remote";
  import { toCreateEntryInput, toUpdateEntryInput } from "$api/entry-request";
  import { remove as deleteBolus } from "$api/generated/bolus.generated.remote";
  import {
    deleteCarbIntake,
    addCarbIntakeFood,
  } from "$api/generated/nutritions.generated.remote";
  import { remove as deleteBGCheck } from "$api/generated/bgChecks.generated.remote";
  import { remove as deleteNote } from "$api/generated/notes.generated.remote";
  import { remove as deleteDeviceEvent } from "$api/generated/deviceEvents.generated.remote";
  import { remove as deleteBasalInjection } from "$api/generated/basalInjections.generated.remote";

  const realtimeStore = tryGetRealtimeStore();

  interface Sections {
    bolus: Partial<Bolus> | null;
    carbs: Partial<CarbIntake> | null;
    bgCheck: Partial<BGCheck> | null;
    note: Partial<Note> | null;
    deviceEvent: Partial<DeviceEvent> | null;
    basalInjection: Partial<BasalInjection> | null;
  }

  interface Props {
    open: boolean;
    entry?: EntryRecord | null;
    correlatedRecords?: EntryRecord[];
    onClose: () => void;
  }

  let {
    open = $bindable(),
    entry = null,
    correlatedRecords = [],
    onClose,
  }: Props = $props();

  // On mobile, present the editor as a bottom sheet instead of a centered dialog.
  const isMobile = new IsMobile();

  // Let the browser back button (and mobile back gesture) dismiss the dialog.
  useDialogHistory(
    () => open,
    () => onClose(),
  );

  let sections = $state<Sections>({
    bolus: null,
    carbs: null,
    bgCheck: null,
    note: null,
    deviceEvent: null,
    basalInjection: null,
  });

  let mills = $state<number>(Date.now());
  const deletion = useToastSubmission("Failed to delete entry");
  let carbsPendingFoods = $state<PendingFood[]>([]);

  let saveError = $state<string | null>(null);
  let isSaving = $state(false);

  let isEditing = $derived(entry != null);
  let sourceDisplayName = $derived(
    entry ? getDataSourceDisplayName(entry.data.dataSource ?? entry.data.device) : null,
  );

  function isEntryCategoryId(key: string): key is EntryCategoryId {
    return Object.hasOwn(ENTRY_CATEGORIES, key);
  }

  let activeSectionCount = $derived(
    Object.values(sections).filter((s) => s != null).length,
  );

  let activeSectionKeys = $derived(
    Object.keys(sections)
      .filter(isEntryCategoryId)
      .filter((k) => sections[k] != null),
  );

  let inactiveSectionKeys = $derived(
    Object.keys(ENTRY_CATEGORIES)
      .filter(isEntryCategoryId)
      .filter((k) => sections[k] == null),
  );

  const sectionIcons: Record<EntryCategoryId, typeof Syringe> = {
    bolus: Syringe,
    carbs: Apple,
    bgCheck: Droplet,
    note: FileText,
    deviceEvent: Smartphone,
    basalInjection: Syringe,
  };

  // Populate sections from entry and correlated records when dialog opens
  $effect(() => {
    if (!open) return;

    if (entry) {
      // Reset all sections
      const fresh: Sections = {
        bolus: null,
        carbs: null,
        bgCheck: null,
        note: null,
        deviceEvent: null,
        basalInjection: null,
      };

      // Populate primary entry
      populateSection(fresh, entry);

      // Populate correlated records
      for (const record of correlatedRecords) {
        populateSection(fresh, record);
      }

      sections = fresh;
      mills = entry.data.mills ?? Date.now();
      carbsPendingFoods = [];
    } else {
      // New entry: default to Meal Bolus layout (bolus + carbs)
      sections = {
        bolus: {},
        carbs: {},
        bgCheck: null,
        note: null,
        deviceEvent: null,
        basalInjection: null,
      };
      mills = Date.now();
      carbsPendingFoods = [];
    }
  });

  function populateSection(target: Sections, record: EntryRecord) {
    switch (record.kind) {
      case "bolus":
        target.bolus = { ...record.data };
        break;
      case "carbs":
        target.carbs = { ...record.data };
        break;
      case "bgCheck":
        target.bgCheck = { ...record.data };
        break;
      case "note":
        target.note = { ...record.data };
        break;
      case "deviceEvent":
        target.deviceEvent = { ...record.data };
        break;
      case "basalInjection":
        target.basalInjection = { ...record.data };
        break;
    }
  }

  function addSection(key: EntryCategoryId) {
    sections[key] = {};
  }

  function removeSection(key: EntryCategoryId) {
    if (activeSectionCount <= 1) return;
    sections[key] = null;
  }

  function millsToInputValue(ms: number): string {
    const date = new Date(ms);
    const year = date.getFullYear();
    const month = (date.getMonth() + 1).toString().padStart(2, "0");
    const day = date.getDate().toString().padStart(2, "0");
    const hours = date.getHours().toString().padStart(2, "0");
    const minutes = date.getMinutes().toString().padStart(2, "0");
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

  function inputValueToMills(value: string): number {
    return new Date(value).getTime();
  }

  /** Find the existing record that matches a section, if editing */
  function findExistingRecord(
    kind: EntryCategoryId,
  ): EntryRecord | undefined {
    if (entry?.kind === kind) return entry;
    return correlatedRecords.find((r) => r.kind === kind);
  }

  function hasSectionData(key: EntryCategoryId): boolean {
    const existing = findExistingRecord(key);
    if (existing?.data.id) return true;

    switch (key) {
      case "bolus":
        return (
          sections.bolus?.insulin != null ||
          sections.bolus?.programmed != null ||
          sections.bolus?.delivered != null
        );
      case "carbs":
        return (
          sections.carbs?.carbs != null || carbsPendingFoods.length > 0
        );
      case "bgCheck":
        return sections.bgCheck?.glucose != null;
      case "note":
        return Boolean(sections.note?.text?.trim());
      case "deviceEvent":
        return (
          sections.deviceEvent?.eventType != null ||
          Boolean(sections.deviceEvent?.notes?.trim())
        );
      case "basalInjection":
        return (
          sections.basalInjection?.units != null ||
          sections.basalInjection?.insulinContext?.patientInsulinId != null
        );
    }
  }

  function buildEntryRecord(
    key: EntryCategoryId,
    correlationId?: string,
  ): EntryRecord | null {
    const existing = findExistingRecord(key);
    switch (key) {
      case "bolus": {
        if (!sections.bolus) return null;
        const prev = existing?.kind === "bolus" ? existing.data : undefined;
        const data: Bolus = {
          ...prev,
          ...sections.bolus,
          id: prev?.id,
          mills,
          correlationId: correlationId ?? prev?.correlationId,
        };
        return { kind: "bolus", data };
      }
      case "carbs": {
        if (!sections.carbs) return null;
        const prev = existing?.kind === "carbs" ? existing.data : undefined;
        const data: CarbIntake = {
          ...prev,
          ...sections.carbs,
          id: prev?.id,
          mills,
          correlationId: correlationId ?? prev?.correlationId,
        };
        return { kind: "carbs", data };
      }
      case "bgCheck": {
        if (!sections.bgCheck) return null;
        const prev = existing?.kind === "bgCheck" ? existing.data : undefined;
        const data: BGCheck = {
          ...prev,
          ...sections.bgCheck,
          id: prev?.id,
          mills,
        };
        return { kind: "bgCheck", data };
      }
      case "note": {
        if (!sections.note) return null;
        const prev = existing?.kind === "note" ? existing.data : undefined;
        const data: Note = {
          ...prev,
          ...sections.note,
          id: prev?.id,
          mills,
        };
        return { kind: "note", data };
      }
      case "deviceEvent": {
        if (!sections.deviceEvent) return null;
        const prev = existing?.kind === "deviceEvent" ? existing.data : undefined;
        const data: DeviceEvent = {
          ...prev,
          ...sections.deviceEvent,
          id: prev?.id,
          mills,
        };
        return { kind: "deviceEvent", data };
      }
      case "basalInjection": {
        if (!sections.basalInjection) return null;
        const prev = existing?.kind === "basalInjection" ? existing.data : undefined;
        const data: BasalInjection = {
          ...prev,
          ...sections.basalInjection,
          id: prev?.id,
          mills,
          correlationId: correlationId ?? prev?.correlationId,
        };
        return { kind: "basalInjection", data };
      }
    }
  }

  async function handleSave() {
    isSaving = true;
    saveError = null;

    try {
      const activeKeys = activeSectionKeys.filter(hasSectionData);
      if (activeKeys.length === 0) {
        toast.error("Please enter details for the entry");
        isSaving = false;
        return;
      }

      const existingCorrelationId =
        entry?.data.correlationId ??
        correlatedRecords.find((r) => r.data.correlationId)?.data.correlationId;
      const sharedCorrelationId =
        activeKeys.length > 1
          ? (existingCorrelationId ?? crypto.randomUUID())
          : undefined;

      const promises: Promise<unknown>[] = [];

      for (const key of activeKeys) {
        const record = buildEntryRecord(key, sharedCorrelationId);
        if (!record) continue;

        if (record.data.id) {
          promises.push(updateEntry(toUpdateEntryInput(record)));
        } else {
          if (key === "carbs" && carbsPendingFoods.length > 0) {
            promises.push(
              (async () => {
                const res = await createEntry(toCreateEntryInput(record));
                const newId =
                  res && typeof res === "object" && "id" in res && typeof res.id === "string"
                    ? res.id
                    : undefined;
                if (newId) {
                  for (const pf of carbsPendingFoods) {
                    await addCarbIntakeFood({ id: newId, request: pf.request });
                  }
                }
              })(),
            );
          } else {
            promises.push(createEntry(toCreateEntryInput(record)));
          }
        }
      }

      await Promise.all(promises);
      realtimeStore?.noteTreatmentWrite();
      toast.success(isEditing ? "Entry updated" : "Entry created");
      open = false;
      onClose();
    } catch (err) {
      console.error("Save error:", err);
      saveError = isEditing ? "Failed to update entry" : "Failed to create entry";
      toast.error(saveError);
    } finally {
      isSaving = false;
    }
  }

  async function handleDelete() {
    const target = entry;
    const targetId = target?.data.id;
    if (!targetId) return;

    await deletion.run(async () => {
      const promises: Promise<unknown>[] = [];

      // Delete primary entry
      switch (target.kind) {
        case "bolus":
          promises.push(deleteBolus(targetId));
          break;
        case "carbs":
          promises.push(deleteCarbIntake(targetId));
          break;
        case "bgCheck":
          promises.push(deleteBGCheck(targetId));
          break;
        case "note":
          promises.push(deleteNote(targetId));
          break;
        case "deviceEvent":
          promises.push(deleteDeviceEvent(targetId));
          break;
        case "basalInjection":
          promises.push(deleteBasalInjection(targetId));
          break;
      }

      // Delete correlated records
      for (const record of correlatedRecords) {
        if (!record.data.id) continue;
        switch (record.kind) {
          case "bolus":
            promises.push(deleteBolus(record.data.id));
            break;
          case "carbs":
            promises.push(deleteCarbIntake(record.data.id));
            break;
          case "bgCheck":
            promises.push(deleteBGCheck(record.data.id));
            break;
          case "note":
            promises.push(deleteNote(record.data.id));
            break;
          case "deviceEvent":
            promises.push(deleteDeviceEvent(record.data.id));
            break;
          case "basalInjection":
            promises.push(deleteBasalInjection(record.data.id));
            break;
        }
      }

      const settled = await Promise.allSettled(promises);
      if (settled.some((r) => r.status === "fulfilled")) realtimeStore?.noteTreatmentWrite();
      const failed = settled.find((r) => r.status === "rejected");
      if (failed) throw failed.reason;
      toast.success("Entry deleted");
      open = false;
      onClose();
    });
  }
</script>

{#if isMobile.current}
  <Sheet.Root bind:open onOpenChange={(o) => !o && onClose()}>
    <Sheet.Content
      side="bottom"
      class="max-h-[90vh] overflow-y-auto p-6"
    >
      {@render dialogBody()}
    </Sheet.Content>
  </Sheet.Root>
{:else}
  <Dialog.Root bind:open onOpenChange={(o) => !o && onClose()}>
    <Dialog.Content class="max-w-lg max-h-[85vh] overflow-y-auto">
      {@render dialogBody()}
    </Dialog.Content>
  </Dialog.Root>
{/if}

{#snippet dialogBody()}
    <Dialog.Header>
      <Dialog.Title>
        {isEditing ? "Edit Entry" : "New Entry"}
      </Dialog.Title>
      <Dialog.Description>
        {isEditing
          ? "Edit the details of this entry and its correlated records."
          : "Create a new entry. Add multiple record types to correlate them."}
      </Dialog.Description>
    </Dialog.Header>

    <div class="space-y-4">
      <!-- Shared Timestamp -->
      <div class="space-y-1.5">
        <Label for="entry-timestamp">Date & Time</Label>
        <Input
          id="entry-timestamp"
          type="datetime-local"
          value={millsToInputValue(mills)}
          onchange={(e: Event & { currentTarget: HTMLInputElement }) => {
            const val = e.currentTarget.value;
            if (val) mills = inputValueToMills(val);
          }}
        />
      </div>

      {#if isEditing && sourceDisplayName}
        <div class="flex items-center gap-2 text-sm">
          <span class="text-muted-foreground">Source:</span>
          <span class="font-medium">{sourceDisplayName}</span>
        </div>
      {/if}

      <Separator />

      <!-- Active Sections -->
      {#each activeSectionKeys as key, i (key)}
        {#if i > 0}
          <Separator />
        {/if}

        {#if key === "bolus" && sections.bolus != null}
          <BolusSection
            bind:bolus={sections.bolus}
            onRemove={activeSectionCount > 1 ? () => removeSection("bolus") : undefined}
          />
        {:else if key === "carbs" && sections.carbs != null}
          <CarbIntakeSection
            bind:carbIntake={sections.carbs}
            carbIntakeId={findExistingRecord("carbs")?.data.id}
            bind:pendingFoods={carbsPendingFoods}
            onRemove={activeSectionCount > 1 ? () => removeSection("carbs") : undefined}
          />
        {:else if key === "bgCheck" && sections.bgCheck != null}
          <BGCheckSection
            bind:bgCheck={sections.bgCheck}
            onRemove={activeSectionCount > 1 ? () => removeSection("bgCheck") : undefined}
          />
        {:else if key === "note" && sections.note != null}
          <NoteSection
            bind:note={sections.note}
            onRemove={activeSectionCount > 1 ? () => removeSection("note") : undefined}
          />
        {:else if key === "deviceEvent" && sections.deviceEvent != null}
          <DeviceEventSection
            bind:deviceEvent={sections.deviceEvent}
            onRemove={activeSectionCount > 1
              ? () => removeSection("deviceEvent")
              : undefined}
          />
        {:else if key === "basalInjection" && sections.basalInjection != null}
          <BasalInjectionSection
            bind:injection={sections.basalInjection}
            onRemove={activeSectionCount > 1
              ? () => removeSection("basalInjection")
              : undefined}
          />
        {/if}
      {/each}

      <!-- Add Section Dropdown -->
      {#if inactiveSectionKeys.length > 0}
        <DropdownMenu.Root>
          <DropdownMenu.Trigger>
            {#snippet child({ props }: { props: Record<string, unknown> })}
              <Button {...props} variant="outline" size="sm" class="w-full">
                <Plus class="mr-2 h-4 w-4" />
                Add Section
              </Button>
            {/snippet}
          </DropdownMenu.Trigger>
          <DropdownMenu.Content align="start">
            {#each inactiveSectionKeys as key (key)}
              {@const category = ENTRY_CATEGORIES[key]}
              {@const Icon = sectionIcons[key]}
              <DropdownMenu.Item onclick={() => addSection(key)}>
                <Icon class="mr-2 h-4 w-4" />
                {category.name}
              </DropdownMenu.Item>
            {/each}
          </DropdownMenu.Content>
        </DropdownMenu.Root>
      {/if}

      <!-- Footer -->
      <Dialog.Footer class="gap-2">
        {#if isEditing && entry?.data.id}
          <Button
            type="button"
            variant="destructive"
            onclick={handleDelete}
            disabled={isSaving || deletion.busy}
            class="mr-auto"
          >
            {#if deletion.busy}
              <Loader2 class="mr-2 h-4 w-4 animate-spin" />
              Deleting...
            {:else}
              <Trash2 class="mr-2 h-4 w-4" />
              Delete
            {/if}
          </Button>
        {/if}
        <Button
          type="button"
          variant="outline"
          onclick={onClose}
          disabled={isSaving || deletion.busy}
        >
          Cancel
        </Button>
        <Button
          type="button"
          onclick={handleSave}
          disabled={isSaving || deletion.busy}
        >
          {#if isSaving}
            <Loader2 class="mr-2 h-4 w-4 animate-spin" />
            Saving...
          {:else}
            {isEditing ? "Save Changes" : "Create"}
          {/if}
        </Button>
      </Dialog.Footer>
    </div>
{/snippet}
