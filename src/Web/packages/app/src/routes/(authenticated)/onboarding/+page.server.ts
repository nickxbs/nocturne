import { isRedirect, redirect } from "@sveltejs/kit";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ locals, parent }) => {
  const { apiClient, user } = locals;
  const { baseDomain } = await parent();

  let signupEnabled = true;
  let signupRequiresApproval = false;

  try {
    const multitenancy = await apiClient.metadata.getMultitenancyInfo();
    signupEnabled = multitenancy.signupEnabled ?? true;
    signupRequiresApproval = multitenancy.signupRequiresApproval ?? false;
  } catch (err) {
    console.error("Failed to load multitenancy info in onboarding:", err);
  }

  // If the user already has a tenant, redirect them to apex /
  try {
    const tenants = await apiClient.myTenants.getMyTenants();
    if (tenants.length > 0) {
      throw redirect(303, "/");
    }
  } catch (err) {
    if (isRedirect(err)) throw err;
    console.error("Failed to check tenant memberships in onboarding:", err);
  }

  return {
    signupEnabled,
    signupRequiresApproval,
    baseDomain: baseDomain ?? "nocturne.app",
    userName: user?.name ?? "",
    userEmail: user?.email ?? "",
  };
};
