import { redirect } from "@sveltejs/kit";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ locals, parent }) => {
  const { tenantless, baseDomain } = await parent();

  if (!tenantless) {
    redirect(302, "/auth/login");
  }

  try {
    const info = await locals.apiClient.metadata.getMultitenancyInfo();
    if (!info?.signupEnabled) {
      redirect(302, "/auth/login");
    }

    return {
      baseDomain: info.baseDomain ?? baseDomain ?? "",
      signupRequiresApproval: info.signupRequiresApproval ?? false,
    };
  } catch {
    redirect(302, "/auth/login");
  }
};
