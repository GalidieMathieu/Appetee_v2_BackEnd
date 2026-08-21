import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const sharedDir = path.dirname(fileURLToPath(import.meta.url));
export const backendRoot = path.resolve(sharedDir, "..", "..", "..");
const defaultApiConfigDir = path.join(backendRoot, "src", "Appetee.Api");

export function parseJsonWithBom(text, source = "configuration") {
  try {
    return JSON.parse(String(text).replace(/^\uFEFF/u, ""));
  } catch (error) {
    throw new Error(`Could not parse ${source} as JSON: ${error.message}`, { cause: error });
  }
}

async function readConfigFile(filePath, { optional = false } = {}) {
  try {
    return parseJsonWithBom(await readFile(filePath, "utf8"), filePath);
  } catch (error) {
    if (optional && error?.code === "ENOENT") return {};
    throw error;
  }
}

function nonEmpty(value) {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

export async function loadToolConfig({ env = process.env, apiConfigDir = defaultApiConfigDir } = {}) {
  const base = await readConfigFile(path.join(apiConfigDir, "appsettings.json"));
  const development = await readConfigFile(
    path.join(apiConfigDir, "appsettings.Development.json"),
    { optional: true },
  );

  return {
    databaseConnectionString:
      nonEmpty(env.ConnectionStrings__AppeteeDb)
      ?? nonEmpty(development.ConnectionStrings?.AppeteeDb)
      ?? nonEmpty(base.ConnectionStrings?.AppeteeDb),
    azureStorage: {
      accountUrl:
        nonEmpty(env.AzureStorage__AccountUrl)
        ?? nonEmpty(development.AzureStorage?.AccountUrl)
        ?? nonEmpty(base.AzureStorage?.AccountUrl),
      containerName:
        nonEmpty(env.AzureStorage__ContainerName)
        ?? nonEmpty(development.AzureStorage?.ContainerName)
        ?? nonEmpty(base.AzureStorage?.ContainerName),
    },
  };
}

export async function loadDatabaseConfig(options) {
  const config = await loadToolConfig(options);
  if (!config.databaseConnectionString) {
    throw new Error(
      "Missing ConnectionStrings__AppeteeDb and ConnectionStrings:AppeteeDb in development/base appsettings.",
    );
  }

  return { connectionString: config.databaseConnectionString };
}

export async function loadAzureStorageConfig(options) {
  const config = await loadToolConfig(options);
  if (!config.azureStorage.accountUrl) {
    throw new Error("Missing AzureStorage__AccountUrl and AzureStorage:AccountUrl in appsettings.");
  }
  if (!config.azureStorage.containerName) {
    throw new Error("Missing AzureStorage__ContainerName and AzureStorage:ContainerName in appsettings.");
  }

  let accountUrl;
  try {
    accountUrl = new URL(config.azureStorage.accountUrl);
  } catch (error) {
    throw new Error("Azure Storage account URL must be a valid HTTPS URL.", { cause: error });
  }
  if (
    accountUrl.protocol !== "https:"
    || accountUrl.username
    || accountUrl.password
    || accountUrl.search
    || accountUrl.hash
    || (accountUrl.pathname !== "/" && accountUrl.pathname !== "")
  ) {
    throw new Error("Azure Storage account URL must be an HTTPS origin without credentials, a path, query, or fragment.");
  }

  const containerName = config.azureStorage.containerName;
  if (
    !/^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$/u.test(containerName)
    || containerName.includes("--")
  ) {
    throw new Error("Azure Storage container name must satisfy Azure's lowercase 3-63 character naming rules.");
  }

  return { accountUrl: accountUrl.origin, containerName };
}

export function redactDatabaseTarget(target) {
  return `${target.host}:${target.port}/${target.database}`;
}
