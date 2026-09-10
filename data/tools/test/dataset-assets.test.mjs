import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { resolveDatasetAsset, sha256File } from "../shared/dataset-assets.mjs";

async function temporaryRecord(t) {
  const root = await mkdtemp(path.join(tmpdir(), "appetee-assets-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const recordDir = path.join(root, "0001_Record");
  const assetsDir = path.join(recordDir, "assets");
  await mkdir(assetsDir, { recursive: true });
  const recordPath = path.join(recordDir, "record.json");
  await writeFile(recordPath, "{}", "utf8");
  return { root, recordDir, assetsDir, recordPath };
}

test("dataset asset resolution rejects path traversal", async (t) => {
  const fixture = await temporaryRecord(t);
  const outside = path.join(fixture.root, "outside.avif");
  await writeFile(outside, "outside", "utf8");
  await assert.rejects(
    resolveDatasetAsset(fixture.recordPath, "../outside.avif"),
    /escapes its record directory/u,
  );
});

test("dataset asset resolution detects missing files", async (t) => {
  const fixture = await temporaryRecord(t);
  await assert.rejects(
    resolveDatasetAsset(fixture.recordPath, "assets/missing.avif"),
    /does not exist/u,
  );
});

test("dataset asset resolution rejects non-files", async (t) => {
  const fixture = await temporaryRecord(t);
  const directoryNamedAsAsset = path.join(fixture.assetsDir, "image.avif");
  await mkdir(directoryNamedAsAsset);
  await assert.rejects(
    resolveDatasetAsset(fixture.recordPath, "assets/image.avif"),
    /not a regular file/u,
  );
});

test("dataset asset resolution requires AVIF extension", async (t) => {
  const fixture = await temporaryRecord(t);
  const image = path.join(fixture.assetsDir, "image.jpg");
  await writeFile(image, "image", "utf8");
  await assert.rejects(
    resolveDatasetAsset(fixture.recordPath, "assets/image.jpg"),
    /must use the .avif extension/u,
  );
});

test("dataset asset SHA-256 is stable lowercase hexadecimal", async (t) => {
  const fixture = await temporaryRecord(t);
  const image = path.join(fixture.assetsDir, "image.avif");
  await writeFile(image, "abc", "utf8");
  assert.equal(
    await sha256File(image),
    "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
  );
});
