import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { backendRoot, loadDatabaseConfig, redactDatabaseTarget } from "../shared/tool-config.mjs";
import {
  executeSeedFiles,
  parseAndAssertSafeLocalTarget,
  recreateLocalDatabase,
  requireGeneratedSqlFiles,
} from "./mysql-local.mjs";
import { printVerification, verifyLocalDatabase } from "./verify-local.mjs";

const validateEntry = path.join(backendRoot, "data", "tools", "validation", "validate.mjs");
const generateEntry = path.join(backendRoot, "data", "tools", "generation", "generate.mjs");

export function runNodeEntry(entryPath) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [entryPath], { stdio: "inherit", shell: false });
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      if (code === 0) resolve();
      else reject(new Error(`Node tool failed (${path.basename(entryPath)}; ${signal ? `signal ${signal}` : `exit ${code}`}).`));
    });
  });
}

export function assertNoResetArguments(args) {
  if (args.length > 0) {
    throw new Error("db:reset:local does not accept flags or bypass arguments.");
  }
}

export async function resetLocalDatabase({
  getDatabaseConfig = loadDatabaseConfig,
  runValidation = () => runNodeEntry(validateEntry),
  runGeneration = () => runNodeEntry(generateEntry),
  loadSqlFiles = requireGeneratedSqlFiles,
  recreateDatabase = recreateLocalDatabase,
  seedDatabase = executeSeedFiles,
  verifyDatabase = verifyLocalDatabase,
  write = console.log,
} = {}) {
  write("Appetee local database reset\n");

  write("[1/6] Configuration");
  const { connectionString } = await getDatabaseConfig();
  const target = parseAndAssertSafeLocalTarget(connectionString);
  write(`  Target: ${redactDatabaseTarget(target)}`);
  write("  Safety: local target verified\n");

  write("[2/6] Dataset validation");
  await runValidation();
  write("  Passed\n");

  write("[3/6] SQL generation");
  await runGeneration();
  const sqlFiles = await loadSqlFiles();
  write("  Passed\n");

  write("[4/6] Database recreation");
  await recreateDatabase(target);
  write("  Created: appetee\n");

  write("[5/6] Seed execution");
  await seedDatabase(target, sqlFiles, {
    onFileComplete: (fileName) => write(`  ${fileName.padEnd(20)} passed`),
  });
  write("");

  write("[6/6] Verification");
  const result = await verifyDatabase(target);
  printVerification(result, write);
  write("\nLocal database ready.");
  return result;
}

export async function main() {
  assertNoResetArguments(process.argv.slice(2));
  await resetLocalDatabase();
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error(error?.message ?? String(error));
    process.exitCode = 1;
  });
}
