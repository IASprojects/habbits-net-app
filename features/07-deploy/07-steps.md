# Implementation Steps — Automated Deployment to GitHub Pages (Stage)

Feature folder: `features/07-deploy`

## Context

The app is a .NET 10 monorepo:
- `HabitsApp.Api`: ASP.NET Core 10 Minimal API + EF Core 10 + PostgreSQL (Npgsql). Runs `Database.MigrateAsync()` on startup.
- `HabitsApp.WebBlazor`: Blazor WebAssembly (standalone, `Microsoft.NET.Sdk.BlazorWebAssembly`), static files. No service worker/manifest. No Brotli compression enabled.
- Solution: `HabitsApp.slnx`.
- Auth: JWT (HMAC-SHA256) + ASP.NET Core Identity + rotated/hashed refresh tokens. `JwtSettings:SecretKey` is a placeholder (`REPLACE_ME_32_CHAR_MIN`).
- Current runtime locations: no `stage` branch, no `.github/workflows`, no `Dockerfile`.

## Goal

When a PR from `feature/*` is **merged into `stage`**, GitHub Actions builds + tests the solution and deploys:
- **API → Render.com** (Web Service, branch `stage`) via a **Deploy Hook** (Actions orchestrates, Render builds & hosts).
- **DB → Neon.tech** (managed PostgreSQL, no deploy; only a connection string + startup migration).
- **Blazor WASM → GitHub Pages** via the official `actions/*-pages` actions (Direct static deploy).

Approved decisions:
- Trigger = `push` to `stage` (merge into stage).
- Backend host = **Render.com** via Deploy Hook.
- DB = **Neon.tech** (free managed Postgres, pooled port 5432).
- Frontend host = **GitHub Pages** (project subpath `/habbits-net-app/`).
- Frontend and backend stay **separate origins** → CORS must allow the GitHub Pages origin on the API.
- `ApiBaseUrl` for production provided via `wwwroot/appsettings.Production.json` (Blazor WASM defaults to the `Production` environment when no `blazor-environment` attribute is set in `index.html`).

---

## 1. Manual setup (one-time, outside Git)

- [ ] **Create `stage` branch** from current integration state:
  ```bash
  git checkout -b stage origin/main && git push -u origin stage
  ```
- [ ] **Neon.tech**: create a project + Postgres database. Copy the **pooled** connection string (port 5432). Save it for Render.
- [ ] **Render.com** — create a Web Service:
  - Connect the GitHub repo `IASprojects/habbits-net-app`.
  - Branch: `stage`.
  - Set **auto-deploy = OFF** (we trigger deploys via Actions + Deploy Hook).
  - Build command (native .NET buildpack) & start command per Render's .NET 10 runtime. **If Render's buildpack does not yet support .NET 10, use the `Dockerfile` alternative (Section 6) with `Runtime = Docker`.**
  - Environment variables (Render → Environment):
    - `ConnectionStrings__DefaultConnection` = the Neon pooled connection string.
    - `JwtSettings__SecretKey` = a strong random key ≥ 32 bytes:
      ```bash
      openssl rand -base64 48
      ```
    - `JwtSettings__Issuer__`/`Audience` (keep `HabitsApp`), `Cors__AllowedOrigins__0` = `https://iasprojects.github.io`.
  - Copy the **Deploy Hook URL** (Settings → Deploy Hook) → will be used as a Secret in GitHub.
- [ ] **GitHub Pages**:
  - Repo Settings → Pages → Source: **GitHub Actions**.
  - (Optional) Keep default project URL `https://iasprojects.github.io/habbits-net-app/` or configure a custom domain later.
- [ ] **GitHub Secrets** (Settings → Secrets and variables → Actions):
  - `RENDER_DEPLOY_HOOK` — the Render Deploy Hook URL.
  - (If custom domain used later, update the CORS origin + base href accordingly.)

## 2. API changes — `src/HabitsApp.Presentation/HabitsApp.Api`

### `Program.cs`

- [ ] **Resilient DB connection** — add `EnableRetryOnFailure` to `UseNpgsql`:
  ```csharp
  builder.Services.AddDbContext<ApplicationDbContext>(options =>
      options.UseNpgsql(
          builder.Configuration.GetConnectionString("DefaultConnection"),
          npgsql => npgsql.EnableRetryOnFailure(
              maxRetryCount: 5,
              maxRetryDelay: TimeSpan.FromSeconds(10),
              errorCodesToAdd: null)));
  ```
- [ ] **Forwarded headers** (behind Render's reverse proxy — required for reliable HTTPS/auth redirects) — before `app.UseHttpsRedirection()`:
  ```csharp
  builder.Services.Configure<ForwardedHeadersOptions>(options =>
  {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      options.KnownNetworks.Clear();
      options.KnownProxies.Clear();
  });
  ...
  app.UseForwardedHeaders();
  ```
- [ ] **CORS configurable** — replace the hardcoded `AddCors("WebBlazor")` policy with a config-driven one (`appsettings.json` + Render env override):
  ```csharp
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("WebBlazor", policy =>
      {
          var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
          policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
      });
  });
  ```
- [ ] **`appsettings.json`** — add dev origins and keep env overrides for Render:
  ```json
  "Cors": { "AllowedOrigins": [ "http://localhost:5119", "https://localhost:7243" ] }
  ```
  (Production value injected via Render env `Cors__AllowedOrigins__0` = `https://iasprojects.github.io`; no secret in the repo.)

## 3. Frontend changes — `src/HabitsApp.Presentation/HabitsApp.WebBlazor`

- [ ] **Create `wwwroot/appsettings.Production.json`** with the deployed API URL (placeholder to be replaced once the Render service is created):
  ```json
  {
    "ApiBaseUrl": "https://<your-app>.onrender.com"
  }
  ```
- [ ] **Add SPA fallback `wwwroot/404.html`** (GitHub Pages has no rewrite support; deep links like `/habits` must bounce to index.html):
  ```html
  <!DOCTYPE html>
  <html lang="en">
  <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0" />
      <title>Redirecting…</title>
  </head>
  <body>
      <script>
          (function () {
              var requested = window.location.href;
              try { sessionStorage.setItem("requestedPath", requested); } catch (e) { }
              window.location.replace("/habbits-net-app/");
          })();
      </script>
  </body>
  </html>
  ```
- [ ] **Restore the requested path in `wwwroot/index.html`** — add this script at the very top of `<body>` (before the app div) so Blazor can deep-link after the 404 bounce:
  ```html
  <script>
      (function () {
          var requested = sessionStorage.getItem("requestedPath");
          if (requested) {
              sessionStorage.removeItem("requestedPath");
              history.replaceState(null, "", requested);
          }
      })();
  </script>
  ```

> Note: do **not** change `<base href="/" />` in source. The subpath base is injected at publish time in CI (Section 4) so local dev still uses the root.

## 4. Workflow — `.github/workflows/deploy-stage.yml`

- [ ] Create the workflow (trigger on push to `stage`; required `pages` + `id-token` permissions):
  ```yaml
  name: Deploy to Stage

  on:
    push:
      branches: [stage]

  permissions:
    contents: read
    pages: write
    id-token: write

  concurrency:
    group: "pages"
    cancel-in-progress: true

  jobs:
    ci:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: "10.0.x"
        - name: Restore
          run: dotnet restore HabitsApp.slnx
        - name: Build
          run: dotnet build HabitsApp.slnx -c Release --no-restore
        - name: Test
          run: dotnet test HabitsApp.slnx -c Release --no-build

    deploy-api:
      needs: ci
      runs-on: ubuntu-latest
      steps:
        - name: Trigger Render deploy hook
          run: curl -fsS -X POST "${{ secrets.RENDER_DEPLOY_HOOK }}"

    deploy-frontend:
      needs: ci
      runs-on: ubuntu-latest
      environment:
        name: github-pages
        url: ${{ steps.deployment.outputs.page_url }}
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: "10.0.x"
        - name: Publish Blazor WASM
          run: dotnet publish src/HabitsApp.Presentation/HabitsApp.WebBlazor/HabitsApp.WebBlazor.csproj -c Release -o publish
        - name: Prepare GitHub Pages output (base href + nojekyll)
          run: |
            sed -i 's|<base href="/" />|<base href="/habbits-net-app/" />|' publish/wwwroot/index.html
            touch publish/wwwroot/.nojekyll
        - uses: actions/configure-pages@v5
        - uses: actions/upload-pages-artifact@v3
          with:
            path: publish/wwwroot
        - id: deployment
          uses: actions/deploy-pages@v4
  ```
- [ ] (Optional) Create a separate `.github/workflows/ci.yml` for PR gates (triggered on `pull_request` to `stage`/`main`, running only the `ci` job, **no deploy**).

## 5. Branch protection (optional but recommended)

- [ ] On `stage`: enable **Require a pull request before merging** and **Require status checks** (`ci`), so every merged change is verified and deployable.

## 6. (Fallback) Docker deployment for Render if .NET 10 buildpack is unavailable

- [ ] Create `Dockerfile` at repo root:
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  WORKDIR /src
  COPY . .
  RUN dotnet publish src/HabitsApp.Presentation/HabitsApp.Api/HabitsApp.Api.csproj -c Release -o /app

  FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
  WORKDIR /app
  COPY --from=build /app .
  EXPOSE 80
  ENTRYPOINT ["dotnet", "HabitsApp.Api.dll"]
  ```
- [ ] Configure the Render Web Service with `Runtime = Docker` (branch `stage`, auto-deploy off, same env vars, same Deploy Hook).

## 7. Verification

- [ ] `dotnet build HabitsApp.slnx`
- [ ] `dotnet test HabitsApp.slnx`
- [ ] Manual end-to-end:
  1. Merge a `feature/*` PR into `stage` → Actions runs `ci` → `deploy-api` (Render hook) + `deploy-frontend` (Pages).
  2. Render shows a successful deploy and serves the API at `https://<app>.onrender.com`; `Database.MigrateAsync` succeeds against Neon.
  3. GitHub Pages serves the WASM at `https://iasprojects.github.io/habbits-net-app/`.
  4. Register/Login works (JWT against the deployed API; CORS allows the Pages origin; access/refresh token flow works).
  5. Refresh on `/habits` and `/calendar` (SPA fallback) still renders.
  6. Table/desktop layout: `BottomNav` visible with icon + label.