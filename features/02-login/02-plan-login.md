# 02 — Login/Register Plan

This document captures the full discussion and resolved assumptions for the Authentication & Authorization feature.

---

## 1. Draft Summary

Source: `02-draft.md`

Implement ASP.NET Core Identity with JWT in `HabitsApp.Api` and set up the Login/Register flow for `HabitsApp.WebBlazor`.

- **Domain & Infrastructure**: Create `ApplicationUser` extending `IdentityUser<Guid>` with `FirstName`, `LastName`, `CreatedAtUtc`. Update `ApplicationDbContext` to inherit from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`. Configure Identity in `Program.cs` and create the EF Core migration (e.g., `AddIdentityUser`).
- **Application Layer**: Use Cases — `RegisterUserCommand`, `LoginQuery`/`LoginCommand`, `GetCurrentUserQuery`. Define JWT Settings interface and Token Generator service.
- **API & Blazor Frontend**: Minimal API endpoints under `/api/auth`:
  - `POST /api/auth/register`
  - `POST /api/auth/login`
  - `GET /api/auth/me`
- Build/Update Blazor login & registration views. Maintain Responsive/Mobile-first layout and Light/Dark theme compatibility.

---

## 2. Architectural Proposal (Initial)

- **Domain**: `ApplicationUser` extends `IdentityUser<Guid>` with custom fields.
- **Infrastructure**: `ApplicationDbContext` supports Identity; configure Identity services and JWT settings; implement token generation service.
- **Application**: `AuthService` with:
  - `RegisterUserAsync` command
  - `LoginAsync` query returning JWT
  - `GetCurrentUserAsync` query
- **API**: Auth endpoints (`POST /auth/register`, `POST /auth/login`, `GET /auth/me`).
- **Blazor Client**: Login/register components, token storage, auth state provider.

**Key considerations raised:**
- JWT expiration time
- Password requirements
- Email verification flow
- Error handling strategy

---

## 3. Open Assumptions (Review Stage)

1. **Security**:
   - JWT signing key storage (Azure Key Vault vs appsettings)
   - Token expiration time (default 1 hour?)
   - Refresh token implementation needed?
2. **Data**:
   - Password complexity requirements
   - Email verification workflow
   - Account lockout policy
3. **UI**:
   - Dark mode compatibility for auth components
   - Mobile-first form layouts
   - Forgot password flow
4. **Dependencies**:
   - Tailwind vs MudBlazor for auth components
   - JWT library choice (`System.IdentityModel.Tokens.Jwt`)
5. **Validation**:
   - Client-side form validation
   - API error response format

---

## 4. Resolved Assumptions

- **JWT Storage & Lifetime**: User Secrets (Local) / Env Vars (Prod). Access Token: 60 mins. Refresh Token: 7 days stored in DB.
- **JWT Secret Key Configuration**: Managed via `JwtSettings:SecretKey`. `appsettings.json` contains a dummy placeholder, while the actual secret is injected via `dotnet user-secrets` (min 32 chars).
- **Identity Policies**: Min 8 chars, 1 digit, 1 uppercase. `RequireConfirmedEmail = false` for Phase 1. Lockout after 5 failed attempts (5 min duration).
- **UI & UX**: Tailwind CSS (Mobile-First + Dark Mode support). Blazor native EditForm validation. "Forgot Password" deferred to Phase 2.
- **Libraries & API Standards**: Use `Microsoft.IdentityModel.JsonWebTokens`. Standard RFC 7807 `ProblemDetails` for API errors.

---

## 5. Implementation Plan (Approved)

1. **Core Setup**:
   - `dotnet user-secrets set "JwtSettings:SecretKey" "<32_char_secure_key>"` (local dev)
   - Configure `JwtSettings` in `appsettings.json`:
     ```json
     "JwtSettings": {
       "Issuer": "HabitsApp",
       "Audience": "HabitsApp",
       "ExpiryMinutes": 60,
       "RefreshTokenExpiryDays": 7
     }
     ```
2. **Database Migration**:
   - Add Identity tables via EF Core migration:
     ```bash
     dotnet ef migrations add "AddIdentityTables" -p src/HabitsApp.Core/HabitsApp.Infrastructure
     ```
3. **API Endpoints**:
   - Implement Minimal API endpoints with:
     - Input validation using .NET 10 `AddValidation`
     - RFC 7807 error responses
     - JWT bearer authentication
4. **Blazor Client**:
   - Create `AuthStateProvider` for token management
   - Build responsive login form with Blazor native `EditForm` validation
   - Maintain mobile-first layout + dark mode classes (`bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-100`)
5. **Verification**:
   - Test flows:
     - Registration → Login → API call with JWT
     - Failed login attempts triggering lockout
     - Token expiration/refresh