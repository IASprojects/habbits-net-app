# Implementation Steps — Desktop Navigation: Bottom Nav Always Visible

Related discussion: the bottom navigation (`BottomNav`) currently only renders on mobile. The CSS hides it on screens `>= 641px`, so on tablet/desktop there is no navigation besides the top app bar.

Approved decisions:
- **Keep the floating bottom nav visible at all screen sizes** (remove the `display: none` at `>= 641px`).
- **Desktop items show icon + label**; mobile keeps the compact icon-only pill.
- Labels are hidden on mobile via CSS and shown on `>= 641px`.

---

## 1. `Layout/BottomNav.razor`

- [ ] Add a text label next to each Material icon in every `NavLink`:
  ```html
  <span class="material-symbols-outlined">home_app_logo</span>
  <span class="bottom-nav__label">Home</span>
  ```
- [ ] Apply to all four items: Home, Habits, Calendar, Settings.

## 2. `wwwroot/css/app.css`

- [ ] Remove the hide-on-desktop block (currently around line 1165):
  ```css
  /* DELETE */
  @media (min-width: 641px) {
      .bottom-nav { display: none; }
  }
  ```
- [ ] Add `.bottom-nav__label`:
  - Hidden by default: `display: none`.
  - Shown on `@media (min-width: 641px)` with `font-size: var(--label-caps-size)`, color `var(--color-on-surface-variant)`.
- [ ] In `@media (min-width: 641px)`, restyle `.bottom-nav__item` as a vertical stack:
  - `flex-direction: column`, `gap: 2px`, `width: 64px`, `height: auto`, `padding: 6px 8px`, `border-radius: var(--radius-md)`.
- [ ] (Optional) Widen `.bottom-nav` container so the four labeled items fit comfortably (currently `max-width: 28rem`; consider `auto` or a larger cap) while keeping it centered.

## 3. `Layout/MainLayout.razor.css`

- [ ] Give the content bottom clearance on desktop so the now-always-visible fixed nav does not overlap content (currently padding-bottom is only applied below 641px):
  ```css
  @media (min-width: 641px) {
      .container-page { padding-bottom: calc(var(--space-2xl) + 96px); }
  }
  ```

## 4. Verification

- [ ] `dotnet build`
- [ ] Manual smoke check:
  - Mobile (< 641px): compact icon-only pill, no labels, content clears the nav.
  - Desktop/tablet (>= 641px): pill visible with icon + label per item, content clears the nav, active item still highlighted.