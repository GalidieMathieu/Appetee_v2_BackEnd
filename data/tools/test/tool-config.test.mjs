import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import {
  loadAzureStorageConfig,
  loadDatabaseConfig,
  loadToolConfig,
  parseJsonWithBom,
  redactDatabaseTarget,
} from "../shared/tool-config.mjs";

async function configDirectory(t, base, development) {
  const directory = await mkdtemp(path.join(tmpdir(), "appetee-config-"));
  t.after(() => rm(directory, { recursive: true, force: true }));
  await mkdir(directory, { recursive: true });
  await writeFile(path.join(directory, "appsettings.json"), JSON.stringify(base), "utf8");
  if (development !== undefined) {
    await writeFile(
      path.join(directory, "appsettings.Development.json"),
      `\uFEFF${JSON.stringify(development)}`,
      "utf8",
    );
  }
  return directory;
}

test("environment overrides development and development overrides base per value", async (t) => {
  const apiConfigDir = await configDirectory(
    t,
    {
      ConnectionStrings: { AppeteeDb: "base-db" },
      AzureStorage: { AccountUrl: "https://base.example", ContainerName: "base-container" },
    },
    {
      ConnectionStrings: { AppeteeDb: "development-db" },
      AzureStorage: { ContainerName: "development-container" },
    },
  );

  const development = await loadToolConfig({ env: {}, apiConfigDir });
  assert.equal(development.databaseConnectionString, "development-db");
  assert.equal(development.azureStorage.accountUrl, "https://base.example");
  assert.equal(development.azureStorage.containerName, "development-container");

  const environment = await loadToolConfig({
    env: {
      ConnectionStrings__AppeteeDb: "environment-db",
      AzureStorage__AccountUrl: "https://environment.example",
    },
    apiConfigDir,
  });
  assert.equal(environment.databaseConnectionString, "environment-db");
  assert.equal(environment.azureStorage.accountUrl, "https://environment.example");
  assert.equal(environment.azureStorage.containerName, "development-container");
});

test("BOM-containing JSON parses", () => {
  assert.deepEqual(parseJsonWithBom('\uFEFF{"value":42}'), { value: 42 });
});

test("missing database configuration produces an actionable error", async (t) => {
  const apiConfigDir = await configDirectory(t, {}, {});
  await assert.rejects(
    loadDatabaseConfig({ env: {}, apiConfigDir }),
    /Missing ConnectionStrings__AppeteeDb/u,
  );
});

test("Azure configuration is normalized and validated", async (t) => {
  const apiConfigDir = await configDirectory(t, {
    AzureStorage: {
      AccountUrl: "https://appeteeimages.blob.core.windows.net/",
      ContainerName: "recipe-images-dev",
    },
  });
  assert.deepEqual(
    await loadAzureStorageConfig({ env: {}, apiConfigDir }),
    {
      accountUrl: "https://appeteeimages.blob.core.windows.net",
      containerName: "recipe-images-dev",
    },
  );
});

test("Azure configuration rejects secret-bearing URLs and invalid container names", async (t) => {
  const apiConfigDir = await configDirectory(t, {
    AzureStorage: {
      AccountUrl: "https://account.blob.core.windows.net?sig=secret",
      ContainerName: "Invalid_Name",
    },
  });
  await assert.rejects(
    loadAzureStorageConfig({ env: {}, apiConfigDir }),
    /without credentials, a path, query, or fragment/u,
  );
});

test("formatted database target contains no credentials", () => {
  const formatted = redactDatabaseTarget({
    host: "localhost",
    port: 3306,
    database: "appetee",
    user: "root",
    password: "super-secret",
  });
  assert.equal(formatted, "localhost:3306/appetee");
  assert.doesNotMatch(formatted, /root|super-secret/u);
});
