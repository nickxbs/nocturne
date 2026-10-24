import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { describe, it, expect, vi } from "vitest";
import { remoteQuery } from "$lib/test-stubs/remote-resource";
import CgmSimProfileCard from "./CgmSimProfileCard.svelte";

const mockSettings = vi.hoisted(() => ({
  subject: {
    sex: "female",
    weight: 62.5,
    age: 28,
    height: 168.0,
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

vi.mock("$lib/api/generated/cgmSims.generated.remote", () => ({
  getSettings: () => remoteQuery(() => mockSettings),
}));

describe("CgmSimProfileCard", () => {
  it("renders subject and parameter values in readonly format", async () => {
    render(CgmSimProfileCard);

    await expect.element(page.getByTestId("cgmsim-profile-card")).toBeVisible();
    await expect.element(page.getByText("CGMSIM Profile")).toBeVisible();

    // Subject fields
    await expect.element(page.getByTestId("cgmsim-subject-sex")).toHaveTextContent("Female");
    await expect.element(page.getByTestId("cgmsim-subject-weight")).toHaveTextContent("62.5 kg");
    await expect.element(page.getByTestId("cgmsim-subject-age")).toHaveTextContent("28 yrs");
    await expect.element(page.getByTestId("cgmsim-subject-height")).toHaveTextContent("168 cm");

    // Parameter fields
    await expect.element(page.getByTestId("cgmsim-param-isf")).toHaveTextContent("45 mg/dL/U");
    await expect.element(page.getByTestId("cgmsim-param-cr")).toHaveTextContent("10.5 g/U");
    await expect.element(page.getByTestId("cgmsim-param-dia")).toHaveTextContent("4.5 h");
    await expect.element(page.getByTestId("cgmsim-param-carbs-abs")).toHaveTextContent("180 min");
    await expect.element(page.getByTestId("cgmsim-param-insulin-peak")).toHaveTextContent("75 min");
  });

  it("provides navigation links to subject and parameters settings", async () => {
    render(CgmSimProfileCard);

    const subjectLink = page.getByRole("link", { name: "Configure Subject" });
    await expect.element(subjectLink).toBeVisible();
    await expect.element(subjectLink).toHaveAttribute("href", "/cgmsim/subject");

    const paramsLink = page.getByRole("link", { name: "Configure Parameters" });
    await expect.element(paramsLink).toBeVisible();
    await expect.element(paramsLink).toHaveAttribute("href", "/cgmsim/parameters");
  });
});
