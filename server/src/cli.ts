import readline from "node:readline";
import { stdin as input, stdout as output } from "node:process";
import { buildApp } from "./app.js";
import { config } from "./config.js";

function printUsage(): void {
  console.log(`
NNotify server admin CLI

Usage:
  npm run admin -- migrate
  npm run admin -- user:list [status]
  npm run admin -- user:create <username> [password]
  npm run admin -- user:approve <username>
  npm run admin -- user:reject <username> [reason]
  npm run admin -- user:block <username>
  npm run admin -- telegram:set-webhook

status: pending | active | blocked | rejected

Tip:
  user:create without [password] will prompt securely (not visible in shell history).
`);
}

function ask(question: string): Promise<string> {
  return new Promise((resolve) => {
    const rl = readline.createInterface({ input, output });
    rl.question(question, (answer) => {
      rl.close();
      resolve(answer.trim());
    });
  });
}

function askHidden(question: string): Promise<string> {
  return new Promise((resolve) => {
    const rl = readline.createInterface({ input, output }) as readline.Interface & { stdoutMuted?: boolean };
    rl.stdoutMuted = true;
    const originalWrite = (rl as unknown as { _writeToOutput?: (text: string) => void })._writeToOutput;
    (rl as unknown as { _writeToOutput: (text: string) => void })._writeToOutput = function writeToOutput(text: string) {
      if (rl.stdoutMuted) {
        output.write("*");
        return;
      }

      if (originalWrite) {
        originalWrite.call(rl, text);
      } else {
        output.write(text);
      }
    };

    output.write(question);
    rl.question("", (answer) => {
      rl.close();
      output.write("\n");
      resolve(answer.trim());
    });
  });
}

async function resolveCreatePassword(argPassword?: string): Promise<string> {
  if (argPassword && argPassword.trim().length > 0) {
    return argPassword.trim();
  }

  const first = await askHidden("Enter password: ");
  const second = await askHidden("Repeat password: ");
  if (first !== second) {
    throw new Error("Passwords do not match.");
  }

  return first;
}

async function main(): Promise<void> {
  const [, , command, ...args] = process.argv;
  const { app, authService, telegram } = buildApp();

  try {
    if (!command) {
      printUsage();
      return;
    }

    switch (command) {
      case "migrate": {
        console.log(`OK: database is ready at ${config.databasePath}`);
        return;
      }

      case "user:list": {
        const status = args[0] as "pending" | "active" | "blocked" | "rejected" | undefined;
        const users = authService.listUsers(status);
        if (users.length === 0) {
          console.log("No users found.");
          return;
        }

        console.table(
          users.map((u) => ({
            id: u.id,
            username: u.username,
            status: u.status,
            failed_login_attempts: u.failed_login_attempts,
            locked_until_utc: u.locked_until_utc,
            created_at: u.created_at,
            approved_at: u.approved_at,
            rejected_at: u.rejected_at
          }))
        );
        return;
      }

      case "user:create": {
        const [username, rawPassword] = args;
        if (!username) {
          throw new Error("user:create requires <username> [password]");
        }

        const password = await resolveCreatePassword(rawPassword);
        const result = await authService.createActiveUserByAdmin(username, password, config.bootstrapAdminName);
        console.log(result.message);
        process.exitCode = result.ok ? 0 : 2;
        return;
      }

      case "user:approve": {
        const [username] = args;
        if (!username) {
          throw new Error("user:approve requires <username>");
        }

        const result = authService.approveByUsername(username, config.bootstrapAdminName);
        console.log(result.message);
        process.exitCode = result.ok ? 0 : 2;
        return;
      }

      case "user:reject": {
        const [username, ...rest] = args;
        if (!username) {
          throw new Error("user:reject requires <username> [reason]");
        }

        const reason = rest.length > 0 ? rest.join(" ") : undefined;
        const result = authService.rejectByUsername(username, config.bootstrapAdminName, reason);
        console.log(result.message);
        process.exitCode = result.ok ? 0 : 2;
        return;
      }

      case "user:block": {
        const [username] = args;
        if (!username) {
          throw new Error("user:block requires <username>");
        }

        const result = authService.blockByUsername(username, config.bootstrapAdminName);
        console.log(result.message);
        process.exitCode = result.ok ? 0 : 2;
        return;
      }

      case "telegram:set-webhook": {
        await telegram.setWebhook();
        console.log("Telegram webhook configured successfully.");
        return;
      }

      default: {
        throw new Error(`Unknown command: ${command}`);
      }
    }
  } finally {
    await app.close();
  }
}

main().catch((error) => {
  console.error(`ERROR: ${error instanceof Error ? error.message : String(error)}`);
  process.exit(1);
});
