import type { Page, CDPSession } from "@playwright/test";

export interface VirtualAuthenticator {
  authenticatorId: string;
  cdp: CDPSession;
  cleanup: () => Promise<void>;
}

/**
 * Attaches a virtual WebAuthn authenticator to the Playwright page session via Chrome DevTools Protocol.
 * Emulates an internal platform authenticator (e.g., Touch ID / Windows Hello) with presence simulation.
 */
export async function createVirtualAuthenticator(page: Page): Promise<VirtualAuthenticator> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send("WebAuthn.enable");
  const { authenticatorId } = (await cdp.send("WebAuthn.addVirtualAuthenticator", {
    options: {
      protocol: "ctap2",
      transport: "internal",
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
    },
  })) as { authenticatorId: string };

  return {
    authenticatorId,
    cdp,
    cleanup: async () => {
      try {
        await cdp.send("WebAuthn.removeVirtualAuthenticator", { authenticatorId });
        await cdp.send("WebAuthn.disable");
        await cdp.detach();
      } catch {
        // Cleanup errors ignored
      }
    },
  };
}
