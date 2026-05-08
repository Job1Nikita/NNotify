# NNotify Server

[Русская версия](#ru) | [English Version](#en)

---

<a id="ru"></a>
## Русская версия

**Быстрые ссылки:**
[Описание](#ru-overview) | [Возможности](#ru-features) | [API](#ru-api) | [Безопасность](#ru-security) | [Развертывание](#ru-deploy) | [Telegram](#ru-telegram) | [CLI](#ru-cli) | [SQLite](#ru-sqlite)

<a id="ru-overview"></a>
### Описание

**NNotify Server** — серверная часть для NNotify Desktop.

Сервер отвечает за:

- учетные записи пользователей;
- подтверждение регистраций администратором;
- синхронизацию напоминаний между устройствами;
- серверную Telegram-эскалацию напоминаний;
- Telegram-админку и CLI-управление пользователями.

Сервер опционален. Клиент NNotify может работать полностью локально без него.

<a id="ru-features"></a>
### Возможности

- Регистрация клиента со статусом `pending approval`.
- Вход / обновление сессии / выход.
- Синхронизация напоминаний между несколькими устройствами.
- Первичное объединение локальных напоминаний при подключении sync-аккаунта.
- Синхронизация активных, пропущенных и исторических напоминаний.
- Отметка похожих напоминаний как возможных дублей.
- Серверная Telegram-эскалация, даже если клиент выключен.
- Telegram-админка для подтверждения, блокировки, удаления и восстановления пользователей.
- Admin CLI для управления пользователями.
- SQLite с WAL mode.

<a id="ru-api"></a>
### API

Публичные маршруты за nginx:

- `POST /v1/auth/register`
- `POST /v1/auth/login`
- `POST /v1/auth/refresh`
- `POST /v1/auth/logout`
- `GET /v1/sync/reminders`
- `POST /v1/sync/reminders/batch`
- `POST /v1/telegram/webhook/<TELEGRAM_WEBHOOK_SECRET>`

Сам Node.js-процесс должен слушать только loopback:

```text
127.0.0.1:3100
```

<a id="ru-security"></a>
### Базовая безопасность

- Сервер работает за nginx: `443 -> 127.0.0.1:3100`.
- Процесс запускается от отдельного Linux-пользователя, например `nnotifysvc`.
- Секреты хранятся вне репозитория: `/etc/nnotify/nnotify-auth.env`.
- Пароли хранятся как Argon2id-хэши.
- Access JWT короткоживущий.
- Refresh token хранится только как хэш.
- Refresh token rotation и защита от повторного использования.
- Блокировка входа после серии неудачных попыток.
- Rate-limit на auth-маршрутах.
- Секретный путь Telegram webhook.
- Опциональная проверка Telegram webhook header secret.
- Успешный polling не логируется, ошибки логируются.

<a id="ru-deploy"></a>
### Рекомендуемая структура на сервере

```text
/opt/nnotify/server                 # код приложения
/opt/nnotify/server/data            # SQLite DB
/etc/nnotify/nnotify-auth.env       # защищенный runtime config
/etc/systemd/system/nnotify-auth.service
```

### 1. Создать пользователя и директории

```bash
sudo useradd --system --create-home --home-dir /opt/nnotify --shell /usr/sbin/nologin nnotifysvc
sudo mkdir -p /opt/nnotify/server /etc/nnotify
sudo chown -R nnotifysvc:nnotifysvc /opt/nnotify
sudo chmod 750 /opt/nnotify/server
```

### 2. Скопировать файлы

Скопируйте каталог `server/` из репозитория в:

```text
/opt/nnotify/server
```

Не копируйте локальные `.env`, `data/`, `dist/`, `node_modules/` с dev-машины.

### 3. Создать защищенный env-файл

Шаблон в репозитории:

```text
server/deploy/systemd/nnotify-auth.env.example
```

На сервере:

```bash
sudo cp /opt/nnotify/server/deploy/systemd/nnotify-auth.env.example /etc/nnotify/nnotify-auth.env
sudo chown root:nnotifysvc /etc/nnotify/nnotify-auth.env
sudo chmod 640 /etc/nnotify/nnotify-auth.env
sudo nano /etc/nnotify/nnotify-auth.env
```

Замените все значения `CHANGE_ME`.

Важные параметры:

- `PUBLIC_BASE_URL=https://your-domain.example`
- `JWT_ACCESS_SECRET` — длинный случайный секрет, 64+ символа
- `REFRESH_TOKEN_PEPPER` — другой длинный случайный секрет, 64+ символа
- `TELEGRAM_BOT_TOKEN` — токен админского бота
- `REMINDER_TELEGRAM_BOT_TOKEN` — токен бота для эскалации напоминаний
- `TELEGRAM_ADMIN_CHAT_ID` — chat target для админского бота
- `TELEGRAM_ADMIN_USER_IDS` — Telegram user IDs, которым разрешена админка
- `TELEGRAM_WEBHOOK_SECRET` — секретный сегмент пути webhook
- `TELEGRAM_WEBHOOK_HEADER_SECRET` — опциональный header secret для Telegram webhook
- `REMINDER_TIME_ZONE` — таймзона для сообщений, например `Europe/Moscow`

### 4. Установить зависимости

Используйте lockfile:

```bash
cd /opt/nnotify/server
npm ci
```

Не обновляйте глобальный npm на shared-хосте. Все npm-команды выполняйте только внутри `/opt/nnotify/server`.

### 5. Собрать и инициализировать БД

```bash
cd /opt/nnotify/server
npm run build
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run migrate'
```

### 6. Установить systemd-службу

Unit-файл в репозитории:

```text
server/deploy/systemd/nnotify-auth.service
```

Установка:

```bash
sudo cp /opt/nnotify/server/deploy/systemd/nnotify-auth.service /etc/systemd/system/nnotify-auth.service
sudo systemctl daemon-reload
sudo systemctl enable --now nnotify-auth
sudo systemctl status nnotify-auth --no-pager
```

Логи:

```bash
journalctl -u nnotify-auth -f
```

### 7. Настроить nginx

Пример конфига:

```text
server/deploy/nginx/nnotify-auth.conf.example
```

Используйте его как:

```text
/etc/nginx/sites-available/nnotify-auth.conf
```

Что нужно заменить:

- `nnotify.example.com` на ваш домен;
- `SECRET_IN_PATH` на `TELEGRAM_WEBHOOK_SECRET`;
- при необходимости включить проверку `X-Telegram-Bot-Api-Secret-Token` через `TELEGRAM_WEBHOOK_HEADER_SECRET`;
- backend оставить на `127.0.0.1:3100`;
- оставить `client_max_body_size 256k` или больше для sync batch-запросов.

Включить конфиг:

```bash
sudo ln -s /etc/nginx/sites-available/nnotify-auth.conf /etc/nginx/sites-enabled/nnotify-auth.conf
sudo nginx -t
sudo systemctl reload nginx
```

<a id="ru-telegram"></a>
### Telegram Bot Setup

Для серверной части обычно используются два Telegram-бота:

- `TELEGRAM_BOT_TOKEN` — админский бот для подтверждения регистраций и управления пользователями.
- `REMINDER_TELEGRAM_BOT_TOKEN` — бот, который отправляет пользователям эскалации напоминаний.

Можно использовать одного и того же бота для обеих ролей, но отдельные боты удобнее и безопаснее: админские действия и пользовательские уведомления не смешиваются.

Как создать бота:

1. Откройте `@BotFather` в Telegram.
2. Выполните `/newbot`.
3. Задайте имя и username бота.
4. Скопируйте Bot Token в нужную env-переменную.
5. Пользователь, которому бот должен писать в личку, должен открыть этого бота и нажать `Start`.

Для Telegram-админки администратор должен нажать `Start` у админского бота.

Для серверной эскалации каждый пользователь должен нажать `Start` у бота напоминаний. Иначе Telegram вернет `Bad Request: chat not found`.

### Telegram Admin Bot

Если Telegram moderation включен, установите webhook:

```bash
cd /opt/nnotify/server
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- telegram:set-webhook'
```

Затем откройте админского бота в Telegram и нажмите `Start`.

Админка поддерживает:

- зарегистрированных пользователей;
- заблокированных пользователей;
- удаленных пользователей;
- ожидающие регистрации;
- подтвердить / отклонить;
- заблокировать / разблокировать;
- удалить / восстановить.

### Telegram-эскалация напоминаний

Для серверной эскалации:

1. Заполните `REMINDER_TELEGRAM_BOT_TOKEN`.
2. В клиенте заполните `ID пользователя для оповещений` в блоке sync-аккаунта.
3. Пользователь должен открыть бота напоминаний и нажать `Start`.

Если Telegram возвращает `Bad Request: chat not found`, пользователь не нажал `Start` у этого бота или указан неправильный user ID.

<a id="ru-cli"></a>
### Admin CLI

```bash
cd /opt/nnotify/server
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:list'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:list pending'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:create mylogin'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:approve mylogin'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:reject mylogin "manual reject"'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:block mylogin'
```

<a id="ru-sqlite"></a>
### SQLite

SQLite подходит для небольших приватных инсталляций, например около 20 пользователей и нескольких устройств на пользователя.

Если проект вырастет в тяжелую конкурентную запись, следующим логичным шагом будет PostgreSQL.

---

<a id="en"></a>
## English Version

**Quick Links:**
[Overview](#en-overview) | [Features](#en-features) | [API](#en-api) | [Security](#en-security) | [Deployment](#en-deploy) | [Telegram](#en-telegram) | [CLI](#en-cli) | [SQLite](#en-sqlite)

<a id="en-overview"></a>
### Overview

**NNotify Server** is the server component for NNotify Desktop.

It provides:

- user accounts;
- admin approval for registrations;
- reminder synchronization between devices;
- server-side Telegram reminder escalation;
- Telegram admin panel and CLI user management.

The server is optional. The NNotify client can still work fully offline without it.

<a id="en-features"></a>
### Features

- Client registration with `pending approval` status.
- Login / refresh / logout API.
- Multi-device reminder synchronization.
- Initial merge of local reminders when a sync account is connected.
- Sync for active, missed, and historical reminders.
- Duplicate-candidate marking for similar reminders.
- Server-side Telegram escalation even when the client is offline.
- Telegram admin panel for approving, blocking, deleting, and restoring users.
- Admin CLI for user management.
- SQLite storage with WAL mode.

<a id="en-api"></a>
### API

Public routes behind nginx:

- `POST /v1/auth/register`
- `POST /v1/auth/login`
- `POST /v1/auth/refresh`
- `POST /v1/auth/logout`
- `GET /v1/sync/reminders`
- `POST /v1/sync/reminders/batch`
- `POST /v1/telegram/webhook/<TELEGRAM_WEBHOOK_SECRET>`

The Node.js process itself should bind only to loopback:

```text
127.0.0.1:3100
```

<a id="en-security"></a>
### Security Baseline

- Run behind nginx: `443 -> 127.0.0.1:3100`.
- Run as a dedicated Linux user, for example `nnotifysvc`.
- Keep secrets outside the repository in `/etc/nnotify/nnotify-auth.env`.
- Password hashing: Argon2id.
- Short-lived access JWT.
- Opaque refresh token stored only as hash.
- Refresh token rotation and reuse detection.
- Account lockout after repeated failed login attempts.
- Rate limits on auth routes.
- Telegram webhook path secret.
- Optional Telegram webhook header secret.
- Successful polling is not logged; errors are logged.

<a id="en-deploy"></a>
### Recommended Production Layout

```text
/opt/nnotify/server                 # app code
/opt/nnotify/server/data            # SQLite DB
/etc/nnotify/nnotify-auth.env       # protected runtime config
/etc/systemd/system/nnotify-auth.service
```

### 1. Create Service User And Directories

```bash
sudo useradd --system --create-home --home-dir /opt/nnotify --shell /usr/sbin/nologin nnotifysvc
sudo mkdir -p /opt/nnotify/server /etc/nnotify
sudo chown -R nnotifysvc:nnotifysvc /opt/nnotify
sudo chmod 750 /opt/nnotify/server
```

### 2. Copy Files

Copy the repository `server/` directory to:

```text
/opt/nnotify/server
```

Do not copy local `.env`, `data/`, `dist/`, or `node_modules/` from a development machine.

### 3. Create Protected Env File

Template in repo:

```text
server/deploy/systemd/nnotify-auth.env.example
```

On the server:

```bash
sudo cp /opt/nnotify/server/deploy/systemd/nnotify-auth.env.example /etc/nnotify/nnotify-auth.env
sudo chown root:nnotifysvc /etc/nnotify/nnotify-auth.env
sudo chmod 640 /etc/nnotify/nnotify-auth.env
sudo nano /etc/nnotify/nnotify-auth.env
```

Replace every `CHANGE_ME` value.

Important values:

- `PUBLIC_BASE_URL=https://your-domain.example`
- `JWT_ACCESS_SECRET` - long random secret, 64+ chars
- `REFRESH_TOKEN_PEPPER` - different long random secret, 64+ chars
- `TELEGRAM_BOT_TOKEN` - admin bot token
- `REMINDER_TELEGRAM_BOT_TOKEN` - reminder escalation bot token
- `TELEGRAM_ADMIN_CHAT_ID` - admin bot chat target
- `TELEGRAM_ADMIN_USER_IDS` - comma-separated Telegram user IDs allowed to use admin panel
- `TELEGRAM_WEBHOOK_SECRET` - secret path segment
- `TELEGRAM_WEBHOOK_HEADER_SECRET` - optional Telegram webhook header secret
- `REMINDER_TIME_ZONE` - timezone for reminder messages, for example `Europe/Moscow`

### 4. Install Dependencies

Use the lockfile for reproducible installs:

```bash
cd /opt/nnotify/server
npm ci
```

Do not run global npm updates on a shared host. Keep npm commands inside `/opt/nnotify/server`.

### 5. Build And Initialize Database

```bash
cd /opt/nnotify/server
npm run build
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run migrate'
```

### 6. Install systemd Service

Unit file in repo:

```text
server/deploy/systemd/nnotify-auth.service
```

Install:

```bash
sudo cp /opt/nnotify/server/deploy/systemd/nnotify-auth.service /etc/systemd/system/nnotify-auth.service
sudo systemctl daemon-reload
sudo systemctl enable --now nnotify-auth
sudo systemctl status nnotify-auth --no-pager
```

Logs:

```bash
journalctl -u nnotify-auth -f
```

### 7. Configure nginx

Example config in repo:

```text
server/deploy/nginx/nnotify-auth.conf.example
```

Use it as:

```text
/etc/nginx/sites-available/nnotify-auth.conf
```

Required changes:

- replace `nnotify.example.com` with your domain;
- replace `SECRET_IN_PATH` with `TELEGRAM_WEBHOOK_SECRET`;
- optionally enforce `X-Telegram-Bot-Api-Secret-Token` with `TELEGRAM_WEBHOOK_HEADER_SECRET`;
- keep backend on `127.0.0.1:3100`;
- keep `client_max_body_size 256k` or larger for sync batches.

Enable and reload:

```bash
sudo ln -s /etc/nginx/sites-available/nnotify-auth.conf /etc/nginx/sites-enabled/nnotify-auth.conf
sudo nginx -t
sudo systemctl reload nginx
```

<a id="en-telegram"></a>
### Telegram Bot Setup

The server usually uses two Telegram bots:

- `TELEGRAM_BOT_TOKEN` — admin bot for registration approval and user management.
- `REMINDER_TELEGRAM_BOT_TOKEN` — reminder bot for user escalation messages.

You can use the same bot for both roles, but separate bots are cleaner and safer: admin actions and user alerts do not mix.

How to create a bot:

1. Open `@BotFather` in Telegram.
2. Run `/newbot`.
3. Set the bot name and username.
4. Copy the Bot Token into the corresponding env variable.
5. The user who should receive direct messages must open this bot and press `Start`.

For the Telegram admin panel, the administrator must press `Start` in the admin bot.

For server-side reminder escalation, each user must press `Start` in the reminder bot. Otherwise Telegram returns `Bad Request: chat not found`.

### Telegram Admin Bot

If Telegram moderation is enabled, set webhook:

```bash
cd /opt/nnotify/server
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- telegram:set-webhook'
```

Then open the admin bot in Telegram and press `Start`.

The admin panel supports:

- registered users;
- blocked users;
- deleted users;
- pending registrations;
- approve / reject;
- block / unblock;
- delete / restore.

### Telegram Reminder Escalation

For server-side reminder escalation:

1. Set `REMINDER_TELEGRAM_BOT_TOKEN`.
2. In the desktop client, fill `User ID for alerts` in the sync account section.
3. The user must open the reminder bot and press `Start`.

If Telegram returns `Bad Request: chat not found`, the user has not started that bot or the user ID is incorrect.

<a id="en-cli"></a>
### Admin CLI

```bash
cd /opt/nnotify/server
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:list'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:list pending'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:create mylogin'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:approve mylogin'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:reject mylogin "manual reject"'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:block mylogin'
```

<a id="en-sqlite"></a>
### SQLite

SQLite is acceptable for small private deployments, for example around 20 users and a few devices per user.

If the system grows into heavy concurrent write traffic, PostgreSQL would be the next reasonable step.

