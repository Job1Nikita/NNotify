import { loadConfig } from "../src/config.js";
import { createPool } from "../src/db.js";

async function main() {
  const config = loadConfig();
  const db = createPool(config);

  try {
    const result = await db.query<{
      username: string;
      created_at_utc: Date;
    }>(
      `SELECT username, created_at_utc
       FROM users
       WHERE status = 'pending'
       ORDER BY created_at_utc ASC`,
    );

    if (!result.rowCount) {
      // eslint-disable-next-line no-console
      console.log("No pending registrations.");
      return;
    }

    // eslint-disable-next-line no-console
    console.table(
      result.rows.map((row) => ({
        username: row.username,
        createdAtUtc: row.created_at_utc.toISOString(),
      })),
    );
  } finally {
    await db.end();
  }
}

main().catch((err) => {
  // eslint-disable-next-line no-console
  console.error(err);
  process.exit(1);
});