<script lang="ts">
  import * as Card from "#lib/components/ui/card/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import Check from "@lucide/svelte/icons/check";
  import Fingerprint from "@lucide/svelte/icons/fingerprint-pattern";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import ArrowLeft from "@lucide/svelte/icons/arrow-left";
  import ArrowRight from "@lucide/svelte/icons/arrow-right";
  import Globe from "@lucide/svelte/icons/globe";
  import User from "@lucide/svelte/icons/user";
  import Building2 from "@lucide/svelte/icons/building-2";
  import Clock from "@lucide/svelte/icons/clock";
  import {
    startRegistration,
    type PublicKeyCredentialCreationOptionsJSON,
  } from "@simplewebauthn/browser";
  import {
    signupOptions,
    signupComplete,
    validateSignupUsername,
    validateSignupSlug,
  } from "./signup.remote";
  import {
    FormError,
    FormField,
    useAvailability,
  } from "#lib/forms/index.js";
  import {
    describePasskeyError,
    parseCeremonyOptions,
  } from "#lib/components/auth/passkey-errors.js";
  import RecoveryCodes from "#lib/components/auth/RecoveryCodes.svelte";
  import { resolve } from "$app/paths";
  import { goto } from "$app/navigation";

  let { data } = $props();

  let step = $state<1 | 2 | 3>(1);

  // ── Step 1: Account ──────────────────────────────────────────────────
  let displayName = $state("");
  let username = $state("");
  const normalizedUsername = $derived(username.trim().toLowerCase());

  const usernameAvailability = useAvailability(
    () => normalizedUsername,
    (value) => validateSignupUsername({ username: value }),
    { label: "Username" }
  );

  const canProceedStep1 = $derived(
    displayName.trim().length > 0 &&
      normalizedUsername.length >= 2 &&
      usernameAvailability.submittable &&
      !usernameAvailability.validating
  );

  // ── Step 2: Workspace ────────────────────────────────────────────────
  let tenantDisplayName = $state("");
  let tenantSlug = $state("");
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

  const canProceedStep2 = $derived(
    tenantDisplayName.trim().length > 0 &&
      normalizedSlug.length >= 2 &&
      slugAvailability.submittable &&
      !slugAvailability.validating
  );

  // ── Step 3: Passkey Ceremony ─────────────────────────────────────────
  let isRegistering = $state(false);
  let registrationComplete = $state(false);
  let pendingApproval = $state(false);
  let handoffUrl = $state<string | null>(null);
  let recoveryCodes = $state<string[]>([]);
  let passkeyError = $state<string | null>(null);

  async function handlePasskeyRegister() {
    if (isRegistering) return;
    isRegistering = true;
    passkeyError = null;

    try {
      const response = await signupOptions({
        username: normalizedUsername,
        displayName: displayName.trim(),
        tenantSlug: normalizedSlug,
        tenantDisplayName: tenantDisplayName.trim(),
      });

      const options = parseCeremonyOptions<PublicKeyCredentialCreationOptionsJSON>(
        response.options
      );
      const challengeToken = response.challengeToken ?? "";

      const attestation = await startRegistration({ optionsJSON: options });

      const result = await signupComplete({
        username: normalizedUsername,
        tenantSlug: normalizedSlug,
        tenantDisplayName: tenantDisplayName.trim(),
        attestationResponseJson: JSON.stringify(attestation),
        challengeToken,
      });

      registrationComplete = true;
      recoveryCodes = result.recoveryCodes ?? [];
      pendingApproval = result.pendingApproval === true;
      handoffUrl = result.handoffUrl ?? null;
    } catch (err) {
      console.error("Passkey signup registration failed:", err);
      passkeyError = describePasskeyError(
        err,
        "register",
        "Could not register your passkey. Try again."
      );
    } finally {
      isRegistering = false;
    }
  }

  function handleFinish() {
    if (pendingApproval || !handoffUrl) {
      goto(resolve("/(unauthenticated)/auth/login"));
    } else {
      window.location.href = handoffUrl;
    }
  }
</script>

<svelte:head>
  <title>Sign Up - Nocturne</title>
</svelte:head>

<div class="flex flex-1 items-center justify-center p-4">
  <Card.Root class="w-full max-w-lg" data-testid="signup-card">
    <Card.Header class="space-y-2 text-center">
      <div
        class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-primary/10"
      >
        {#if registrationComplete && pendingApproval}
          <Clock class="h-6 w-6 text-primary" />
        {:else if registrationComplete}
          <Check class="h-6 w-6 text-primary" />
        {:else if step === 1}
          <User class="h-6 w-6 text-primary" />
        {:else if step === 2}
          <Building2 class="h-6 w-6 text-primary" />
        {:else}
          <Fingerprint class="h-6 w-6 text-primary" />
        {/if}
      </div>

      {#if !registrationComplete}
        <Card.Title class="text-2xl font-bold">
          {#if step === 1}
            Create your account
          {:else if step === 2}
            Name your workspace
          {:else}
            Register your passkey
          {/if}
        </Card.Title>
        <Card.Description>
          {#if step === 1}
            Choose your display name and username.
          {:else if step === 2}
            Choose an address for your personal Nocturne workspace.
          {:else}
            Register a secure passkey to protect your account and data.
          {/if}
        </Card.Description>

        <!-- Stepper Indicator -->
        <div class="flex items-center justify-center gap-2 pt-2">
          <div
            class="flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold transition-colors {step === 1 ? 'bg-primary text-primary-foreground' : step > 1 ? 'bg-primary/20 text-primary' : 'bg-muted text-muted-foreground'}"
          >
            {#if step > 1}<Check class="h-3.5 w-3.5" />{:else}1{/if}
          </div>
          <div class="h-0.5 w-8 {step > 1 ? 'bg-primary/50' : 'bg-muted'}"></div>
          <div
            class="flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold transition-colors {step === 2 ? 'bg-primary text-primary-foreground' : step > 2 ? 'bg-primary/20 text-primary' : 'bg-muted text-muted-foreground'}"
          >
            {#if step > 2}<Check class="h-3.5 w-3.5" />{:else}2{/if}
          </div>
          <div class="h-0.5 w-8 {step > 2 ? 'bg-primary/50' : 'bg-muted'}"></div>
          <div
            class="flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold transition-colors {step === 3 ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'}"
          >
            3
          </div>
        </div>
      {:else}
        <Card.Title class="text-2xl font-bold">
          {#if pendingApproval}
            Workspace Pending Approval
          {:else}
            Workspace Created
          {/if}
        </Card.Title>
        <Card.Description>
          {#if pendingApproval}
            Your account and workspace were registered. An administrator must approve your workspace before you can sign in.
          {:else}
            Your personal workspace is ready. Save your recovery codes to proceed.
          {/if}
        </Card.Description>
      {/if}
    </Card.Header>

    <Card.Content>
      {#if !registrationComplete}
        {#if step === 1}
          <form
            class="space-y-4"
            onsubmit={(e) => {
              e.preventDefault();
              if (canProceedStep1) step = 2;
            }}
          >
            <FormField label="Full Name" id="signup-displayname" required>
              {#snippet control(field)}
                <Input
                  {...field}
                  bind:value={displayName}
                  placeholder="e.g. Jane Doe"
                  autocomplete="name"
                  autofocus
                />
              {/snippet}
            </FormField>

            <FormField
              label="Username"
              id="signup-username"
              required
              issues={usernameAvailability.error}
            >
              {#snippet control(field)}
                <div class="relative">
                  <Input
                    {...field}
                    bind:value={username}
                    placeholder="e.g. janedoe"
                    autocomplete="username"
                  />
                  <div class="absolute right-3 top-1/2 -translate-y-1/2">
                    {#if usernameAvailability.validating}
                      <Loader2 class="h-4 w-4 animate-spin text-muted-foreground" />
                    {:else if usernameAvailability.valid}
                      <Check class="h-4 w-4 text-success" />
                    {/if}
                  </div>
                </div>
              {/snippet}
            </FormField>

            <div class="flex justify-between pt-4">
              <Button variant="ghost" href={resolve("/(unauthenticated)/auth/login")}>
                <ArrowLeft class="mr-1 h-4 w-4" />
                Sign in instead
              </Button>
              <Button type="submit" disabled={!canProceedStep1}>
                Next: Workspace
                <ArrowRight class="ml-1 h-4 w-4" />
              </Button>
            </div>
          </form>
        {:else if step === 2}
          <form
            class="space-y-4"
            onsubmit={(e) => {
              e.preventDefault();
              if (canProceedStep2) step = 3;
            }}
          >
            <FormField label="Workspace Name" id="signup-tenant-name" required>
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
              label="Workspace Address (Slug)"
              id="signup-tenant-slug"
              required
              issues={slugAvailability.error}
            >
              {#snippet control(field)}
                <div class="relative">
                  <Input
                    {...field}
                    bind:value={tenantSlug}
                    placeholder="e.g. jane"
                  />
                  <div class="absolute right-3 top-1/2 -translate-y-1/2">
                    {#if slugAvailability.validating}
                      <Loader2 class="h-4 w-4 animate-spin text-muted-foreground" />
                    {:else if slugAvailability.valid}
                      <Check class="h-4 w-4 text-success" />
                    {/if}
                  </div>
                </div>
              {/snippet}
            </FormField>

            <!-- URL preview -->
            <div
              class="flex items-center gap-2 rounded-lg border bg-muted/40 px-3 py-2 text-xs text-muted-foreground"
            >
              <Globe class="h-4 w-4 shrink-0 text-primary" />
              <span class="truncate font-mono">
                https://<strong class="text-foreground">{subdomainPreview}</strong>
              </span>
            </div>

            <div class="flex justify-between pt-4">
              <Button variant="ghost" onclick={() => (step = 1)}>
                <ArrowLeft class="mr-1 h-4 w-4" />
                Back
              </Button>
              <Button type="submit" disabled={!canProceedStep2}>
                Next: Passkey
                <ArrowRight class="ml-1 h-4 w-4" />
              </Button>
            </div>
          </form>
        {:else if step === 3}
          <div class="space-y-6">
            <FormError issues={passkeyError} focusOnShow />

            <!-- Summary box -->
            <div class="rounded-lg border bg-card/50 p-4 space-y-2 text-sm">
              <div class="flex justify-between">
                <span class="text-muted-foreground">Account</span>
                <span class="font-medium">{displayName} (@{normalizedUsername})</span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Workspace</span>
                <span class="font-medium">{tenantDisplayName}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-muted-foreground">Subdomain</span>
                <span class="font-mono text-xs">{subdomainPreview}</span>
              </div>
            </div>

            <div class="text-center text-xs text-muted-foreground">
              Passkeys use your device's biometric sensor (Face ID, Touch ID, Windows Hello) or a security key.
            </div>

            <div class="flex justify-between pt-2">
              <Button
                variant="ghost"
                onclick={() => (step = 2)}
                disabled={isRegistering}
              >
                <ArrowLeft class="mr-1 h-4 w-4" />
                Back
              </Button>
              <Button
                data-testid="create-passkey-button"
                onclick={handlePasskeyRegister}
                disabled={isRegistering}
              >
                {#if isRegistering}
                  <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                  Creating Passkey...
                {:else}
                  <Fingerprint class="mr-2 h-4 w-4" />
                  Create Passkey
                {/if}
              </Button>
            </div>
          </div>
        {/if}
      {:else}
        <!-- Completion / Recovery Codes Screen -->
        <div class="space-y-4">
          <RecoveryCodes
            codes={recoveryCodes}
            onContinue={handleFinish}
            continueLabel={pendingApproval ? "Return to Sign In" : "Proceed to Workspace"}
          />
        </div>
      {/if}
    </Card.Content>
  </Card.Root>
</div>
