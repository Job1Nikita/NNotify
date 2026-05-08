# NNotify Server (Auth Phase)

This module provides secure server-side auth for NNotify clients:
- `POST /v1/auth/register` (pending approval)
- `POST /v1/auth/login`
- `POST /v1/auth/refresh`
- `POST /v1/auth/logout`

Current phase: auth + registration approval. Reminder sync API is not included yet.

## Security baseline

- Local bind only: `127.0.0.1:3100`
- External access only via `nginx:443 -> 127.0.0.1:3100`
- Password hashing: Argon2id
- Access JWT (short TTL) + opaque refresh token (DB stores only hash)
- Refresh token rotation + reuse detection
- Account lockout after repeated failed login attempts
- Automatic cleanup for expired/old auth data
- Route rate-limits + audit log
- Optional Telegram moderation for approvals

## Production layout (recommended)

- App code: `/opt/nnotify/server`
- Runtime env file: `/etc/nnotify/nnotify-auth.env`
- Service user: `nnotifysvc`

## 1) Create service user and directories

```bash
sudo useradd --system --create-home --home-dir /opt/nnotify --shell /usr/sbin/nologin nnotifysvc
sudo mkdir -p /opt/nnotify/server /etc/nnotify
sudo chown -R nnotifysvc:nnotifysvc /opt/nnotify
sudo chmod 750 /opt/nnotify/server
```

## 2) Copy project files

Copy the `server/` folder from repo to:
- `/opt/nnotify/server`

## 3) Create protected env file

Template file in repo:
- `server/deploy/systemd/nnotify-auth.env.example`

On server:

```bash
sudo cp /opt/nnotify/server/deploy/systemd/nnotify-auth.env.example /etc/nnotify/nnotify-auth.env
sudo chown root:nnotifysvc /etc/nnotify/nnotify-auth.env
sudo chmod 640 /etc/nnotify/nnotify-auth.env
sudo nano /etc/nnotify/nnotify-auth.env
```

Replace all `CHANGE_ME` values.

## 4) Lockfile and dependencies

For reproducible installs, `package-lock.json` must exist.

If lockfile is missing:

```bash
cd /opt/nnotify/server
npm install --package-lock-only
```

Then install strictly from lockfile:

```bash
npm ci
```

Important:
- do not run global updates like `npm install -g ...` on shared host;
- run all npm commands only inside `/opt/nnotify/server`.

## 5) Build and init DB

```bash
cd /opt/nnotify/server
npm run build
npm run migrate
```

## 6) Install hardened systemd unit

Unit file in repo:
- `server/deploy/systemd/nnotify-auth.service`

Install:

```bash
sudo cp /opt/nnotify/server/deploy/systemd/nnotify-auth.service /etc/systemd/system/nnotify-auth.service
sudo systemctl daemon-reload
sudo systemctl enable --now nnotify-auth
sudo systemctl status nnotify-auth
journalctl -u nnotify-auth -f
```

## 7) Nginx reverse proxy

Example config in repo:
- `server/deploy/nginx/nnotify-auth.conf.example`

Use it as:
- `/etc/nginx/sites-available/nnotify-auth.conf`

Important:
- replace `SECRET_IN_PATH` with `TELEGRAM_WEBHOOK_SECRET`
- for extra webhook validation also check header `X-Telegram-Bot-Api-Secret-Token` against `TELEGRAM_WEBHOOK_HEADER_SECRET`
- keep backend on loopback (`127.0.0.1:3100`)

Enable and reload:

```bash
sudo ln -s /etc/nginx/sites-available/nnotify-auth.conf /etc/nginx/sites-enabled/nnotify-auth.conf
sudo nginx -t
sudo systemctl reload nginx
```

## 8) Admin CLI

```bash
cd /opt/nnotify/server
npm run admin -- user:list
npm run admin -- user:list pending
npm run admin -- user:create mylogin
npm run admin -- user:approve mylogin
npm run admin -- user:reject mylogin "manual reject"
npm run admin -- user:block mylogin
```

If Telegram moderation is enabled:

```bash
npm run admin -- telegram:set-webhook
```

Webhook route:
- `/v1/telegram/webhook/<TELEGRAM_WEBHOOK_SECRET>`

## SQLite note

For ~20 users and auth-only workload, SQLite is acceptable with local disk + WAL mode.
If later sync traffic becomes write-heavy across many devices, migrate to PostgreSQL.
