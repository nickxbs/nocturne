<script lang="ts">
  import * as Card from "#lib/components/ui/card/index.js";
  import { Button } from "#lib/components/ui/button/index.js";
  import { Input } from "#lib/components/ui/input/index.js";
  import { Label } from "#lib/components/ui/label/index.js";
  import Check from "@lucide/svelte/icons/check";
  import Copy from "@lucide/svelte/icons/copy";
  import Loader2 from "@lucide/svelte/icons/loader-circle";
  import AlertTriangle from "@lucide/svelte/icons/triangle-alert";
  import ArrowRight from "@lucide/svelte/icons/arrow-right";
  import UserPlus from "@lucide/svelte/icons/user-plus";
  import {
    requestSignupInvite,
    type SignupInviteResult,
  } from "./signup.remote.js";

  let email = $state("");
  let isSubmitting = $state(false);
  let errorMessage = $state<string | null>(null);
  let result = $state<SignupInviteResult | null>(null);
  let copied = $state(false);

  async function handleSubmit(event: SubmitEvent) {
    event.preventDefault();
    if (!email.trim() || isSubmitting) return;

    isSubmitting = true;
    errorMessage = null;

    try {
      const response = await requestSignupInvite({ email: email.trim() });
      result = response;
    } catch (err) {
      if (err instanceof Error) {
        errorMessage = err.message;
      } else if (typeof err === "object" && err !== null && "message" in err) {
        errorMessage = String((err as { message: unknown }).message);
      } else {
        errorMessage = "Si è verificato un errore durante la richiesta.";
      }
    } finally {
      isSubmitting = false;
    }
  }

  async function copyToClipboard(text: string) {
    try {
      await navigator.clipboard.writeText(text);
      copied = true;
      setTimeout(() => {
        copied = false;
      }, 2000);
    } catch {
      // Fallback ignorato
    }
  }

  function resetForm() {
    email = "";
    result = null;
    errorMessage = null;
    copied = false;
  }
</script>

<svelte:head>
  <title>Registrazione - Nocturne</title>
</svelte:head>

<div class="flex flex-1 items-center justify-center p-4">
  <Card.Root class="w-full max-w-md shadow-lg" data-testid="signup-card">
    {#if !result}
      <Card.Header class="space-y-1 text-center">
        <div
          class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10"
        >
          <UserPlus class="h-6 w-6 text-primary" />
        </div>
        <Card.Title class="text-2xl font-bold">Crea il tuo account</Card.Title>
        <Card.Description>
          Inserisci il tuo indirizzo email per avviare la registrazione e configurare il tuo spazio Nocturne.
        </Card.Description>
      </Card.Header>

      <form onsubmit={handleSubmit}>
        <Card.Content class="space-y-4">
          {#if errorMessage}
            <div
              class="flex items-center gap-2 rounded-lg border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive"
              role="alert"
            >
              <AlertTriangle class="h-4 w-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          {/if}

          <div class="space-y-2 text-left">
            <Label for="signup-email">Email</Label>
            <Input
              id="signup-email"
              type="email"
              placeholder="nome@esempio.com"
              bind:value={email}
              required
              disabled={isSubmitting}
              autocomplete="email"
              class="h-10"
            />
          </div>
        </Card.Content>

        <Card.Footer class="flex flex-col gap-3 pt-2">
          <Button
            type="submit"
            class="w-full h-10 font-medium"
            disabled={isSubmitting || !email.trim()}
          >
            {#if isSubmitting}
              <Loader2 class="mr-2 h-4 w-4 animate-spin" />
              <span>Creazione in corso...</span>
            {:else}
              <span>Registrati</span>
              <ArrowRight class="ml-2 h-4 w-4" />
            {/if}
          </Button>

          <div class="text-center text-xs text-muted-foreground">
            Hai già un account?
            <a
              href="/auth/login"
              class="underline underline-offset-4 hover:text-primary transition-colors font-medium ml-1"
            >
              Accedi
            </a>
          </div>
        </Card.Footer>
      </form>
    {:else}
      <Card.Header class="space-y-2 text-center">
        <div
          class="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-green-500/10 text-green-600 dark:text-green-400"
        >
          <Check class="h-6 w-6" />
        </div>
        <Card.Title class="text-2xl font-bold">Invito Creato!</Card.Title>
        <Card.Description>
          È stato creato il nuovo tenant: <span class="font-semibold text-foreground">{result.tenantSlug}</span>.
        </Card.Description>
      </Card.Header>

      <Card.Content class="space-y-4">
        <div class="rounded-lg border border-border bg-muted/40 p-3.5 space-y-2 text-left">
          <div class="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            Link di attivazione
          </div>
          <div class="flex items-center gap-2">
            <input
              type="text"
              readonly
              value={result.url || result.inviteUrl}
              class="flex-1 bg-background text-xs font-mono border rounded px-2.5 py-1.5 select-all focus:outline-none"
            />
            <Button
              type="button"
              variant="outline"
              size="icon"
              class="h-8 w-8 shrink-0"
              onclick={() => copyToClipboard(result?.url || result?.inviteUrl || "")}
              title={copied ? "Copiato!" : "Copia link"}
              aria-label="Copia link"
            >
              {#if copied}
                <Check class="h-4 w-4 text-green-600 dark:text-green-400" />
              {:else}
                <Copy class="h-4 w-4" />
              {/if}
            </Button>
          </div>
          <p class="text-xs text-muted-foreground">
            Usa questo link per completare la configurazione delle tue credenziali.
          </p>
        </div>
      </Card.Content>

      <Card.Footer class="flex flex-col gap-2.5">
        <Button
          href={result.url || result.inviteUrl}
          class="w-full h-10 font-medium"
        >
          <span>Completa la registrazione</span>
          <ArrowRight class="ml-2 h-4 w-4" />
        </Button>

        <Button
          type="button"
          variant="ghost"
          class="w-full text-xs text-muted-foreground"
          onclick={resetForm}
        >
          Registra un altro account
        </Button>
      </Card.Footer>
    {/if}
  </Card.Root>
</div>

