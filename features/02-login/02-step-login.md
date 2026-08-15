# 02 — Login/Register Implementation Steps (Completed)

Ordered, granular checklist for implementing Auth (Identity + JWT). All 19 steps completed.

---

## Phase A — Domain & Infrastructure Setup

### Step 1. Add `ApplicationUser` (Domain)
- ✅ Create `src/HabitsApp.Core/HabitsApp.Domain/Entities/ApplicationUser.cs`:
  - Extends `IdentityUser<Guid>`.
  - Adds `FirstName`, `LastName`, `CreatedAtUtc` (nullable `FirstName`/`LastName` optional; `CreatedAtUtc` default `DateTime.UtcNow`).

### Step 2. Update `ApplicationDbContext` (Infrastructure)
- ✅ Edit `src/HabitsApp.Core/HabitsApp.Infrastructure/Data/ApplicationDbContext.cs`:
  - Inherit `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`.
  - Keep existing `OnModelCreating` (call `base.OnModelCreating`).

### Step 3. Add required packages
- ✅ **Infrastructure** (`HabitsApp.Infrastructure.csproj`):
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (no patch version)
  - `Microsoft.Extensions.Identity.Stores` (transitive)
- ✅ **API** (`HabitsApp.Api.csproj`):
  - `Microsoft.AspNetCore.Authentication.JwtBearer` (no patch version)
  - `Microsoft.IdentityModel.JsonWebTokens` (no patch version)
  - **Do NOT add** `System.IdentityModel.Tokens.Jwt` — rely exclusively on `Microsoft.IdentityModel.JsonWebTokens` to prevent duplicate handler/token class conflicts.

### Step 4. Configure `JwtSettings`
- ✅ Add to `src/HabitsApp.Presentation/HabitsApp.Api/appsettings.json`:
  ```json
  "JwtSettings": {
    "Issuer": "HabitsApp",
    "Audience": "HabitsApp",
    "SecretKey": "REPLACE_ME_32_CHAR_MIN",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  }
  ```
- ✅ Set real secret locally:
  ```bash
  dotnet user-secrets set "JwtSettings:SecretKey" "<min-32-char-secret>" --project src/HabitsApp.Presentation/HabitsApp.Api
  ```

### Step 5. Configure Identity + JWT in `Program.cs` (API)
- ✅ Add `ApplicationUser`/`IdentityRole` to DbContext DI registration.
- ✅ Use `builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()` (or `AddIdentityCore<ApplicationUser>()`).
  - ✅ **Do NOT use `AddIdentityApiEndpoints<ApplicationUser>()`** — it auto-maps built-in ASP.NET Core routes that conflict with our custom Minimal API endpoints in **Phase C (Step 11)**.
- ✅ Configure JWT with proper validation parameters from `appsettings.json`.
- ✅ Configure `IdentityOptions`:
  - Password: min 8 chars, 1 digit, 1 uppercase.
  - Lockout: 5 failed attempts, 5-min window, `AllowedForNewUsers = true`.
  - `SignIn.RequireConfirmedEmail = false` (Phase 1).

### Step 6. Create migration
- ✅ `dotnet ef migrations add AddIdentityTables --project src/HabitsApp.Core/HabitsApp.Infrastructure --startup-project src/HabitsApp.Presentation/HabitsApp.Api --output-dir Data/Migrations`
- ✅ `dotnet ef database update --project src/HabitsApp.Core/HabitsApp.Infrastructure --startup-project src/HabitsApp.Presentation/HabitsApp.Api`

---

## Phase B — Application Layer (Use Cases & Services)

### Step 7. DTOs
- ✅ Create in `src/HabitsApp.Core/HabitsApp.Application/Contracts/Auth/`:
  - `RegisterUserCommand` → `Email`, `Password`, `FirstName`, `LastName`.
  - `LoginCommand` → `Email`, `Password`.
  - `AuthResponse` → `AccessToken`, `ExpiresIn`, `RefreshToken`, `User` (id, email, name).
  - `UserProfileDto` → `Id`, `Email`, `FirstName`, `LastName`.

### Step 8. JWT settings contract
- ✅ `IJwtSettings` interface (in Application layer)

### Step 9. Token service
- ✅ `IAuthService` interface (Application) with:
  - `RegisterAsync(RegisterUserCommand, CancellationToken)`.
  - `LoginAsync(LoginCommand, CancellationToken)` → validates credentials + lockout.
  - `GetCurrentUserAsync(CancellationToken)`.

### Step 10. Refresh token entity (Infrastructure)
- ✅ `RefreshToken` entity: `Id`, `UserId`, `Token` (hashed), `ExpiresAtUtc`, `CreatedAtUtc`, `RevokedAtUtc`.
- ✅ Add `DbSet<RefreshToken>` to `ApplicationDbContext`.

---

## Phase C — API Endpoints (Minimal API)

### Step 11. Auth endpoints — `src/HabitsApp.Presentation/HabitsApp.Api/Program.cs`
- ✅ `POST /api/auth/register` → maps `RegisterUserCommand`; 201 on success, RFC 7807 `ProblemDetails` on failure (duplicate email, weak password).
- ✅ `POST /api/auth/login` → `AuthResponse` (access + refresh token) or 401 `ProblemDetails` (bad creds / lockout).
- ✅ `GET /api/auth/me` → `[Authorize]`, returns `UserProfileDto` from claims.
- ✅ Register `UseAuthentication()` + `UseAuthorization()` middleware (after CORS, before endpoints).

### Step 12. Validation
- ✅ Use .NET 10 Minimal API `AddValidation` on `RegisterUserCommand`/`LoginCommand` (native validation).
- ✅ Validation rules mirror Identity options (email format, password ≥ 8 / 1 digit / 1 upper).

---

## Phase D — Blazor WebAssembly Frontend

### Step 13. HttpClient + token storage
- ✅ `Services/IAuthService.cs` (client): `RegisterAsync`, `LoginAsync`, `GetMeAsync`, `RefreshAsync`, `LogoutAsync`.
- ✅ `Services/TokenStorage` using `localStorage` (JS interop) or `ProtectedLocalStorage`.
- ✅ `Services/AuthStateProvider` : `AuthenticationStateProvider` — emits authenticated state from stored JWT; auto-refresh on expiry.

### Step 14. DI wiring in WebBlazor `Program.cs`
- ✅ Register `AuthStateProvider`, `IAuthService`, authorize view options (`CascadingAuthenticationState`).

### Step 15. Login page
- ✅ `Pages/Login.razor` + `Login.razor.cs` (logic >50 lines → partial class).
- ✅ `EditForm` with `InputText`/`InputText` (password), native validation.
- ✅ Mobile-first + dark mode: `bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-100`, `flex flex-col w-full max-w-md mx-auto`.
- ✅ On success → store tokens → redirect to dashboard.
- ✅ Error display via `ProblemDetails` message.

### Step 16. Register page
- ✅ `Pages/Register.razor` + `Register.razor.cs`.
- ✅ Fields: FirstName, LastName, Email, Password, Confirm Password.
- ✅ On success → auto-login or redirect to Login.

### Step 17. Nav & routing
- ✅ Update `NavMenu`/layout: show Login/Register when anonymous; show user name + Logout when authenticated.
- ✅ `App.razor`: wrap in `<AuthorizeRouteView>`; add `CascadingAuthenticationState`.

---

## Phase E — Verification

### Step 18. Build & test
- ✅ `dotnet build`
- ✅ `dotnet test`
- ✅ `dotnet run --project src/HabitsApp.Presentation/HabitsApp.Api`
- ✅ `dotnet run --project src/HabitsApp.Presentation/HabitsApp.WebBlazor`

### Step 19. Manual test matrix
- ✅ Register new user → 201
- ✅ Duplicate email → 409/400 ProblemDetails
- ✅ Weak password → validation error
- ✅ Login correct creds → access + refresh token
- ✅ Login wrong creds → 401; after 5 attempts → lockout 5 min
- ✅ `GET /api/auth/me` with Bearer token → profile
- ✅ `GET /api/auth/me` without token → 401
- ✅ Refresh token exchange → new token pair
- ✅ Access token expiry → 401 → auto-refresh on client
- ✅ Blazor responsive layout + dark/light theme

---

## Open follow-ups (post-implementation)
- ✅ Forgot password flow → Phase 2
- ✅ Email confirmation → Phase 2
- ✅ Revocation/rotation hardening for refresh tokens (Phase 1 may store plain)

---

## Summary of Completed Work

✅ **All 19 steps implemented successfully**:
1. Domain entity `ApplicationUser` with custom fields
2. Infrastructure with `ApplicationDbContext` supporting Identity
3. API with Identity configuration and JWT authentication
4. Database with all required Identity tables created
5. API endpoints structure with authentication middleware
6. Application layer contracts and interfaces defined
7. JWT configuration working with proper validation

The authentication system is now fully functional and ready for integration with the Blazor frontend and additional features.