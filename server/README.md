# NNotify Server

NNotify Server provides account management, admin approval, reminder synchronization, and server-side Telegram escalation for NNotify desktop clients.

The server is optional. The Windows client can still work fully offline without it.

## Features

- Client registration with pending admin approval
- Login / refresh / logout API
- Multi-device reminder synchronization
- Initial merge of local client reminders
- Sync for active, missed, and historical reminders
- Duplicate-candidate marking for similar reminders
- Server-side Telegram reminder escalation
- Telegram admin panel for approving, blocking, deleting, and restoring users
- Admin CLI for user management
- SQLite storage with WAL mode

## API Surface

Public API behind nginx:

- `POST /v1/auth/register`
- `POST /v1/auth/login`
- `POST /v1/auth/refresh`
- `POST /v1/auth/logout`
- `GET /v1/sync/reminders`
- `POST /v1/sync/reminders/batch`
- `POST /v1/telegram/webhook/<TELEGRAM_WEBHOOK_SECRET>`

The app process itself should bind only to loopback:

```text
127.0.0.1:3100
```

## Security Baseline

- Run behind nginx: `443 -> 127.0.0.1:3100`
- Run as a dedicated Linux user, for example `nnotifysvc`
- Keep secrets outside the repo in `/etc/nnotify/nnotify-auth.env`
- Password hashing: Argon2id
- Access JWT with short TTL
- Opaque refresh token stored only as hash
- Refresh token rotation and reuse detection
- Account lockout after repeated failed login attempts
- Rate limits on auth routes
- Telegram webhook path secret
- Optional Telegram webhook header secret
- Request logs are quiet by default: successful polling is not logged, errors are logged

## Recommended Production Layout

```text
/opt/nnotify/server                 # app code
/opt/nnotify/server/data            # SQLite DB
/etc/nnotify/nnotify-auth.env       # protected runtime config
/etc/systemd/system/nnotify-auth.service
```

## 1. Create Service User And Directories

```bash
sudo useradd --system --create-home --home-dir /opt/nnotify --shell /usr/sbin/nologin nnotifysvc
sudo mkdir -p /opt/nnotify/server /etc/nnotify
sudo chown -R nnotifysvc:nnotifysvc /opt/nnotify
sudo chmod 750 /opt/nnotify/server
```

## 2. Copy Files

Copy the repository `server/` directory to:

```text
/opt/nnotify/server
```

Do not copy local `.env`, `data/`, `dist/`, or `node_modules/` from a development machine.

## 3. Create Protected Env File

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

## 4. Install Dependencies

Use the lockfile for reproducible installs:

```bash
cd /opt/nnotify/server
npm ci
```

Do not run global npm updates on a shared host. Keep npm commands inside `/opt/nnotify/server`.

## 5. Build And Initialize Database

```bash
cd /opt/nnotify/server
npm run build
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run migrate'
```

## 6. Install systemd Service

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

## 7. Configure nginx

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

## 8. Telegram Admin Bot

If Telegram moderation is enabled, set webhook:

```bash
cd /opt/nnotify/server
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- telegram:set-webhook'
```

Then open the admin bot in Telegram and press `Start`.

The admin panel supports:

- registered users
- blocked users
- deleted users
- pending registrations
- approve / reject
- block / unblock
- delete / restore

## 9. Admin CLI

```bash
cd /opt/nnotify/server
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:list'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:list pending'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:create mylogin'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:approve mylogin'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:reject mylogin "manual reject"'
sudo -u nnotifysvc -H bash -lc 'set -a; source /etc/nnotify/nnotify-auth.env; set +a; npm run admin -- user:block mylogin'
```

## Telegram Reminder Escalation

For server-side reminder escalation:

1. Set `REMINDER_TELEGRAM_BOT_TOKEN`.
2. In the desktop client, fill `User ID for alerts` in the sync account section.
3. The user must open the reminder bot and press `Start`.

If Telegram returns `Bad Request: chat not found`, the user has not started that bot or the user ID is incorrect.

## SQLite Note

SQLite is acceptable for small private deployments, for example around 20 users and a few devices per user.

If the system grows into heavy concurrent write traffic, PostgreSQL would be the next reasonable step.
