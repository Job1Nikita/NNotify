import { request } from "undici";
import type { AppConfig } from "./config.js";

export async function notifyAdminAboutPendingRegistration(config: AppConfig, username: string): Promise<void> {
  if (!config.adminTelegramBotToken || !config.adminTelegramChatId) {
    return;
  }

  const url = `https://api.telegram.org/bot${config.adminTelegramBotToken}/sendMessage`;
  const body = {
    chat_id: config.adminTelegramChatId,
    text: `NNotify sync registration pending approval: ${username}`,
  };

  try {
    await request(url, {
      method: "POST",
      headers: {
        "content-type": "application/json",
      },
      body: JSON.stringify(body),
    });
  } catch {
    // Intentionally ignore: registration itself must not fail on Telegram issues.
  }
}