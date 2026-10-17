import assert from "node:assert/strict";
import { mkdtemp, readFile, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { writeGeneratedArtifacts } from "../generation/generate.mjs";

test("freshness check accepts matching content without rewriting files", async (t) => {
  const root = await mkdtemp(path.join(tmpdir(), "appetee-artifacts-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const file = path.join(root, "schema.sql");
  const artifacts = new Map([[file, "-- expected\n"]]);
  await writeGeneratedArtifacts(artifacts);
  await writeGeneratedArtifacts(artifacts, { check: true });
  assert.equal(await readFile(file, "utf8"), "-- expected\n");
});

test("freshness check rejects stale and missing files and does not repair them", async (t) => {
  const root = await mkdtemp(path.join(tmpdir(), "appetee-artifacts-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const stale = path.join(root, "stale.sql");
  const missing = path.join(root, "missing.sql");
  await writeFile(stale, "original");
  await assert.rejects(writeGeneratedArtifacts(new Map([[stale, "new"], [missing, "new"]]), { check: true }),
    /Generated artifacts are missing or stale/u);
  assert.equal(await readFile(stale, "utf8"), "original");
  await assert.rejects(readFile(missing), { code: "ENOENT" });
});

test("generation creates missing directories and replaces stale outputs", async (t) => {
  const root = await mkdtemp(path.join(tmpdir(), "appetee-artifacts-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const file = path.join(root, "nested", "output.sql");
  await writeGeneratedArtifacts(new Map([[file, "first"]]));
  await writeGeneratedArtifacts(new Map([[file, "second"]]));
  assert.equal(await readFile(file, "utf8"), "second");
});

test("freshness check tolerates Git CRLF checkout without changing file bytes", async (t) => {
  const root = await mkdtemp(path.join(tmpdir(), "appetee-artifacts-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const file = path.join(root, "schema.sql");
  await writeFile(file, "-- first\r\n-- second\r\n");
  await writeGeneratedArtifacts(new Map([[file, "-- first\n-- second\n"]]), { check: true });
  assert.equal(await readFile(file, "utf8"), "-- first\r\n-- second\r\n");
});
