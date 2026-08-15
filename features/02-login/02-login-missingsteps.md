# 02 — Login/Register: Missing Implementation Steps (Draft)

Source: `02-step-login.md` (claims all 19 steps complete) — **verified incomplete**. This draft documents the gaps and provides a granular, ordered checklist to finish Phases B–E.

---

## 1. Verification Summary (Phase 0)

| Area | Status | Evidence |
| --- | --- | --- |
| Phase A (Steps 1–6) | ✅ Done | `ApplicationUser`, `ApplicationDbContext`, Identity/JWT packages + config, `AddIdentityTables` migration exist |
| Phase B (Steps 7–10) | ❌ Missing | No Auth DTOs in Application, no `IJwtSettings`, no `IAuthService`, no `RefreshToken` entity/DbSet |
| Phase C (Steps 11–12) | ⚠️ Partial | Endpoints exist inline but return no tokens; no native validation |
| Phase D (Steps 13–17) | ❌ Missing | No client auth services, pages, or routing |
| Phase E (Steps 18–19) | ⚠️ Untestable | Login issues no JWT, so manual test matrix cannot pass |

### Critical defect
`POST /api/auth/login` (`HabitsApp.Api/Program.cs:115-134`) returns `{ message: "Login successful" }` — no access/refresh token is ever issued. Register returns a generic 400 `ProblemDetails` with no per-error detail. The auth system is non-functional end-to-end.

---

## 2. Architectural Proposal

### 2.1 Application Layer (`HabitsApp.Application`) — New files
- `Contracts/Auth/RegisterUserCommand.cs`, `Contracts/Auth/LoginCommand.cs`, `Contracts/Auth/AuthResponse.cs`, `Contracts/Auth/UserProfileDto.cs` (move commands out of API `Program.cs`).
- `Contracts/Auth/IJwtSettings.cs` + `Services/JwtSettings.cs` (implement `IJwtSettings`, bound from `JwtSettings:` config section).
- `Contracts/Auth/IAuthService.cs` + `Services/AuthService.cs`:
  - `RegisterAsync(RegisterUserCommand, CancellationToken)` → `AuthResponse` (auto-login after register).
  - `LoginAsync(LoginCommand, CancellationToken)` → `AuthResponse` or 401 (bad creds / lockout).
  - `GetCurrentUserAsync(ClaimsPrincipal, CancellationToken)` → `UserProfileDto`.
  - Token issuance: access token via `Microsoft.IdentityModel.JsonWebTokens` (`JsonWebTokenHandler`), refresh token generation + persistence (hashed).
- `ITokenService` (optional internal split): `CreateAccessTokenAsync(ApplicationUser)`, `CreateRefreshTokenAsync(Guid userId)`.

> **Note**: `HabitsApp.Application` currently references `HabitsApp.Infrastructure` (inverted Clean Architecture). `IAuthService` must reference only Domain + `IJwtSettings` + `IJwtSettings`-style abstractions; EF/Identity-specific access is resolved by concrete infra/service implementations registered in the API or via `Microsoft.Extensions.Identity.Core` abstractions (`UserManager<T>`). Keep commands/DTOs dependency-free so validation (`AddValidation`) can run on the request body.

### 2.2 Infrastructure (`HabitsApp.Infrastructure`)
- `Entities/RefreshToken.cs`: `Id` (Guid), `UserId` (Guid), `TokenHash` (SHA-256), `ExpiresAtUtc`, `CreatedAtUtc`, `RevokedAtUtc` (nullable). Add `DbSet<RefreshToken>` to `ApplicationDbContext`; configure FK to `AspNetUsers` + unique index on `TokenHash`.
- New migration `AddRefreshTokens` (do **not** merge into `AddIdentityTables`; DB may already be updated).
- `Services/RefreshTokenService.cs` (optional): create/rotate/revoke/validate.

### 2.3 API (`HabitsApp.Api`)
- `Program.cs` refactor: register `IJwtSettings` (bound via `builder.Configuration.GetSection("JwtSettings")`), `IAuthService`.
- Rewrite endpoints:
  - `POST /api/auth/register` → 201 + `AuthResponse`; 400 `ProblemDetails` with Identity error detail (duplicate email, weak password); 409-style detail for existing email.
  - `POST /api/auth/login` → 200 + `AuthResponse`; 401 `ProblemDetails`; `isLockedOut` awareness (401 vs 423-style message).
  - `POST /api/auth/refresh` → new token pair (validates hashed refresh token, rotation).
  - `POST /api/auth/logout` → revoke refresh token (`[Authorize]`).
  - `GET /api/auth/me` → `UserProfileDto` from claims + DB (`[Authorize]`).
- Add native validation via .NET 10 Minimal API `AddValidation` on `RegisterUserCommand`/`LoginCommand` (email format, password ≥ 8 / 1 digit / 1 upper, `ConfirmPassword` match on register).
- Wire `app.UseAuthentication()` / `app.UseAuthorization()` (already present).
- `SignIn.RequireConfirmedEmail = false` explicitly (Phase 1).

### 2.4 Blazor Client (`HabitsApp.WebBlazor`) — New files
- `Services/IAuthService.cs` (client): `RegisterAsync`, `LoginAsync`, `GetMeAsync`, `RefreshAsync`, `LogoutAsync`.
- `Services/TokenStorage.cs`: JS interop wrapper over `localStorage` (access token, refresh token, expiry).
- `Services/AuthStateProvider.cs` : `AuthenticationStateProvider` — emit authenticated state from stored JWT, auto-refresh on expiry (401 → refresh → retry), `Logout`.
- `Services/AuthorizingHttpMessageHandler.cs` (DelegatingHandler): attach `Authorization: Bearer <token>`.
- `Pages/Login.razor` + `Login.razor.cs`: `EditForm`, email/password, `ProblemDetails` error display, on success store tokens → redirect.
- `Pages/Register.razor` + `Register.razor.cs`: FirstName, LastName, Email, Password, Confirm Password; auto-login or redirect to Login.
- Update `App.razor`: `<CascadingAuthenticationState>` + `<AuthorizeRouteView>` + redirect to Login when anonymous.
- Update `Layout/NavMenu.razor`: Login/Register when anonymous; user name + Logout when authenticated.
- Update `Program.cs` (WebBlazor): register `AuthStateProvider`, `IAuthService`, `TokenStorage`, `AuthorizingHttpMessageHandler`, `AddOptions` + authorize view options.
- Mobile-first + dark mode on all new components (`bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-100`, `flex flex-col w-full max-w-md mx-auto`).

---

## 3. Open Assumptions (to confirm before code)

1. **Refresh tokens**: stored **hashed** (SHA-256) per Phase 2 hardening, or plaintext for Phase 1? Proposal: hashed now.
2. **Register behavior**: auto-login (return `AuthResponse` + 201) vs. redirect to Login page? Proposal: auto-login.
3. **Token storage** (Blazor): JS interop `localStorage` vs `ProtectedLocalStorage`. Proposal: `localStorage` via JS interop (simpler PWA).
4. **`IAuthService` placement**: Application-layer interface implemented in API (needs `UserManager`, `DbContext`), keeping Application free of EF/Identity packages.
5. **Duplicate email response**: 400 vs 409. Proposal: 409 with `ProblemDetails` title "Email already registered".
6. **Refresh endpoint auth**: unauthenticated refresh-token body vs `[Authorize]`. Proposal: body-based, no auth header required.
7. **API base URL / CORS**: `ApiBaseUrl` from `wwwroot/appsettings.json`; ensure Blazor dev origin added to CORS policy.

---

## 4. Implementation Plan (granular checklist)

### Phase 0 — Verification (DONE)
- [x] Audit current state; record gaps (Section 1).

### Phase B — Application Layer
- [ ] B1. Create `Contracts/Auth/RegisterUserCommand.cs` (Email, Password, ConfirmPassword, FirstName?, LastName?).
- [ ] B2. Create `Contracts/Auth/LoginCommand.cs` (Email, Password).
- [ ] B3. Create `Contracts/Auth/AuthResponse.cs` (AccessToken, ExpiresIn, RefreshToken, User: `UserProfileDto`).
- [ ] B4. Create `Contracts/Auth/UserProfileDto.cs` (Id, Email, FirstName, LastName).
- [ ] B5. Create `Contracts/Auth/IJwtSettings.cs` (Issuer, Audience, SecretKey, ExpiryMinutes, RefreshTokenExpiryDays).
- [ ] B6. Create `Contracts/Auth/IAuthService.cs` (`RegisterAsync`, `LoginAsync`, `RefreshAsync`, `LogoutAsync`, `GetCurrentUserAsync`).
- [ ] B7. Create `Services/JwtSettings.cs` implementing `IJwtSettings` (bound from config in API).

### Phase B2 — Infrastructure
- [ ] B8. Create `Entities/RefreshToken.cs` (Id, UserId, TokenHash, ExpiresAtUtc, CreatedAtUtc, RevokedAtUtc).
- [ ] B9. Add `DbSet<RefreshToken>` + fluent config (FK to `AspNetUsers`, unique index on `TokenHash`) in `ApplicationDbContext`.
- [ ] B10. `dotnet ef migrations add AddRefreshTokens --project src/HabitsApp.Core/HabitsApp.Infrastructure --startup-project src/HabitsApp.Presentation/HabitsApp.Api --output-dir Data/Migrations`
- [ ] B11. `dotnet ef database update --project src/HabitsApp.Core/HabitsApp.Infrastructure --startup-project src/HabitsApp.Presentation/HabitsApp.Api`

### Phase C — API Endpoints
- [ ] C1. Register `IJwtSettings` + `IAuthService` in API DI (bound from `JwtSettings:` section).
- [ ] C2. Add `AddValidation` (native Minimal API validation) for auth commands.
- [ ] C3. Rewrite `POST /api/auth/register` → 201 + `AuthResponse` (auto-login) or `ProblemDetails` (duplicate email 409 / weak password 400 with detail).
- [ ] C4. Rewrite `POST /api/auth/login` → 200 + `AuthResponse` or 401 `ProblemDetails` (bad creds / lockout); respect `LockoutEnabled`.
- [ ] C5. Add `POST /api/auth/refresh` → rotate refresh token, return new `AuthResponse`.
- [ ] C6. Add `POST /api/auth/logout` → revoke refresh token, `[Authorize]`.
- [ ] C7. Rewrite `GET /api/auth/me` → `UserProfileDto` from claims + DB, `[Authorize]`.
- [ ] C8. Set `SignIn.RequireConfirmedEmail = false` in Identity options.
- [ ] C9. Remove inline `RegisterUserCommand`/`LoginCommand` classes from API `Program.cs`; use Application contracts.

### Phase D — Blazor Client
- [ ] D1. Create `Services/IAuthService.cs` (client): `RegisterAsync`, `LoginAsync`, `GetMeAsync`, `RefreshAsync`, `LogoutAsync`.
- [ ] D2. Create `Services/TokenStorage.cs` (localStorage via JS interop).
- [ ] D3. Create `Services/AuthStateProvider.cs` (emit auth state; auto-refresh on expiry/401).
- [ ] D4. Create `Services/AuthorizingHttpMessageHandler.cs` (attach Bearer token).
- [ ] D5. Register auth services + `AuthorizeView`/`CascadingAuthenticationState` options in WebBlazor `Program.cs`.
- [ ] D6. Create `Pages/Login.razor` + `Login.razor.cs` (mobile-first + dark mode, `EditForm`, `ProblemDetails` errors).
- [ ] D7. Create `Pages/Register.razor` + `Register.razor.cs` (FirstName, LastName, Email, Password, Confirm; auto-login).
- [ ] D8. Update `App.razor`: `CascadingAuthenticationState` + `AuthorizeRouteView` + anonymous redirect.
- [ ] D9. Update `Layout/NavMenu.razor`: anonymous → Login/Register; authenticated → user name + Logout.
- [ ] D10. Ensure `wwwroot/appsettings.json` `ApiBaseUrl` + API CORS include Blazor origin.

### Phase E — Verification
- [ ] E1. `dotnet build` (both solutions/projects clean).
- [ ] E2. `dotnet test`.
- [ ] E3. `dotnet run --project src/HabitsApp.Presentation/HabitsApp.Api` + WebBlazor.
- [ ] E4. Manual matrix:
  - Register → 201 + tokens; duplicate email → 409; weak password → validation error.
  - Login correct → tokens; wrong creds → 401; 5 attempts → lockout.
  - `GET /api/auth/me` with/without Bearer → profile / 401.
  - Refresh exchange → new pair; access expiry → auto-refresh on client.
  - Blazor responsive + dark/light theme.

---

## 5. Deliverables

- Application contracts: `RegisterUserCommand`, `LoginCommand`, `AuthResponse`, `UserProfileDto`, `IJwtSettings`, `IAuthService`, `JwtSettings`.
- Infrastructure: `RefreshToken` entity, `DbSet`, `AddRefreshTokens` migration.
- API: 5 endpoints (`register`, `login`, `refresh`, `logout`, `me`) with native validation + `ProblemDetails`.
- Blazor: `IAuthService`, `TokenStorage`, `AuthStateProvider`, `AuthorizingHttpMessageHandler`, Login/Register pages, auth-aware `App.razor` + `NavMenu`.
- Verified build/test + manual matrix.

---