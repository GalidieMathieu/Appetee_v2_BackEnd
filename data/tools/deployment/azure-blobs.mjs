import { DefaultAzureCredential } from "@azure/identity";
import { BlobServiceClient } from "@azure/storage-blob";

export const DATASET_BLOB_PREFIX = "dataset/";
export const DATASET_CONTENT_TYPE = "image/avif";
export const DEFAULT_UPLOAD_CONCURRENCY = 8;

function normalizeMetadata(metadata = {}) {
  return Object.fromEntries(
    Object.entries(metadata).map(([key, value]) => [key.toLowerCase(), String(value)]),
  );
}

export function createAzureContainerClient(
  { accountUrl, containerName },
  {
    credential = new DefaultAzureCredential(),
    createServiceClient = (url, tokenCredential) => new BlobServiceClient(url, tokenCredential),
  } = {},
) {
  return createServiceClient(accountUrl, credential).getContainerClient(containerName);
}

export async function inspectAzureContainer(containerClient, { accountUrl, containerName }) {
  let properties;
  try {
    properties = await containerClient.getProperties();
  } catch (error) {
    if (error?.statusCode === 404 || error?.code === "ContainerNotFound") {
      throw new Error(
        `Azure Blob container does not exist: ${accountUrl}/${containerName}. Create and verify the intended infrastructure before synchronization.`,
        { cause: error },
      );
    }
    throw error;
  }

  const publicAccess = properties.blobPublicAccess ?? "none";
  return {
    publicAccess,
    anonymous: publicAccess === "blob" || publicAccess === "container",
  };
}

export async function listDatasetBlobs(containerClient, { prefix = DATASET_BLOB_PREFIX } = {}) {
  const remote = new Map();
  for await (const blob of containerClient.listBlobsFlat({ prefix, includeMetadata: true })) {
    remote.set(blob.name, {
      contentType: blob.properties?.contentType,
      metadata: normalizeMetadata(blob.metadata),
    });
  }
  return remote;
}

function classifyAsset(asset, remoteBlob) {
  if (!remoteBlob) return { ...asset, state: "missing", mismatchReasons: ["missing"] };

  const metadata = normalizeMetadata(remoteBlob.metadata);
  const mismatchReasons = [];
  if (metadata.sha256 !== asset.sha256) mismatchReasons.push("sha256");
  if (metadata.seedid !== asset.seedId) mismatchReasons.push("seedid");
  if (metadata.assettype !== asset.assetType) mismatchReasons.push("assettype");
  if (remoteBlob.contentType !== DATASET_CONTENT_TYPE) mismatchReasons.push("content-type");

  return {
    ...asset,
    state: mismatchReasons.length === 0 ? "unchanged" : "changed",
    mismatchReasons,
  };
}

export function classifyDatasetAssets(expectedAssets, remoteBlobs) {
  const plan = { missing: [], changed: [], unchanged: [], extras: [] };
  const expectedNames = new Set();

  for (const asset of expectedAssets) {
    if (expectedNames.has(asset.blobName)) {
      throw new Error(`Duplicate expected dataset Blob name: ${asset.blobName}`);
    }
    expectedNames.add(asset.blobName);
    const classified = classifyAsset(asset, remoteBlobs.get(asset.blobName));
    plan[classified.state].push(classified);
  }

  for (const blobName of remoteBlobs.keys()) {
    if (!expectedNames.has(blobName)) plan.extras.push(blobName);
  }
  plan.extras.sort((left, right) => left.localeCompare(right));
  return plan;
}

export function assertAnonymousAccessIsSafe({ containerName, containerState, assets }) {
  if (!containerState.anonymous) return;
  const unapproved = assets.filter((asset) => asset.productionApproved !== true);
  if (unapproved.length === 0) return;

  const sampleSeedIds = [...new Set(unapproved.map((asset) => asset.seedId))].slice(0, 5);
  throw new Error(
    `Refusing Azure synchronization: container ${containerName} allows anonymous ${containerState.publicAccess} access, but ${unapproved.length} dataset assets are not production-approved (sample: ${sampleSeedIds.join(", ")}). Use a non-anonymous development container or approved media; there is no bypass.`,
  );
}

export async function runWithConcurrency(items, worker, concurrency = DEFAULT_UPLOAD_CONCURRENCY) {
  if (!Number.isInteger(concurrency) || concurrency < 1) {
    throw new TypeError(`Concurrency must be a positive integer; received ${concurrency}`);
  }

  const outcomes = new Array(items.length);
  let cursor = 0;
  async function runWorker() {
    while (true) {
      const index = cursor;
      cursor += 1;
      if (index >= items.length) return;
      try {
        outcomes[index] = { status: "fulfilled", value: await worker(items[index], index) };
      } catch (reason) {
        outcomes[index] = { status: "rejected", reason };
      }
    }
  }

  await Promise.all(
    Array.from({ length: Math.min(concurrency, items.length) }, () => runWorker()),
  );
  return outcomes;
}

export async function uploadDatasetAsset(containerClient, asset, { datasetVersion } = {}) {
  const metadata = {
    sha256: asset.sha256,
    seedid: asset.seedId,
    assettype: asset.assetType,
  };
  if (datasetVersion) metadata.datasetversion = datasetVersion;

  const blobClient = containerClient.getBlockBlobClient(asset.blobName);
  await blobClient.uploadFile(asset.localPath, {
    blobHTTPHeaders: { blobContentType: DATASET_CONTENT_TYPE },
    concurrency: 1,
    metadata,
  });
}

export async function applySyncPlan(
  plan,
  {
    concurrency = DEFAULT_UPLOAD_CONCURRENCY,
    datasetVersion,
    dryRun = false,
    uploadAsset,
  } = {},
) {
  const selected = [
    ...plan.missing.map((asset) => ({ action: "uploaded", asset })),
    ...plan.changed.map((asset) => ({ action: "updated", asset })),
  ];

  if (dryRun) {
    return {
      uploaded: plan.missing.length,
      updated: plan.changed.length,
      unchanged: plan.unchanged.length,
      failed: [],
      dryRun: true,
    };
  }
  if (typeof uploadAsset !== "function") {
    throw new TypeError("applySyncPlan requires an uploadAsset function outside dry-run mode");
  }

  const outcomes = await runWithConcurrency(
    selected,
    ({ asset }) => uploadAsset(asset, { datasetVersion }),
    concurrency,
  );
  const failed = outcomes.flatMap((outcome, index) => outcome.status === "rejected"
    ? [{ ...selected[index], error: outcome.reason }]
    : []);
  const successful = outcomes.reduce((count, outcome) => count + (outcome.status === "fulfilled" ? 1 : 0), 0);
  const successfulUploads = outcomes.reduce(
    (count, outcome, index) => count + (outcome.status === "fulfilled" && selected[index].action === "uploaded" ? 1 : 0),
    0,
  );

  return {
    uploaded: successfulUploads,
    updated: successful - successfulUploads,
    unchanged: plan.unchanged.length,
    failed,
    dryRun: false,
  };
}

export function verifyDatasetAssets(expectedAssets, remoteBlobs) {
  const plan = classifyDatasetAssets(expectedAssets, remoteBlobs);
  return {
    valid: plan.missing.length === 0 && plan.changed.length === 0,
    ...plan,
  };
}
