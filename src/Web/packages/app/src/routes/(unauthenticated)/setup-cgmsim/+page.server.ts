import { redirect } from "@sveltejs/kit";
import type { PageServerLoad } from "./$types";
import { invalidateOnboardingCache } from "#lib/server/onboarding-check.js";

export const load: PageServerLoad = async ({ locals, cookies }) => {
  invalidateOnboardingCache(cookies);

  if (!locals.isAuthenticated) {
    redirect(302, "/auth/login?returnUrl=/setup-cgmsim");
  }

  return {};
};
