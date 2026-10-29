// Builds the demo site: the dashboard page plus dummy run data with generated screenshots.
import { copyFileSync, mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const arg = process.argv.indexOf("--site");
const site = resolve(arg > 0 ? process.argv[arg + 1] : join(here, "..", "_site"));

const shot = (title, bad) => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="400"><rect width="640" height="400" fill="#fff"/><rect width="640" height="44" fill="${bad ? "#c0392b" : "#2563eb"}"/><text x="16" y="29" font-family="sans-serif" font-size="18" fill="#fff">Demo app</text><text x="320" y="215" font-family="sans-serif" font-size="22" text-anchor="middle" fill="#1c2430">${title}</text></svg>`;
  return "data:image/svg+xml;utf8," + encodeURIComponent(svg);
};
const t = (name, categories, status, durationMs, extra = {}) => ({
  name, categories, status, durationMs, previous: "passed", url: "https://demo.example.test/",
  screenshot: shot(name.split(".").pop().replaceAll("_", " "), status === "failed"), ...extra,
});
const failure = (error, step) => ({
  error, failedStep: 2, steps: ["Open /items", "Click 'Add item'", step], console: ["[error] Failed to load resource: 404"],
  stack: "   at Demo.Items.ItemsTests.Run() in ItemsTests.cs:line 42\n   at Xunit.Sdk.TestInvoker.Run()",
});

const data = {
  title: "Demo UI tests", runId: "20261023143248", previousRunId: "20261010090000", environment: "demo",
  baseUrl: "https://demo.example.test", startedAt: "2026-10-23T14:32:48Z", elapsedMs: 13000,
  tests: [
    t("Demo.Items.ItemsTests.Delete_item_removes_it_from_the_list", ["Smoke"], "passed", 2700),
    t("Demo.Items.ItemsTests.Add_item_shows_it_in_the_list", ["Smoke"], "passed", 6400),
    t("Demo.Items.ItemsTests.Blank_name_is_rejected", ["Regression"], "failed", 3200,
      failure('Assert.Equal() Failure\nExpected: "Name is required."\nActual:   ""', "Wait for validation message")),
    t("Demo.Items.ItemsTests.Item_list_pagination", ["Smoke"], "skipped", 3900),
    t("Demo.Items.ItemsTests.Edit_item_name", ["Smoke"], "passed", 4800),
    t("Demo.Search.SearchTests.Finds_an_existing_item", ["Smoke"], "passed", 1600),
    t("Demo.Search.SearchTests.Empty_state_is_shown_without_matches", ["Smoke"], "failed", 1900,
      { previous: "failed", ...failure("Expected element '#empty-state' to be visible, but it was not found within 5000ms.", "Wait for empty state") }),
    t("Demo.Search.SearchTests.Search_is_case_insensitive", ["Smoke"], "passed", 2100),
    t("Demo.Health.HealthTests.Api_reports_healthy", ["Smoke"], "passed", 900),
    t("Demo.Health.HealthTests.Page_has_a_title", ["Smoke"], "passed", 1100),
    t("Demo.Health.HealthTests.Page_has_no_console_errors", ["Smoke"], "passed", 1500),
  ],
};

mkdirSync(site, { recursive: true });
copyFileSync(join(here, "..", "dashboard.html"), join(site, "index.html"));
writeFileSync(join(site, "data.json"), JSON.stringify(data));
console.log(`Demo site written to ${site}`);
