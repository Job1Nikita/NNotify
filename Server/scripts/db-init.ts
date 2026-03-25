import fs from "node:fs/promises";
import { loadConfig } from "../src/config.js";
import { createPool } from "../src/db.js";

async function main() {
  const config = loadConfig();
  const db = createPool(config);

  try {
    const sqlUrl = new URL("../sql/001_init.sql", import.meta.url);
    const sql = await fs.readFile(sqlUrl, "utf8");
    await db.query(sql);
    // eslint-disable-next-line no-console
    console.log("Database initialized successfully.");
  } finally {
    await db.end();
  }
}

main().catch((err) => {
  // eslint-disable-next-line no-console
  console.error(err);
  process.exit(1);
});