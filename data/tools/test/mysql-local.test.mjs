import assert from "node:assert/strict";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import {
  GENERATED_SQL_FILE_NAMES,
  parseAndAssertSafeLocalTarget,
  parseMySqlConnectionString,
  requireGeneratedSqlFiles,
} from "../database/mysql-local.mjs";

for (const host of ["localhost", "127.0.0.1", "::1"]) {
  test(`accepts safe local host ${host}`, () => {
    const target = parseAndAssertSafeLocalTarget(
      `Server=${host};Port=3307;Database=appetee;User ID=root;Password=secret;`,
    );
    assert.equal(target.host, host);
    assert.equal(target.port, 3307);
    assert.equal(target.database, "appetee");
  });
}

for (const host of ["mysql", "db.internal", "10.0.0.5", "8.8.8.8", "appetee.mysql.database.azure.com", ""]) {
  test(`rejects unsafe host ${host || "<empty>"}`, () => {
    assert.throws(
      () => parseAndAssertSafeLocalTarget(`Server=${host};Database=appetee;AllowDbReset=true;`),
      /host must be/u,
    );
  });
}

for (const database of ["appetee-dev", "Appetee", "test", ""]) {
  test(`rejects database ${database || "<empty>"}`, () => {
    assert.throws(
      () => parseAndAssertSafeLocalTarget(`Host=localhost;Initial Catalog=${database};`),
      /database must be exactly appetee/u,
    );
  });
}

test("parses project aliases and quoted secret values", () => {
  const parsed = parseMySqlConnectionString(
    'Host=LOCALHOST;Initial Catalog=appetee;User=developer;Password="semi;colon";',
  );
  assert.deepEqual(parsed, {
    host: "localhost",
    port: 3306,
    database: "appetee",
    user: "developer",
    password: "semi;colon",
  });
});

test("requires exactly the fixed generated SQL file list in order", async (t) => {
  const sqlDir = await mkdtemp(path.join(tmpdir(), "appetee-sql-"));
  t.after(() => rm(sqlDir, { recursive: true, force: true }));
  for (const fileName of [...GENERATED_SQL_FILE_NAMES, "00-extra.sql", "05-extra.sql"]) {
    await writeFile(path.join(sqlDir, fileName), `SELECT '${fileName}';`, "utf8");
  }

  const files = await requireGeneratedSqlFiles({ sqlDir });
  assert.deepEqual(files.map((file) => file.fileName), GENERATED_SQL_FILE_NAMES);
});
