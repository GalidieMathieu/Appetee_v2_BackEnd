import path from "node:path";
import { fileURLToPath } from "node:url";
import { runValidation } from "../validation/validate.mjs";
import { buildDatasetAssetInventory } from "../shared/dataset-assets.mjs";
import { loadAzureStorageConfig } from "../shared/tool-config.mjs";
import {
  DATASET_BLOB_PREFIX,
  assertAnonymousAccessIsSafe,
  createAzureContainerClient,
  inspectAzureContainer,
  listDatasetBlobs,
  verifyDatasetAssets,
} from "./azure-blobs.mjs";

export function assertNoVerifyArguments(args) {
  if (args.length > 0) {
    throw new Error("images:verify:azure is read-only and does not accept flags or bypass arguments.");
  }
}

function validationFailure(report) {
  const details = report.errors.slice(0, 5).map((error) => `${error.code}: ${error.message}`).join("; ");
  return new Error(`Dataset validation failed before Azure access (${report.errors.length} errors): ${details}`);
}

export async function verifyAzureImages({
  getConfig = loadAzureStorageConfig,
  validateDataset = () => runValidation({ writeReport: false }),
  buildInventory = buildDatasetAssetInventory,
  getContainerClient = createAzureContainerClient,
  inspectContainer = inspectAzureContainer,
  listBlobs = listDatasetBlobs,
  write = console.log,
} = {}) {
  const config = await getConfig();
  const validation = await validateDataset();
  if (!validation.valid) throw validationFailure(validation);
  const inventory = await buildInventory();
  const containerClient = getContainerClient(config);
  const containerState = await inspectContainer(containerClient, config);
  const remoteBlobs = await listBlobs(containerClient);

  assertAnonymousAccessIsSafe({
    containerName: config.containerName,
    containerState,
    assets: inventory,
  });
  const result = verifyDatasetAssets(inventory, remoteBlobs);

  write("Azure dataset image verification\n");
  write(`Account: ${config.accountUrl}`);
  write(`Container: ${config.containerName}`);
  write(`Prefix: ${DATASET_BLOB_PREFIX}`);
  write(`Access: ${containerState.anonymous ? `anonymous ${containerState.publicAccess}` : "non-anonymous"}`);
  write(`Expected: ${inventory.length}`);
  write(`Verified: ${result.unchanged.length}`);
  write(`Missing: ${result.missing.length}`);
  write(`Mismatched: ${result.changed.length}`);
  write(`Remote extras: ${result.extras.length}`);

  for (const asset of result.missing.slice(0, 10)) write(`  missing: ${asset.blobName}`);
  for (const asset of result.changed.slice(0, 10)) {
    write(`  mismatch: ${asset.blobName} (${asset.mismatchReasons.join(", ")})`);
  }
  if (!result.valid) {
    throw new Error(
      `Azure dataset image verification failed: ${result.missing.length} missing and ${result.changed.length} mismatched assets.`,
    );
  }

  write("\nAzure dataset image verification passed.");
  if (result.extras.length > 0) write("Remote extras were retained and did not fail verification.");
  return { config, containerState, inventory, result };
}

export async function main() {
  assertNoVerifyArguments(process.argv.slice(2));
  await verifyAzureImages();
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error(error?.message ?? String(error));
    process.exitCode = 1;
  });
}
