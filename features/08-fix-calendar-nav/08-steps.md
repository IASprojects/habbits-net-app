# Implementation Steps — Fix Calendar Navigation (404 in production)

## Problem

The Blazor WASM app is deployed to **GitHub Pages under the subpath `/habbits-net-app/`**. During deploy,
`deploy-stage.yml` rewrites `<base href="/" />` to `<base href="/habbits-net-app/" />`. With that base, any
navigation that uses an **absolute** path (leading `/`) resolves against the domain root
(`https://<user>.github.io/calendar`) instead of the app subpath, producing a **404**.

Commit `103eb59` (`fix: Update navigation links to use relative paths`) switched Home / Habits / Settings /
Login / Register to relative paths, but the **Calendar** link was added later with an absolute path and was
missed.

## Approved decisions

- Change all in-app `NavLink` / `NavigateTo` targets to **relative** paths (no leading `/`) so they resolve
  correctly under the GitHub Pages base.
- The Calendar `NavLink` is the reported bug; fix it to `href="calendar"`.
- Audit other absolute navigation calls that still point to a leading `/` and break under the subpath.

---

## 1. Fix the Calendar `NavLink`

File: `src/HabitsApp.Presentation/HabitsApp.WebBlazor/Layout/BottomNav.razor`

- [ ] Change line 10:

  ```diff
  - <NavLink class="bottom-nav__item" href="/calendar" title="Calendar">
  + <NavLink class="bottom-nav__item" href="calendar" title="Calendar">
  ```

## 2. Audit remaining absolute navigation under the subpath

- [ ] Grep the WebBlazor project for leading-slash navigation targets:
  - `NavLink ... href="/`
  - `Navigation.NavigateTo("/`
- [ ] Fix any remaining absolute paths the same way (relative, no leading `/`).
- [ ] Confirm `Settings` target: there is **no** `Pages/Settings.razor` with `@page "/settings"`, so the
      `href="settings"` link still renders `NotFound.razor`. Decide here whether to add a Settings page (new
      feature) or stub a placeholder. **Out of scope for this fix unless explicitly requested.**

## 3. Verification

- [ ] `dotnet build HabitsApp.slnx -c Release`
- [ ] Local smoke test (`dotnet run --project src/HabitsApp.Presentation/HabitsApp.WebBlazor`):
  - Login → click **Calendar** in the bottom nav → page renders (no 404).
  - Verify the URL is `<app>/calendar` relative to the base, not rooted at the domain.
- [ ] Publish preview to confirm base-href behavior:
  - `dotnet publish src/HabitsApp.Presentation/HabitsApp.WebBlazor/HabitsApp.WebBlazor.csproj -c Release -o publish`
  - Inspect `publish/wwwroot/index.html` for `<base href="/habbits-net-app/" />`.
