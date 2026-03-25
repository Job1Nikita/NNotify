import { loadConfig } from "../src/config.js";
import { createPool } from "../src/db.js";

async function main() {
  const username = process.argv[2]?.trim();
  const rejectedBy = process.argv[3]?.trim() || "cli-admin";

  if (!username) {
    // eslint-disable-next-line no-console
    console.error("Usage: npm run admin:reject -- <username> [rejectedBy]");
    process.exit(1);
  }

  const config = loadConfig();
  const db = createPool(config);

  try {
    const result = await db.query(
      `UPDATE users
       SET status = 'rejected',
           approved_at_utc = NOW(),
           approved_by = $2
       WHERE username = $1
         AND status = 'pending'`,
      [username, rejectedBy],
    );

    if (!result.rowCount) {
      // eslint-disable-next-line no-console
      console.log(`No pending user found for username '${username}'.`);
      return;
    }

    // eslint-disable-next-line no-console
    console.log(`User '${username}' rejected by '${rejectedBy}'.`);
  } finally {
    await db.end();
  }
}

main().catch((err) => {
  // eslint-disable-next-line no-console
  console.error(err);
  process.exit(1);
});