# NNotify Sync Server

Server module for account auth and reminder synchronization.

## Stack
- Node.js + TypeScript
- Fastify
- PostgreSQL
- PM2

## Security defaults
- HTTPS required by default (`ALLOW_HTTP=0`)
- Access token (JWT) + rotating refresh token
- Refresh tokens stored as SHA-256 hash + server pepper
- Password hashing: Argon2id
- New users are `pending` until approved

## Endpoints
- `POST /v1/auth/register`
- `POST /v1/auth/login`
- `POST /v1/auth/refresh`
- `POST /v1/auth/logout`
- `GET /v1/auth/me`
- `GET /v1/sync/changes`
- `POST /v1/sync/upsert`
- `GET /v1/sync/events` (SSE)

## Local setup (on Ubuntu 24.04)
1. Install Node.js 20+ and PostgreSQL.
2. Copy `.env.example` to `.env` and fill secrets.
3. Install dependencies:
   - `npm ci`
4. Initialize DB schema:
   - `npm run db:init`
5. Build and run:
   - `npm run build`
   - `npm run start`

## PM2
- Update `cwd` in `ecosystem.config.cjs`.
- Start:
  - `pm2 start ecosystem.config.cjs`
- Save startup config:
  - `pm2 save`
  - `pm2 startup`

## Admin approval (CLI)
- Pending users:
  - `npm run admin:pending`
- Approve:
  - `npm run admin:approve -- <username> [approvedBy]`
- Reject:
  - `npm run admin:reject -- <username> [rejectedBy]`

## Notes
- Client should use `https://<host>:5334`.
- Telegram admin notifications are optional (set `ADMIN_TELEGRAM_*` in `.env`).
- This module does not include a web admin panel by design.