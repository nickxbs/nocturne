import type { PageServerLoad } from "./$types";
import { classifyRequestHost } from "#lib/server/tenantless-host.js";
import {
  transformChartData,
  type TransformedChartData,
} from "#lib/utils/chart-data-transform.js";

const INITIAL_HOURS = 6;
const TOTAL_HOURS = 48;

export const load: PageServerLoad = async ({ locals, request, parent }) => {
  const { apiClient } = locals;
  const { kind } = classifyRequestHost(request);
  const chartData =
    kind !== "apex" &&
    kind !== "dashboard-slug" &&
    (locals.isAuthenticated || locals.isShareHost)
      ? loadChartData(apiClient)
      : null;

  const { tenantless } = await parent();
  if (tenantless) {
    return { initialChartData: null };
  }

  const { initialChartData, initialWindowStart, historicalDataPromise } =
    chartData ?? loadChartData(apiClient);

  return {
    initialChartData: await initialChartData,
    initialWindowStart,
    streamed: {
      historicalChartData: historicalDataPromise,
    },
  };
};

function loadChartData(apiClient: App.Locals["apiClient"]) {
  const now = Date.now();
  const intervalMs = 5 * 60 * 1000;
  const endTime = Math.ceil(now / intervalMs) * intervalMs;
  const initialStartTime = endTime - INITIAL_HOURS * 60 * 60 * 1000;
  const fullStartTime = endTime - TOTAL_HOURS * 60 * 60 * 1000;

  const historicalDataPromise = (async (): Promise<TransformedChartData | null> => {
    try {
      const data = await apiClient.chartData.getDashboardChartData(
        fullStartTime,
        initialStartTime,
        5,
        false,
      );
      return transformChartData(data);
    } catch (err) {
      console.error("Error loading historical chart data:", err);
      return null;
    }
  })();

  const initialChartData = (async (): Promise<TransformedChartData | null> => {
    try {
      const data = await apiClient.chartData.getDashboardChartData(
        initialStartTime,
        endTime,
        5,
        false,
      );
      return transformChartData(data);
    } catch (err) {
      console.error("Error loading initial chart data:", err);
      return null;
    }
  })();

  return {
    initialChartData,
    initialWindowStart: initialStartTime,
    historicalDataPromise,
  };
}

