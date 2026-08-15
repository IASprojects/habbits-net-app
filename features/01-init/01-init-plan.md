# Implementation Plan: Database Connection Health Check with Telemetry

## Objective
Verify PostgreSQL database connection via a landing/health check page in `HabitsApp.WebBlazor` (frontend) and `HabitsApp.Api` (backend), with basic telemetry logging.

---

## Phase 1: Backend (`HabitsApp.Api`)

### Health Endpoint
- **Location**: `Program.cs` (Minimal API) 
- **Route**: `GET /api/health`
- **Response**:
  ```json
  {
    "IsConnected": bool,
    "DatabaseName": string,
    "TimestampUtc": "ISO8601",
    "LatencyMs": number
  }
  ```

### Database Check + Telemetry
1. **AppSettings Configuration**:
   ```json
   "Telemetry": {
     "LogPath": "Logs/db-health-{Date}.log",
     "LogLevel": "Information",
     "SamplingRate": 1.0
   }
   ```
2. **Service Layer (HabitsApp.Application)**:
   - Create `IDatabaseHealthService` interface
   - Implement `DatabaseHealthService` with:
     - Constructor injection of `ApplicationDbContext`
     - `CheckDatabaseHealthAsync()` method executing `SELECT 1`
3. **API Endpoint (HabitsApp.Api)**:
   - Inject `IDatabaseHealthService`
   - Call service method and format response
4. Log to configured path with settings from appsettings:
   ```log
   [TIMESTAMP] [LEVEL]: PostgreSQL ping succeeded/failed (Latency: Xms)
   ```
5. Catch/log exceptions (timeouts, auth errors)

### Security
- Credentials from `appsettings.json`/env vars only
- Never log credentials

---

## Phase 2: Frontend (`HabitsApp.WebBlazor`)

### Landing Page (`Home.razor`)
1. **UI Components**:
   - Hero banner ("HabitsApp")
   - Status badge (🟢/🔴)
   - Retry button
2. **Telemetry**:
   - Console log API response times
   - Anonymous session ID for errors

### UI Compliance
- Mobile-first (Tailwind/MudBlazor)
- Light/Dark theme support
- 50-line rule enforced

---

## Phase 3: Testing

### Backend
- Unit tests: Mock `DbContext` scenarios
- Verify log file creation

### Frontend  
- Test UI states (connected/disconnected)
- Verify console telemetry

---

## Approval Request
Confirm with:

```bash
dotnet run --project src/HabitsApp.Presentation/HabitsApp.Api
```