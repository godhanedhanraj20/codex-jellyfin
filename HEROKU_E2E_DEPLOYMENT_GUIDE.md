# Jellyfin End-to-End Heroku Deployment Guide (Container Stack)

This document provides a complete deployment flow for running Jellyfin on Heroku using the container stack.

> **Important runtime model:** Heroku dynos are stateless. Local filesystem data is ephemeral and can be lost on restart/redeploy.

---

## 1) Prerequisites

- Heroku account and Heroku CLI installed.
- Docker installed locally.
- Access to this repository.
- A Heroku app name (example: `my-jellyfin-app`).

Optional but recommended:
- A dedicated Heroku Postgres plan for `DATABASE_URL`.

---

## 2) Architecture assumptions for this repo

This repo includes Heroku-focused deployment assets/config:

- `Dockerfile` (multi-stage build + non-root runtime)
- `Procfile`
- `app.json`
- Runtime config handling for:
  - `PORT`
  - `DATABASE_URL`
  - `JELLYFIN_DATA_DIR`
  - `JELLYFIN_CONFIG_DIR`
  - `JELLYFIN_CACHE_DIR`
  - `JELLYFIN_LOG_DIR`
  - `TRANSCODING_TEMP_PATH`
  - `FFMPEG_PATH` (optional)
  - `JELLYFIN_DISABLE_OPTIONAL_BACKGROUND_SERVICES` (optional)

---

## 3) Create Heroku app and required services

### 3.1 Create app on container stack

```bash
heroku create my-jellyfin-app --stack container
```

### 3.2 (Recommended) Attach PostgreSQL

```bash
heroku addons:create heroku-postgresql:essential-0 -a my-jellyfin-app
```

This injects `DATABASE_URL` automatically.

### 3.3 Set required config vars

```bash
heroku config:set \
  HEROKU=true \
  JELLYFIN_DISABLE_OPTIONAL_BACKGROUND_SERVICES=true \
  JELLYFIN_DATA_DIR=/app/data \
  JELLYFIN_CONFIG_DIR=/app/config \
  JELLYFIN_CACHE_DIR=/app/cache \
  JELLYFIN_LOG_DIR=/app/log \
  TRANSCODING_TEMP_PATH=/tmp/jellyfin-transcodes \
  -a my-jellyfin-app
```

Optional ffmpeg override:

```bash
heroku config:set FFMPEG_PATH=/usr/bin/ffmpeg -a my-jellyfin-app
```

---

## 4) Build and deploy container

From repository root:

```bash
heroku container:login
heroku container:push web -a my-jellyfin-app
heroku container:release web -a my-jellyfin-app
```

---

## 5) Post-deploy verification

### 5.1 Confirm dyno is running

```bash
heroku ps -a my-jellyfin-app
```

### 5.2 Stream logs and check startup

```bash
heroku logs --tail -a my-jellyfin-app
```

Look for indicators that:
- Kestrel bound successfully.
- Database provider initialized.
- Startup migrations completed.

### 5.3 Open app

```bash
heroku open -a my-jellyfin-app
```

Or browse directly:

```text
https://my-jellyfin-app.herokuapp.com
```

---

## 6) Operational checklist (production)

- **Database persistence:** use Heroku Postgres (or other managed external DB).
- **Media persistence:** do not rely on local dyno filesystem for long-term storage.
- **Dyno sizing:** start with at least a Standard dyno for better memory headroom.
- **Background services:** keep `JELLYFIN_DISABLE_OPTIONAL_BACKGROUND_SERVICES=true` on small dynos.
- **Logs:** rely on Heroku log drains/add-ons for retention.

---

## 7) Update/redeploy workflow

After code changes:

```bash
git push
heroku container:push web -a my-jellyfin-app
heroku container:release web -a my-jellyfin-app
```

Validate release:

```bash
heroku releases -a my-jellyfin-app
heroku logs --tail -a my-jellyfin-app
```

---

## 8) Rollback procedure

If latest release fails:

```bash
heroku releases -a my-jellyfin-app
heroku rollback vNN -a my-jellyfin-app
```

Then monitor:

```bash
heroku logs --tail -a my-jellyfin-app
```

---

## 9) Troubleshooting

## Issue: App boots but crashes immediately

- Check logs for missing/invalid env values:

```bash
heroku logs --tail -a my-jellyfin-app
heroku config -a my-jellyfin-app
```

- Ensure `DATABASE_URL` is present when Postgres mode is expected.

## Issue: Connection issues with database

- Verify add-on:

```bash
heroku addons -a my-jellyfin-app
```

- Validate URL visibility:

```bash
heroku config:get DATABASE_URL -a my-jellyfin-app
```

## Issue: Slow startup or memory pressure

- Keep optional services disabled:

```bash
heroku config:set JELLYFIN_DISABLE_OPTIONAL_BACKGROUND_SERVICES=true -a my-jellyfin-app
```

- Consider larger dyno plan.

## Issue: Lost local files after restart

- This is expected on Heroku. Move persistent state/media off dyno filesystem.

---

## 10) Security and maintenance notes

- Do not commit secrets into repo or Docker image.
- Use `heroku config:set` for credentials.
- Keep base images and dependencies updated.
- Validate behavior in staging app before production release.

---

## 11) Quick command summary

```bash
# one-time setup
heroku create my-jellyfin-app --stack container
heroku addons:create heroku-postgresql:essential-0 -a my-jellyfin-app
heroku config:set HEROKU=true JELLYFIN_DISABLE_OPTIONAL_BACKGROUND_SERVICES=true -a my-jellyfin-app

# deploy
heroku container:login
heroku container:push web -a my-jellyfin-app
heroku container:release web -a my-jellyfin-app

# observe
heroku ps -a my-jellyfin-app
heroku logs --tail -a my-jellyfin-app
heroku open -a my-jellyfin-app
```
