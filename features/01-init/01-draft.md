@AGENTS.md

Hi OpenCode! We are ready to verify our database connection to Aiven PostgreSQL by creating a simple Landing / Health Check page in our Blazor WebAssembly frontend (`HabitsApp.WebBlazor`) and Minimal API (`HabitsApp.Api`).

### Goal
Build a lightweight landing page with a brand status indicator that checks and displays the connection status to our PostgreSQL database.

### Requirements & Architecture Alignment

1. **Backend Endpoint (`HabitsApp.Api`)**:
   - Create a health check / status endpoint (e.g., `GET /api/status` or `GET /api/health`).
   - Use `ApplicationDbContext` (or `EF Core` DB check) to attempt a query/ping to Aiven PostgreSQL.
   - Return a JSON response containing:
     - `IsConnected`: `bool`
     - `DatabaseName`: `string`
     - `TimestampUtc`: `DateTime`

2. **Frontend UI (`HabitsApp.WebBlazor`)**:
   - Create a clean landing view or update `Home.razor`.
   - Include a brand banner/hero for **HabitsApp**.
   - Display a status badge/indicator:
     - 🟢 **Connected to Aiven PostgreSQL** (Green badge) if `IsConnected == true`.
     - 🔴 **Database Disconnected** (Red badge with retry button) if the connection fails or throws an exception.
   - Must adhere to the global UI rules: **Mobile-First/Responsive** design and support for both **Light & Dark themes** natively.
   - If the component logic exceeds 50 lines, separate it into a `Home.razor.cs` partial class.

3. **Security Check**:
   - Ensure NO real credentials or connection strings are hardcoded in source code or committed files. Use `appsettings.json` template or environment variables.

Please review this draft, suggest any necessary refinements, and provide the implementation plan before generating or running commands.