import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { describe, it, expect, beforeEach, vi } from "vitest";
import { remoteQuery, remoteQueryError } from "#lib/test-stubs/remote-resource.js";
import CgmSimProfileCard from "./CgmSimProfileCard.svelte";

const mockSettings = vi.hoisted(() => ({
  subject: {
    sex: "female",
    weight: 62.5,
    age: 28,
  },
  parameters: {
    isf: 45.0,
    dia: 4.5,
    carbsAbsTime: 180,
    insulinPeak: 75,
    cr: 10.5,
  },
  isInitialized: true,
  updatedAt: new Date().toISOString(),
}));

const getSettingsMock = vi.hoisted(() => vi.fn());

vi.mock("#lib/api/generated/cgmSims.generated.remote.js", () => ({
  getSettings: getSettingsMock,
}));

describe("CgmSimProfileCard", () => {
  beforeEach(() => {
    getSettingsMock.mockReset();
    getSettingsMock.mockImplementation(() => remoteQuery(() => mockSettings));
  });

  it("renders subject and parameter values in readonly format", async () => {
    render(CgmSimProfileCard);

    await expect.element(page.getByTestId("cgmsim-profile-card")).toBeVisible();
    await expect.element(page.getByText("CGMSIM Profile")).toBeVisible();

    // Subject fields
    await expect.element(page.getByTestId("cgmsim-subject-sex")).toHaveTextContent("Female");
    await expect.element(page.getByTestId("cgmsim-subject-weight")).toHaveTextContent("62.5 kg");
    await expect.element(page.getByTestId("cgmsim-subject-age")).toHaveTextContent("28 yrs");

    // Parameter fields
    await expect.element(page.getByTestId("cgmsim-param-isf")).toHaveTextContent("45 mg/dL/U");
    await expect.element(page.getByTestId("cgmsim-param-cr")).toHaveTextContent("10.5 g/U");
    await expect.element(page.getByTestId("cgmsim-param-dia")).toHaveTextContent("4.5 h");
    await expect.element(page.getByTestId("cgmsim-param-carbs-abs")).toHaveTextContent("180 min");
    await expect.element(page.getByTestId("cgmsim-param-insulin-peak")).toHaveTextContent("75 min");
  });

  it("provides navigation links to subject and parameters settings", async () => {
    render(CgmSimProfileCard);

    const subjectLink = page.getByRole("link", { name: "Configure Patient" });
    await expect.element(subjectLink).toBeVisible();
    await expect.element(subjectLink).toHaveAttribute("href", "/settings/patient");

    const paramsLink = page.getByRole("link", { name: "Configure Parameters" });
    await expect.element(paramsLink).toBeVisible();
    await expect.element(paramsLink).toHaveAttribute("href", "/cgmsim/parameters");
  });

  it("skips fetching settings when disabled", async () => {
    render(CgmSimProfileCard, { props: { enabled: false } });

    await expect.element(page.getByTestId("cgmsim-profile-card")).toBeVisible();
    await expect.element(page.getByTestId("cgmsim-subject-sex")).toHaveTextContent("-");
    expect(getSettingsMock).not.toHaveBeenCalled();
  });

  it("renders a contained fallback instead of crashing when loading settings fails", async () => {
    getSettingsMock.mockImplementation(() => remoteQueryError(new Error("Forbidden")));

    render(CgmSimProfileCard);

    await expect.element(page.getByTestId("cgmsim-profile-card")).toBeVisible();
    await expect.element(page.getByTestId("cgmsim-profile-error")).toBeVisible();
  });
});
