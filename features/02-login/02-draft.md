@AGENTS.md

Now that our Aiven PostgreSQL connection is set up and verified, we are ready to implement User Authentication and Authorization.

### Goal
Implement ASP.NET Core Identity with JWT (JSON Web Tokens) in `HabitsApp.Api` and set up the Login/Register flow for `HabitsApp.WebBlazor`.

### Requirements & Architecture Alignment

1. **Domain & Infrastructure (`HabitsApp.Domain` / `HabitsApp.Infrastructure`)**:
   - Create `ApplicationUser` extending `IdentityUser<Guid>` with custom fields: `FirstName`, `LastName`, `CreatedAtUtc`.
   - Update `ApplicationDbContext` to inherit from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`.
   - Configure ASP.NET Core Identity in `Program.cs` and create the EF Core migration (e.g., `AddIdentityUser`).

2. **Application Layer (`HabitsApp.Application`)**:
   - Create Use Cases / Commands:
     - `RegisterUserCommand`: Receives `Email`, `Password`, `FirstName`, `LastName`.
     - `LoginQuery` / `LoginCommand`: Validates credentials and generates a JWT Bearer Token.
     - `GetCurrentUserQuery`: Retrieves current authenticated user profile.
   - Define JWT Settings interface and Token Generator service.

3. **API & Blazor Frontend**:
   - Create Minimal API endpoints under `/api/auth`:
     - `POST /api/auth/register`
     - `POST /api/auth/login`
     - `GET /api/auth/me`
   - Build/Update Blazor login & registration views in `HabitsApp.WebBlazor`.
   - Maintain Responsive/Mobile-first layout and Light/Dark theme compatibility.

Please analyze this draft, provide your architectural proposal, and wait for confirmation before generating files or migrations.