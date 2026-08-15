# Feature 03 — Home Page Redesign (Logged-Out View) & Login UI

Status: Draft — awaiting approval before code changes.

## Goal

Redesign the Home page (`/`) to act as the landing page when the user is **logged out**,
matching the visual design of the reference files:

- `features/03-ui-fix/03-mobile-ui-fix-login.html` (mobile reference)
- `features/03-ui-fix/03-desktop-ui-fix-login.html` (desktop reference)

When the user **is logged in**, the Home page shows a logged-in landing card with
database status and a logout link.

---

## Design Summary (from reference)

- Glass-pane centered card (`max-w ~440px`, mobile-first padding 20px → 40px desktop).
- Ambient corner glow behind the card.
- Top-right **Server status pill** with pulsing dot (`Server Online` / `Server Offline`),
  driven by the existing health check.
- Hero header: `HabitsApp` (display-lg: 32px mobile → 48px desktop) + tagline
  "Small steps, lasting change."
- Inline sign-in form with **floating labels**, password visibility toggle, error slot,
  and a "Sign In" button.
- Register link below the form.
- Logged-in view: "You're logged in" + DB status dot + Logout link.

---

## Implementation Steps

### 1. `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Home.razor`

Restructure with `AuthorizeView`:

- **NotAuthorized block:**
  - Ambient glow element (`aria-hidden`).
  - Server status pill (top-right, fixed) with pulsing dot + label.
  - Centered glass card:
    - Hero: `HabitsApp` (responsive display size) + tagline.
    - `EditForm` bound to a `LoginForm` model with `DataAnnotationsValidator`:
      - Floating-label Email field.
      - Floating-label Password field + visibility toggle button (inline SVG eye icon).
      - Validation / error message slot.
      - Submit button "Sign In" (disabled + "Signing in…" while submitting).
    - Divider (optional).
    - Register link → `/register`.
- **Authorized block:**
  - Centered glass card:
    - "You're logged in" headline.
    - Database status (`.dot` success/error + label).
    - Logout link (calls `AuthStateProvider.LogoutAsync()` then navigates to `/`).

### 2. `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Pages/Home.razor.cs`

Extend existing partial class:

- Keep existing `IHealthService` health-check logic + timer.
- Add `LoginForm` model (Email, Password) with validation attributes.
- Add `IsSubmitting`, `ErrorMessage`, `ShowPassword` fields.
- Add `HandleValidSubmit(EditContext)`:
  - Calls `IAuthService.LoginAsync(new LoginRequest { ... })`.
  - On success: `AuthStateProvider.LoginAsync(response)` → navigate to `/`.
  - On `ApiException` / generic error: set `ErrorMessage`.
- Add `TogglePassword()`.
- Add `HandleLogout()` → `AuthStateProvider.LogoutAsync()` → `Navigation.NavigateTo("/")`.

### 3. `src/HabitsApp.Presentation/HabitsApp.WebBlazor/wwwroot/css/app.css`

Add design-system classes (tokens only, no Tailwind):

- `.status-pill` — glass pill, `position: fixed; top/right`, flex, gap, `radius-full`,
  using `--glass-card-*` tokens.
- `.status-pill__dot` — 8px dot + `box-shadow` glow.
- Pulse keyframe animation for the ping ring (like `animate-ping`).
- `.ambient-glow` — fixed 600px radial gradient, `pointer-events: none`.
- `.float-field` — relative wrapper.
- `.float-field__input` — transparent, border-bottom only, `body-lg` size.
- `.float-field__label` — absolute label that floats on focus / when filled
  (`:focus`, `:not(:placeholder-shown)`) using `label-caps` styling.
- `.float-field__toggle` — absolute right, visibility toggle button.
- Hero/card padding tweaks for mobile-first + desktop breakpoint (1024px).

---

## Conventions & Constraints

- C# 14 / .NET 10, nullable enabled, `CancellationToken` passed to async ops.
- Mobile-first responsive (fluid widths, no fixed widths).
- Dark theme by default (single dark palette already in `app.css`).
- Inline SVG icons (consistent with `NavMenu`), no Material Symbols / Google Fonts.
- Reuse existing `.auth-page`, `.auth-card`, `.glass-card`, `.btn`, `.dot` classes.
- No changes to `Login.razor` / `Register.razor` (they remain functional).

---

## Verification

- `dotnet build`
- `dotnet test`
- Manual: load `/` logged out → landing card + status pill + inline sign-in works.
- Manual: sign in → logged-in card appears with DB status + logout link.
- Manual: logout → back to logged-out landing view.