import { loadConfig } from "./config.js";
import { createPool } from "./db.js";
import { buildServer } from "./server.js";

async function main() {
  const config = loadConfig();
  const db = createPool(config);
  const server = await buildServer({ config, db });

  const close = async () => {
    try {
      await server.close();
      await db.end();
    } finally {
      process.exit(0);
    }
  };

  process.on("SIGINT", close);
  process.on("SIGTERM", close);

  await server.listen({
    host: config.host,
    port: config.port,
  });
}

main().catch((err) => {
  // eslint-disable-next-line no-console
  console.error(err);
  process.exit(1);
});