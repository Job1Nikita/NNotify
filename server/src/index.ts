import { buildApp } from "./app.js";
import { config } from "./config.js";

const { app } = buildApp();

const shutdown = async (signal: string) => {
  app.log.info({ signal }, "Shutting down");
  try {
    await app.close();
  } finally {
    process.exit(0);
  }
};

process.on("SIGINT", () => {
  void shutdown("SIGINT");
});

process.on("SIGTERM", () => {
  void shutdown("SIGTERM");
});

app
  .listen({ host: config.host, port: config.port })
  .then(() => {
    app.log.info({ host: config.host, port: config.port }, "NNotify auth server started");
  })
  .catch((error) => {
    app.log.error(error, "Server startup failed");
    process.exit(1);
  });
