# OneNest AI — Deployment Guide

How to deploy OneNest AI to Render (free tier) using Supabase as the database and file storage.

Total cost: **$0** (Render free + Supabase free tier).

---

## What gets deployed where

```
Browser
  └── Render Static Site   → Angular frontend (built with ng build, served from CDN)
        └── calls API at →
              Render Web Service (Docker) → .NET 10 backend
                ├── Supabase PostgreSQL   → all user data + embedding vectors
                ├── Supabase Storage      → uploaded documents and health reports
                ├── Local ONNX model      → baked into Docker image, zero API cost
                └── Google Gemini API     → AI chat and RAG generation
```

**Important things to know before you start:**
- Render's free tier has an **ephemeral filesystem** — files saved to the container disk disappear on restart. That's why files go to Supabase Storage, not the local disk.
- Free services **spin down after 15 minutes of inactivity**. The first request after that takes 10–20 seconds (server start + ONNX init). Subsequent requests are fast.
- The Supabase service-role key and Gemini API key are **server-side secrets only** — never put them in the Angular frontend.

---

## Prerequisites

- Supabase project set up (free tier is fine)
- GitHub repository pushed
- Render account (free tier)
- Gemini API key from [Google AI Studio](https://aistudio.google.com/app/apikey)

---

## Step 1 — Supabase setup

### Enable pgvector

In Supabase SQL Editor, run:
```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

Confirm it worked:
```sql
SELECT * FROM pg_extension WHERE extname = 'vector';
```

### Create the storage bucket

1. Open your Supabase project → **Storage**
2. Click **New bucket**
3. Name it exactly: `onenest-documents`
4. **Public bucket: OFF** (must be private — files are only accessed via the backend)
5. Save

If you use a different bucket name, set `Supabase__StorageBucket` in the backend env vars to match.

### Get your Supabase credentials

You need these three values from **Project Settings → API / Database**:

| What | Where to find it |
|---|---|
| Project URL | Settings → API → Project URL |
| Service Role Key | Settings → API → `service_role` key |
| DB Connection String | Settings → Database → Connection string → **Transaction pooler** |

> The service_role key has full admin access to your database. Keep it server-side only.

---

## Step 2 — Run database migrations

Run this before deploying the backend (or any time you add new migrations):

```bash
cd backend
dotnet ef database update \
  --project OneNest.Infrastructure \
  --startup-project OneNest.API \
  --connection "Host=aws-0-...pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<your-db-password>;SSL Mode=Require;Trust Server Certificate=true"
```

This is safe to run multiple times — it only applies migrations that haven't run yet.

After running, confirm the embedding column was created:
```sql
-- In Supabase SQL Editor
SELECT column_name, udt_name FROM information_schema.columns
WHERE table_name = 'EmbeddingRecords';
-- Should show: Embedding | vector
```

---

## Step 3 — Deploy the backend

### Create the Render Web Service

1. Render dashboard → **New → Web Service**
2. Connect your GitHub repository
3. Settings:
   - **Root directory**: `backend`
   - **Runtime**: Docker
   - **Dockerfile path**: `./Dockerfile`
   - **Instance type**: Free

Render sets `PORT=10000` automatically. The Dockerfile reads `$PORT` at startup — no extra config needed.

### Set health check

In Web Service settings → Health & Alerts:
- **Health Check Path**: `/api/health`

### Set environment variables

Add these as **Secret Environment Variables** in the Render Web Service dashboard (Environment tab):

| Variable | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | Your Supabase Transaction Pooler connection string |
| `Jwt__Key` | A long random secret — min 64 chars (generate one below) |
| `Jwt__Issuer` | `OneNest.API` |
| `Jwt__Audience` | `OneNest.Client` |
| `AI__ApiKey` | Your Gemini API key |
| `AI__Provider` | `Gemini` |
| `AI__Model` | `gemini-2.5-flash` |
| `Embeddings__Provider` | `Local` |
| `Embeddings__Dimension` | `384` |
| `LocalEmbedding__ModelDirectory` | `/app/models/all-MiniLM-L6-v2` |
| `Supabase__Url` | `https://<project-ref>.supabase.co` |
| `Supabase__ServiceRoleKey` | Your Supabase service-role key |
| `Supabase__StorageBucket` | `onenest-documents` |
| `Cors__AllowedOrigins__0` | `https://<your-angular-site>.onrender.com` (set after step 4) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Generate a JWT key:
```bash
openssl rand -base64 64
# or on Windows:
node -e "console.log(require('crypto').randomBytes(64).toString('base64'))"
```

> If you ever change `Jwt__Key`, all existing tokens are invalidated and everyone gets logged out.

---

## Step 4 — Deploy the frontend

### Create the Render Static Site

1. Render dashboard → **New → Static Site**
2. Connect the same GitHub repository
3. Settings:
   - **Root directory**: `frontend/OneNest.Web`
   - **Build command**:
     ```
     npm ci && sed -i "s|ONENEST_API_URL_PLACEHOLDER|${API_URL}|g" src/environments/environment.prod.ts && ng build
     ```
   - **Publish directory**: `dist/OneNest.Web/browser`

4. Add this environment variable:

| Variable | Value |
|---|---|
| `API_URL` | `https://<your-api-service>.onrender.com/api` |

The `sed` command replaces the placeholder in `environment.prod.ts` with your real API URL before Angular builds.

### Add SPA rewrite rule

Angular uses client-side routing — without this, refreshing `/dashboard` returns a 404.

In Static Site settings → Redirects/Rewrites, add:

| Source | Destination | Action |
|---|---|---|
| `/*` | `/index.html` | **Rewrite** |

---

## Step 5 — Final wiring

1. Copy the Angular Static Site URL (e.g., `https://onenest-web.onrender.com`)
2. Go back to the backend Web Service → Environment
3. Set `Cors__AllowedOrigins__0` to that URL (exact match, no trailing slash)
4. **Redeploy the backend** to pick up the CORS change

---

## Deploy order (to avoid errors)

1. Run EF migrations → Supabase DB is ready
2. Create Supabase Storage bucket
3. Deploy backend → wait for "Live"
4. Copy backend URL, set `API_URL` in frontend env
5. Deploy frontend → copy frontend URL
6. Set `Cors__AllowedOrigins__0` in backend → redeploy backend

---

## Verify it's working

**Check embedding is ready** — in Render logs for the backend, you should see:
```
EmbeddingWarmup: ✓ provider ready — 384-dim vectors. Semantic indexing is enabled.
```

**Test the full RAG pipeline:**
1. Upload a PDF in Documents
2. Wait a few seconds (indexing is background)
3. Ask the AI something about the document content
4. The answer should reference your document

**Re-index existing content** (if needed):
```http
POST /api/semantic-search/backfill
Authorization: Bearer <your-jwt>
```

---

## Test locally with Docker

```bash
docker build -t onenest-api ./backend

docker run --rm -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=...;..." \
  -e Jwt__Key="test-key-at-least-32-chars-long-here" \
  -e Jwt__Issuer="OneNest.API" \
  -e Jwt__Audience="OneNest.Client" \
  -e AI__ApiKey="your-gemini-key" \
  -e Supabase__Url="https://your-project.supabase.co" \
  -e Supabase__ServiceRoleKey="your-service-role-key" \
  -e Supabase__StorageBucket="onenest-documents" \
  -e Cors__AllowedOrigins__0="http://localhost:4200" \
  -e PORT=8080 \
  onenest-api
```

Health check: `GET http://localhost:8080/api/health` → should return 200.

---

## What happens inside the Docker build

1. **Stage 1** — `dotnet publish` compiles the app. Checks that `Vocabularies/base_uncased.txt` (BERT tokenizer vocab) is in the output — build fails if it's missing.
2. **Stage 2** — Downloads `model_quantized.onnx` (all-MiniLM-L6-v2, ~22 MB) from HuggingFace and checks its SHA256 hash. Build fails if hash doesn't match.
3. **Stage 3** — Assembles the final runtime image with the app + model. Runs as non-root `app` user.

The model is downloaded once at build time and baked in — no download happens on startup.

---

## Troubleshooting

**All API requests return 401**
- Check `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` match the values used to generate existing tokens.
- If you changed `Jwt__Key`, all users need to log in again.

**File uploads fail (401/403 from Supabase)**
- Check `Supabase__ServiceRoleKey` is the `service_role` key (not the `anon` key).
- Check `Supabase__StorageBucket` matches the bucket you created.
- Make sure the bucket is **private** (not public).

**Semantic search returns nothing**
- Check the logs for `EmbeddingWarmup: ✗ provider returned null` and find the error above it.
- Run the backfill: `POST /api/semantic-search/backfill`.
- Check `Embeddings__Provider=Local` and `Embeddings__Dimension=384`.

**CORS errors (Angular can't reach API)**
- `Cors__AllowedOrigins__0` must be exactly the Angular URL with `https://` and no trailing slash.
- `API_URL` in Angular build must be the backend URL + `/api`.
- Redeploy backend after changing CORS setting.

**Docker build fails: "vocab file missing"**
- Check that `BERTTokenizers` NuGet copies the vocab to the publish directory.
- Test locally: `dotnet publish backend/OneNest.API -c Release -o /tmp/pub && ls /tmp/pub/Vocabularies/`

**Docker build fails: "SHA256 mismatch"**
- HuggingFace may have updated the file. Download it and recompute the hash:
  ```bash
  curl -fsSL "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx" -o /tmp/model.onnx
  sha256sum /tmp/model.onnx
  ```
- Only update the hash in the Dockerfile if you have verified the new file is correct.

---

## Cold start warning

Free Render services spin down after 15 minutes of inactivity. When they wake up:
- Server starts in ~5–10 s
- ONNX model init takes ~1–2 s more on first request
- Total cold start: 10–20 s

Subsequent requests are fast. For a personal project, cold starts are fine. To avoid them, ping `/api/health` every 14 minutes via a Render Cron Job (uses free tier hours).
