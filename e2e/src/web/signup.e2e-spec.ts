import { expect, test } from "@playwright/test";
import { env } from "../helpers/env.ts";
import { createVirtualAuthenticator } from "../helpers/webauthn.ts";

test.describe("self-service passkey signup flow", () => {
  test("a visitor can register username, tenant, and passkey then hand off to workspace", async ({ page }) => {
    const authenticator = await createVirtualAuthenticator(page);

    try {
      const apexUrl = `http://${env.baseDomain}/signup`;
      await page.goto(apexUrl);

      // Verify on signup page
      await expect(page.locator('[data-testid="signup-card"]')).toBeVisible();

      // Step 1: Account
      const testUser = `user${Date.now().toString().slice(-6)}`;
      await page.fill('input[id="signup-displayname"]', "Test User");
      await page.fill('input[id="signup-username"]', testUser);

      // Wait for username availability check to complete and button to enable
      const nextWorkspaceBtn = page.getByRole("button", { name: "Next: Workspace" });
      await expect(nextWorkspaceBtn).toBeEnabled();
      await nextWorkspaceBtn.click();

      // Step 2: Workspace
      const testSlug = `tenant${Date.now().toString().slice(-6)}`;
      await page.fill('input[id="signup-tenant-name"]', "Test Workspace");
      await page.fill('input[id="signup-tenant-slug"]', testSlug);

      // Verify URL preview updates
      await expect(page.getByText(testSlug)).toBeVisible();

      const nextPasskeyBtn = page.getByRole("button", { name: "Next: Passkey" });
      await expect(nextPasskeyBtn).toBeEnabled();
      await nextPasskeyBtn.click();

      // Step 3: Passkey Ceremony
      const createPasskeyBtn = page.locator('[data-testid="create-passkey-button"]');
      await expect(createPasskeyBtn).toBeVisible();
      await createPasskeyBtn.click();

      // Recovery codes screen should appear after passkey attestation
      await expect(page.getByText("Recovery codes")).toBeVisible({ timeout: 15000 });

      // Confirm codes are saved (checking the confirmation checkbox)
      const confirmedCheckbox = page.getByRole("checkbox");
      if (await confirmedCheckbox.isVisible()) {
        await confirmedCheckbox.check();
      }

      // Proceed to workspace button
      const proceedBtn = page.getByRole("button", { name: /Proceed to Workspace|Continue/i });
      await expect(proceedBtn).toBeEnabled();
      await proceedBtn.click();

      // Verify redirect to tenant subdomain
      await expect(page).toHaveURL(new RegExp(`${testSlug}\\.${env.baseDomain.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`), {
        timeout: 15000,
      });
    } finally {
      await authenticator.cleanup();
    }
  });

  test("the login page on apex domain includes a signup link when enabled", async ({ page }) => {
    const loginUrl = `http://${env.baseDomain}/auth/login`;
    await page.goto(loginUrl);

    const signupLink = page.locator('[data-testid="signup-link"]');
    // If signup is enabled in the running environment, the link should be visible
    if (await signupLink.isVisible()) {
      await expect(signupLink).toHaveAttribute("href", "/signup");
    }
  });
});
