import { readFile } from "node:fs/promises";
import path from "node:path";
import mysql from "mysql2/promise";
import { backendRoot } from "../shared/tool-config.mjs";

export const GENERATED_SQL_FILE_NAMES = Object.freeze([
  "01-schema.sql",
  "02-reference.sql",
  "03-ingredients.sql",
  "04-recipes.sql",
]);

const keyAliases = new Map([
  ["server", "host"],
  ["host", "host"],
  ["port", "port"],
  ["database", "database"],
  ["initialcatalog", "database"],
  ["userid", "user"],
  ["user", "user"],
  ["uid", "user"],
  ["password", "password"],
  ["pwd", "password"],
]);

function splitConnectionString(connectionString) {
  const parts = [];
  let current = "";
  let quote = null;

  for (let index = 0; index < connectionString.length; index += 1) {
    const character = connectionString[index];
    if (quote) {
      if (character === quote && connectionString[index + 1] === quote) {
        current += character;
        index += 1;
      } else if (character === quote) {
        quote = null;
      } else {
        current += character;
      }
    } else if (character === "'" || character === '"') {
      quote = character;
    } else if (character === ";") {
      if (current.trim()) parts.push(current);
      current = "";
    } else {
      current += character;
    }
  }

  if (quote) throw new Error("Invalid MySQL connection string: unterminated quoted value.");
  if (current.trim()) parts.push(current);
  return parts;
}

export function parseMySqlConnectionString(connectionString) {
  if (typeof connectionString !== "string" || !connectionString.trim()) {
    throw new Error("MySQL connection string is missing or empty.");
  }

  const parsed = {};
  for (const part of splitConnectionString(connectionString)) {
    const separator = part.indexOf("=");
    if (separator <= 0) throw new Error("Invalid MySQL connection-string segment.");
    const rawKey = part.slice(0, separator).trim();
    const value = part.slice(separator + 1).trim();
    const normalizedKey = rawKey.toLowerCase().replaceAll(" ", "");
    const key = keyAliases.get(normalizedKey);
    if (!key) continue;
    if (Object.hasOwn(parsed, key)) {
      throw new Error(`Duplicate MySQL connection-string setting: ${rawKey}.`);
    }
    parsed[key] = value;
  }

  const port = parsed.port === undefined ? 3306 : Number(parsed.port);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error("MySQL connection-string Port must be an integer from 1 to 65535.");
  }

  return {
    host: parsed.host?.trim().toLowerCase() ?? "",
    port,
    database: parsed.database?.trim() ?? "",
    user: parsed.user ?? "",
    password: parsed.password ?? "",
  };
}

export function assertSafeLocalDatabaseTarget(target) {
  if (!target || !["localhost", "127.0.0.1", "::1"].includes(target.host)) {
    throw new Error("Local database reset refused: host must be localhost, 127.0.0.1, or ::1.");
  }
  if (target.database !== "appetee") {
    throw new Error("Local database reset refused: database must be exactly appetee.");
  }

  return target;
}

export function parseAndAssertSafeLocalTarget(connectionString) {
  return assertSafeLocalDatabaseTarget(parseMySqlConnectionString(connectionString));
}

function connectionOptions(target, { includeDatabase, multipleStatements = false }) {
  return {
    host: target.host,
    port: target.port,
    database: includeDatabase ? target.database : undefined,
    user: target.user,
    password: target.password,
    multipleStatements,
  };
}

export async function requireGeneratedSqlFiles({
  sqlDir = path.join(backendRoot, "data", "generated", "sql"),
} = {}) {
  const files = [];
  for (const fileName of GENERATED_SQL_FILE_NAMES) {
    const filePath = path.join(sqlDir, fileName);
    let sql;
    try {
      sql = (await readFile(filePath, "utf8")).replace(/^\uFEFF/u, "");
    } catch (error) {
      throw new Error(`Required generated SQL file is unavailable: ${fileName}.`, { cause: error });
    }
    if (!sql.trim()) throw new Error(`Required generated SQL file is empty: ${fileName}.`);
    files.push({ fileName, filePath, sql });
  }
  return files;
}

export async function recreateLocalDatabase(target, { createConnection = mysql.createConnection } = {}) {
  assertSafeLocalDatabaseTarget(target);
  const connection = await createConnection(connectionOptions(target, { includeDatabase: false }));
  try {
    await connection.query("DROP DATABASE IF EXISTS `appetee`");
    await connection.query(
      "CREATE DATABASE `appetee` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci",
    );
  } finally {
    await connection.end();
  }
}

export async function executeSeedFiles(
  target,
  files,
  { createConnection = mysql.createConnection, onFileComplete = () => {} } = {},
) {
  assertSafeLocalDatabaseTarget(target);
  const connection = await createConnection(
    connectionOptions(target, { includeDatabase: true, multipleStatements: true }),
  );
  try {
    for (const file of files) {
      try {
        await connection.query(file.sql);
        onFileComplete(file.fileName);
      } catch (error) {
        throw new Error(`Seed execution failed for ${file.fileName}; correct the cause and rerun npm run db:reset:local.`, { cause: error });
      }
    }
  } finally {
    await connection.end();
  }
}

export async function createLocalDatabaseConnection(
  target,
  { createConnection = mysql.createConnection } = {},
) {
  assertSafeLocalDatabaseTarget(target);
  return createConnection(connectionOptions(target, { includeDatabase: true }));
}
