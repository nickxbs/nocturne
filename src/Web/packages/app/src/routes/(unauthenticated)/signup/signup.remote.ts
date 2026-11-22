import { command, getRequestEvent } from "$app/server";
import { z } from "zod";
import { error } from "@sveltejs/kit";
import { getApiBaseUrl } from "#lib/server/api-client-factory.js";

const signupInviteSchema = z.object({
  email: z.string().email("Inserisci un indirizzo email valido."),
});

export interface SignupInviteResult {
  url: string;
  inviteUrl: string;
  expiresAt: string;
  emailSent?: boolean;
}

export const requestSignupInvite = command(
  signupInviteSchema,
  async ({ email }): Promise<SignupInviteResult> => {
    const event = getRequestEvent();
    if (!event) {
      throw error(500, "Request context unavailable");
    }

    const apiBaseUrl = getApiBaseUrl();
    if (!apiBaseUrl) {
      throw error(500, "API server not configured");
    }

    const host =
      event.request.headers.get("x-forwarded-host") ??
      event.request.headers.get("host") ??
      "";
    const proto =
      event.request.headers.get("x-forwarded-proto") ??
      (event.url.protocol.replace(":", ""));

    const response = await event.fetch(`${apiBaseUrl}/api/v4/signup-invite`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Forwarded-Host": host,
        "X-Forwarded-Proto": proto,
      },
      body: JSON.stringify({ email: email.trim() }),
    });

    if (!response.ok) {
      let message = "Impossibile completare la registrazione in questo momento.";
      try {
        const body = (await response.json()) as { detail?: string; title?: string };
        message = body.detail ?? body.title ?? message;
      } catch {
        // Fallback default message
      }
      throw error(response.status, message);
    }

    return (await response.json()) as SignupInviteResult;
  }
);

