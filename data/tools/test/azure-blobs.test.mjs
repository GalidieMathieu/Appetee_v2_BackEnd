import assert from "node:assert/strict";
import test from "node:test";
import {
  applySyncPlan,
  assertAnonymousAccessIsSafe,
  classifyDatasetAssets,
  runWithConcurrency,
  uploadDatasetAsset,
} from "../deployment/azure-blobs.mjs";
import { parseSyncArguments, syncAzureImages } from "../deployment/sync-azure-images.mjs";
import { assertNoVerifyArguments } from "../deployment/verify-azure-images.mjs";

function asset(overrides = {}) {
  return {
    seedId: "ING-0001",
    entityType: "ingredient",
    assetType: "ingredient",
    recordPath: "ingredient.json",
    localPath: "image.avif",
    blobName: "dataset/ingredients/ING-0001/image.avif",
    productionApproved: false,
    sha256: "a".repeat(64),
    ...overrides,
  };
}

function matchingRemote(localAsset, overrides = {}) {
  return {
    contentType: "image/avif",
    metadata: {
      sha256: localAsset.sha256,
      seedid: localAsset.seedId,
      assettype: localAsset.assetType,
      datasetversion: "an-older-version-is-allowed",
    },
    ...overrides,
  };
}

test("classifies remote missing assets and remote extras", () => {
  const localAsset = asset();
  const remote = new Map([["dataset/obsolete.avif", matchingRemote(localAsset)]]);
  const plan = classifyDatasetAssets([localAsset], remote);
  assert.deepEqual(plan.missing.map((item) => item.blobName), [localAsset.blobName]);
  assert.deepEqual(plan.extras, ["dataset/obsolete.avif"]);
});

test("classifies matching managed properties as unchanged despite dataset version mismatch", () => {
  const localAsset = asset();
  const remote = new Map([[localAsset.blobName, matchingRemote(localAsset)]]);
  const plan = classifyDatasetAssets([localAsset], remote);
  assert.equal(plan.unchanged.length, 1);
  assert.equal(plan.changed.length, 0);
});

test("classifies changed SHA-256", () => {
  const localAsset = asset();
  const remoteBlob = matchingRemote(localAsset);
  remoteBlob.metadata.sha256 = "b".repeat(64);
  const plan = classifyDatasetAssets([localAsset], new Map([[localAsset.blobName, remoteBlob]]));
  assert.deepEqual(plan.changed[0].mismatchReasons, ["sha256"]);
});

test("classifies wrong content type", () => {
  const localAsset = asset();
  const remoteBlob = matchingRemote(localAsset, { contentType: "application/octet-stream" });
  const plan = classifyDatasetAssets([localAsset], new Map([[localAsset.blobName, remoteBlob]]));
  assert.deepEqual(plan.changed[0].mismatchReasons, ["content-type"]);
});

test("classifies wrong managed identity metadata", () => {
  const localAsset = asset();
  const remoteBlob = matchingRemote(localAsset);
  remoteBlob.metadata.seedid = "ING-9999";
  remoteBlob.metadata.assettype = "recipe-main";
  const plan = classifyDatasetAssets([localAsset], new Map([[localAsset.blobName, remoteBlob]]));
  assert.deepEqual(plan.changed[0].mismatchReasons, ["seedid", "assettype"]);
});

test("dry-run sync plan performs zero mutations", async () => {
  let mutations = 0;
  const result = await applySyncPlan(
    { missing: [asset()], changed: [asset({ blobName: "dataset/changed.avif" })], unchanged: [], extras: [] },
    { dryRun: true, uploadAsset: async () => { mutations += 1; } },
  );
  assert.equal(mutations, 0);
  assert.deepEqual(
    { uploaded: result.uploaded, updated: result.updated, unchanged: result.unchanged },
    { uploaded: 1, updated: 1, unchanged: 0 },
  );
});

test("bounded worker pool never exceeds configured concurrency", async () => {
  let active = 0;
  let peak = 0;
  const outcomes = await runWithConcurrency(
    Array.from({ length: 20 }, (_, index) => index),
    async (value) => {
      active += 1;
      peak = Math.max(peak, active);
      await new Promise((resolve) => setTimeout(resolve, 2));
      active -= 1;
      return value * 2;
    },
    3,
  );
  assert.ok(peak <= 3);
  assert.equal(outcomes.filter((outcome) => outcome.status === "fulfilled").length, 20);
});

test("Azure upload sets AVIF headers and managed metadata", async () => {
  const calls = [];
  const containerClient = {
    getBlockBlobClient(blobName) {
      return {
        async uploadFile(localPath, options) { calls.push({ blobName, localPath, options }); },
      };
    },
  };
  const localAsset = asset();
  await uploadDatasetAsset(containerClient, localAsset, { datasetVersion: "0.6.0" });
  assert.deepEqual(calls[0], {
    blobName: localAsset.blobName,
    localPath: localAsset.localPath,
    options: {
      blobHTTPHeaders: { blobContentType: "image/avif" },
      concurrency: 1,
      metadata: {
        sha256: localAsset.sha256,
        seedid: localAsset.seedId,
        assettype: localAsset.assetType,
        datasetversion: "0.6.0",
      },
    },
  });
});

test("unapproved media and anonymous container are rejected before upload", async () => {
  let mutations = 0;
  await assert.rejects(
    syncAzureImages({
      getConfig: async () => ({ accountUrl: "https://example.blob.core.windows.net", containerName: "images-dev" }),
      validateDataset: async () => ({ valid: true, errors: [] }),
      buildInventory: async () => [asset()],
      getDatasetVersion: async () => "0.6.0",
      getContainerClient: () => ({}),
      inspectContainer: async () => ({ anonymous: true, publicAccess: "blob" }),
      listBlobs: async () => new Map(),
      uploadAssetToContainer: async () => { mutations += 1; },
      write: () => {},
    }),
    /Refusing Azure synchronization/u,
  );
  assert.equal(mutations, 0);
});

test("anonymous guard permits only fully approved inventories", () => {
  assert.doesNotThrow(() => assertAnonymousAccessIsSafe({
    containerName: "images",
    containerState: { anonymous: true, publicAccess: "container" },
    assets: [asset({ productionApproved: true })],
  }));
});

test("Azure CLIs reject unknown or bypass arguments", () => {
  assert.deepEqual(parseSyncArguments([]), { dryRun: false });
  assert.deepEqual(parseSyncArguments(["--dry-run"]), { dryRun: true });
  assert.throws(() => parseSyncArguments(["--force"]), /no safety bypasses/u);
  assert.doesNotThrow(() => assertNoVerifyArguments([]));
  assert.throws(() => assertNoVerifyArguments(["--force"]), /does not accept flags/u);
});
