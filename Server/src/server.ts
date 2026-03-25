import fs from "node:fs";
import Fastify from "fastify";
import helmet from "@fastify/helmet";
import cors from "@fastify/cors";
import rateLimit from "@fastify/rate-limit";
import type { AppConfig } from "./config.js";
import type { Pool } from "pg";
import { registerAuthRoutes } from "./auth.js";
import { registerSyncRoutes, SyncEventHub } from "./sync.js";

interface BuildServerDeps {
  config: AppConfig;
  db: Pool;
}

export async function buildServer(deps: BuildServerDeps) {
  const { config, db } = deps;

  const server = Fastify({
    logger: true,
    ...(config.allowHttp
      ? {}
      : {
          https: {
            cert: fs.readFileSync(config.tlsCertPath),
            key: fs.readFileSync(config.tlsKeyPath),
          },
        }),
  });

  await server.register(helmet, {
    contentSecurityPolicy: false,
  });

  await server.register(cors, {
    origin: config.allowedOrigin || false,
    credentials: true,
  });

  await server.register(rateLimit, {
    max: 200,
    timeWindow: "1 minute",
  });

  const syncHub = new SyncEventHub();

  server.get("/health", async () => ({ ok: true, nowUtc: new Date().toISOString() }));

  await registerAuthRoutes(server, { config, db });
  await registerSyncRoutes(server, { config, db, events: syncHub });

  return server;
}