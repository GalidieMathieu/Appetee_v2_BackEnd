import path from "node:path";
import { fileURLToPath } from "node:url";
import { runValidation } from "../validation/validate.mjs";
import { buildDatasetAssetInventory, loadDatasetVersion } from "../shared/dataset-assets.mjs";
import { loadAzureStorageConfig } from "../shared/tool-config.mjs";
import {
  DATASET_BLOB_PREFIX,
  DEFAULT_UPLOAD_CONCURRENCY,
  applySyncPlan,
  assertAnonymousAccessIsSafe,
  classifyDatasetAssets,
  createAzureContainerClient,
  inspectAzureContainer,
  listDatasetBlobs,
  uploadDatasetAsset,
  verifyDatasetAssets,
} from "./azure-blobs.mjs";

const assetGroups = [
  ["ingredient", "Ingredients"],
  ["recipe-main", "Recipes — main"],
  ["recipe-card", "Recipes — card"],
];

export function parseSyncArguments(args) {
  if (args.length === 0) return { dryRun: false };
  if (args.length === 1 && args[0] === "--dry-run") return { dryRun: true };
  throw new Error("images:sync:azure accepts only the optional --dry-run flag and has no safety bypasses.");
}

function countByType(items, assetType) {
  return items.filter((item) => item.assetType === assetType).length;
}

export function printSyncPlan({ config, containerState, inventory, plan, dryRun }, write = console.log) {
  write("Azure dataset image sync\n");
  write(`Account: ${config.accountUrl}`);
  write(`Container: ${config.containerName}`);
  write(`Prefix: ${DATASET_BLOB_PREFIX}`);
  write(`Access: ${containerState.anonymous ? `anonymous ${containerState.publicAccess}` : "non-anonymous"}`);
  write(`Mode: ${dryRun ? "DRY RUN" : "SYNC"}\n`);

  for (const [assetType, label] of assetGroups) {
    write(label);
    write(`  expected: ${countByType(inventory, assetType)}`);
    write(`  missing: ${countByType(plan.missing, assetType)}`);
    write(`  changed: ${countByType(plan.changed, assetType)}`);
    write(`  unchanged: ${countByType(plan.unchanged, assetType)}\n`);
  }
  write(`Remote extras: ${plan.extras.length}`);
  for (const blobName of plan.extras.slice(0, 10)) write(`  extra: ${blobName}`);
  if (plan.extras.length > 10) write(`  ...and ${plan.extras.length - 10} more`);
  write("");
}

function assertValidDataset(report) {
  if (report.valid) return;
  const details = report.errors.slice(0, 5).map((error) => `${error.code}: ${error.message}`).join("; ");
  throw new Error(`Dataset validation failed before Azure access (${report.errors.length} errors): ${details}`);
}

export async function syncAzureImages({
  dryRun = false,
  concurrency = DEFAULT_UPLOAD_CONCURRENCY,
  getConfig = loadAzureStorageConfig,
  validateDataset = () => runValidation({ writeReport: false }),
  buildInventory = buildDatasetAssetInventory,
  getDatasetVersion = loadDatasetVersion,
  getContainerClient = createAzureContainerClient,
  inspectContainer = inspectAzureContainer,
  listBlobs = listDatasetBlobs,
  uploadAssetToContainer = uploadDatasetAsset,
  write = console.log,
} = {}) {
  const config = await getConfig();
  const validation = await validateDataset();
  assertValidDataset(validation);
  const [inventory, datasetVersion] = await Promise.all([buildInventory(), getDatasetVersion()]);
  const containerClient = getContainerClient(config);
  const containerState = await inspectContainer(containerClient, config);
  const remoteBlobs = await listBlobs(containerClient);
  const plan = classifyDatasetAssets(inventory, remoteBlobs);

  printSyncPlan({ config, containerState, inventory, plan, dryRun }, write);
  assertAnonymousAccessIsSafe({
    containerName: config.containerName,
    containerState,
    assets: inventory,
  });

  const result = await applySyncPlan(plan, {
    concurrency,
    datasetVersion,
    dryRun,
    uploadAsset: (asset, options) => uploadAssetToContainer(containerClient, asset, options),
  });

  if (dryRun) {
    write(`Would upload: ${result.uploaded}`);
    write(`Would update: ${result.updated}`);
    write(`Would skip: ${result.unchanged}`);
    write("\nNo Azure data modified.");
    return { config, containerState, inventory, plan, result };
  }

  write(`Uploaded: ${result.uploaded}`);
  write(`Updated: ${result.updated}`);
  write(`Unchanged: ${result.unchanged}`);
  write(`Failed: ${result.failed.length}`);
  if (result.failed.length > 0) {
    for (const failure of result.failed) {
      write(`  ${failure.asset.blobName}: ${failure.error?.message ?? String(failure.error)}`);
    }
    throw new Error(`${result.failed.length} Azure dataset asset uploads failed.`);
  }

  const verificationRemote = result.uploaded + result.updated > 0
    ? await listBlobs(containerClient)
    : remoteBlobs;
  const verification = verifyDatasetAssets(inventory, verificationRemote);
  if (!verification.valid) {
    throw new Error(
      `Post-sync Azure verification failed: ${verification.missing.length} missing and ${verification.changed.length} mismatched assets.`,
    );
  }
  write("\nPost-sync verification passed.");
  if (verification.extras.length > 0) {
    write(`Remote extras retained: ${verification.extras.length}`);
  }
  return { config, containerState, inventory, plan, result, verification };
}

export async function main() {
  const options = parseSyncArguments(process.argv.slice(2));
  await syncAzureImages(options);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error(error?.message ?? String(error));
    process.exitCode = 1;
  });
}
