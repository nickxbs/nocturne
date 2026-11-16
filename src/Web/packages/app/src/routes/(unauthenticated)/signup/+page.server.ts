import { error } from "@sveltejs/kit";
import type { PageServerLoad } from "./$types";
import { classifyRequestHost } from "#lib/server/tenantless-host.js";

export const load: PageServerLoad = async ({ request, parent }) => {
  const { kind, slug } = classifyRequestHost(request);
  const { tenantless } = await parent();

  // Questa pagina è accessibile unicamente sull'URL principale (apex o host tenantless).
  // Se la richiesta proviene da un sottodominio di un tenant (es. dev.nocturne.localhost:1612), viene bloccata con 404.
  if (kind === "tenant" || slug !== null) {
    throw error(404, "Pagina non trovata sul sottodominio del tenant.");
  }

  return {
    isMainUrl: kind === "apex" || tenantless,
  };
};

