<script lang="ts">
  import * as Card from "#lib/components/ui/card/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import Building2 from "@lucide/svelte/icons/building-2";
  import Globe from "@lucide/svelte/icons/globe";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import Check from "@lucide/svelte/icons/check";
  import Clock from "@lucide/svelte/icons/clock";
  import LogOut from "@lucide/svelte/icons/log-out";
  import {
    onboarding as signupOnboarding,
    validateSlug as validateSignupSlug,
  } from "#lib/api/generated/signups.generated.remote.js";
  import { FormError, FormField, useAvailability } from "#lib/forms/index.js";
  import { describeSubmitError } from "#lib/forms/submit-error.js";

  let { data } = $props();

  function suggestSlug(source: string): string {
    const raw = source.includes("@") ? source.split("@")[0]! : source;
    return raw
      .toLowerCase()
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "")
      .slice(0, 30);
  }

  let tenantDisplayName = $state("");
  let tenantSlug = $state("");

  $effect(() => {
    if (!tenantDisplayName) {
      tenantDisplayName = data.userName
        ? `${data.userName}'s Workspace`
        : "My Workspace";
    }
    if (!tenantSlug) {
      tenantSlug = suggestSlug(
        data.userName || data.userEmail || "my-workspace"
      );
    }
  });

  const normalizedSlug = $derived(tenantSlug.trim().toLowerCase());

  const slugAvailability = useAvailability(
    () => normalizedSlug,
    (value) => validateSignupSlug({ slug: value }),
    { label: "Slug" }
  );

  const subdomainPreview = $derived(
    normalizedSlug
      ? `${normalizedSlug}.${data.baseDomain || "nocturne.app"}`
      : `your-workspace.${data.baseDomain || "nocturne.app"}`
  );

  const canSubmit = $derived(
    tenantDisplayName.trim().length > 0 &&
      normalizedSlug.length >= 2 &&
      slugAvailability.submittable &&
      !slugAvailability.validating
  );

  let isSubmitting = $state(false);
  let submitError = $state<string | null>(null);
  let pendingApproval = $state(false);

  async function handleSubmit(e: SubmitEvent) {
    e.preventDefault();
    if (!canSubmit || isSubmitting) return;

    isSubmitting = true;
    submitError = null;

    try {
      const response = await signupOnboarding({
        tenantSlug: normalizedSlug,
        tenantDisplayName: tenantDisplayName.trim(),
      });

      if (response.pendingApproval) {
        pendingApproval = true;
      } else if (response.handoffUrl) {
        window.location.href = response.handoffUrl;
      } else {
        window.location.href = "/";
      }
    } catch (err) {
      submitError = describeSubmitError(
        err,
        "Failed to create workspace. Please try again."
      );
    } finally {
      isSubmitting = false;
    }
  }
</script>

<svelte:head>
  <title>Set up your workspace - Nocturne</title>
</svelte:head>

<div class="flex min-h-[80vh] items-center justify-center p-4">
  {#if !data.signupEnabled}
    <Card.Root class="w-full max-w-lg">
      <Card.Header class="text-center">
        <div
          class="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-muted"
        >
          <Building2 class="h-6 w-6 text-muted-foreground" />
        </div>
        <Card.Title class="text-2xl font-bold">
          Workspace Creation Disabled
        </Card.Title>
        <Card.Description>
          Self-service workspace creation is currently not available on this
          instance.
        </Card.Description>
      </Card.Header>
      <Card.Content class="space-y-4">
        <p class="text-sm text-muted-foreground text-center">
          You are signed in as <span class="font-medium text-foreground">
            {data.userEmail || data.userName || "your account"}
          </span>
          , but your account does not belong to any workspace. Please contact
          your instance administrator for an invite link.
        </p>
        <div class="flex justify-center pt-2">
          <Button variant="outline" href="/auth/logout">
            <LogOut class="mr-2 h-4 w-4" />
            Sign Out
          </Button>
        </div>
      </Card.Content>
    </Card.Root>
  {:else if pendingApproval}
    <Card.Root class="w-full max-w-lg">
      <Card.Header class="text-center">
        <div
          class="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-warning/10 text-warning"
        >
          <Clock class="h-6 w-6" />
        </div>
        <Card.Title class="text-2xl font-bold">
          Workspace Pending Approval
        </Card.Title>
        <Card.Description>
          Your workspace request has been submitted to the instance
          administrator.
        </Card.Description>
      </Card.Header>
      <Card.Content class="space-y-4">
        <p class="text-sm text-muted-foreground text-center">
          An administrator needs to approve your workspace before you can access
          it. You will be able to log in once your request is approved.
        </p>
        <div class="flex justify-center pt-2">
          <Button variant="outline" href="/">Go to Overview</Button>
        </div>
      </Card.Content>
    </Card.Root>
  {:else}
    <Card.Root class="w-full max-w-lg">
      <Card.Header class="text-center">
        <div
          class="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-primary"
        >
          <Building2 class="h-6 w-6" />
        </div>
        <Card.Title class="text-2xl font-bold">
          Set up your workspace
        </Card.Title>
        <Card.Description>
          {#if data.userName}
            Welcome, {data.userName}!
          {:else}
            Welcome!
          {/if}
          Choose a name and address for your personal workspace.
        </Card.Description>
      </Card.Header>
      <Card.Content>
        {#if submitError}
          <div class="mb-4">
            <FormError issues={submitError} />
          </div>
        {/if}

        <form class="space-y-4" onsubmit={handleSubmit}>
          <FormField
            label="Workspace Name"
            id="onboarding-tenant-name"
            required
          >
            {#snippet control(field)}
              <Input
                {...field}
                bind:value={tenantDisplayName}
                placeholder="e.g. Jane's Glucose Tracker"
                autofocus
              />
            {/snippet}
          </FormField>

          <FormField
            label="Workspace URL"
            id="onboarding-tenant-slug"
            required
            issues={slugAvailability.error}
          >
            {#snippet control(field)}
              <div class="relative">
                <Input
                  {...field}
                  bind:value={tenantSlug}
                  placeholder="e.g. janes-clinic"
                />
                <div class="absolute right-3 top-1/2 -translate-y-1/2">
                  {#if slugAvailability.validating}
                    <Loader2
                      class="h-4 w-4 animate-spin text-muted-foreground"
                    />
                  {:else if slugAvailability.valid}
                    <Check class="h-4 w-4 text-success" />
                  {/if}
                </div>
              </div>
            {/snippet}
          </FormField>

          <div class="rounded-lg border bg-muted/50 p-3">
            <div class="flex items-center gap-2 text-sm text-muted-foreground">
              <Globe class="h-4 w-4 shrink-0" />
              <span>Your workspace will be available at:</span>
            </div>
            <p class="mt-1 font-mono text-sm font-medium text-foreground">
              https://{subdomainPreview}
            </p>
          </div>

          <div class="pt-2">
            <Button
              type="submit"
              class="w-full"
              disabled={!canSubmit || isSubmitting}
            >
              {#if isSubmitting}
                <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                Creating workspace...
              {:else}
                Create Workspace
              {/if}
            </Button>
          </div>
        </form>
      </Card.Content>
    </Card.Root>
  {/if}
</div>
