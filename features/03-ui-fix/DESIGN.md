---
name: Obsidian & Emerald
colors:
  surface: '#091422'
  surface-dim: '#091422'
  surface-bright: '#303a49'
  surface-container-lowest: '#050e1c'
  surface-container-low: '#121c2a'
  surface-container: '#16202f'
  surface-container-high: '#212a39'
  surface-container-highest: '#2b3545'
  on-surface: '#d9e3f7'
  on-surface-variant: '#bbcabf'
  inverse-surface: '#d9e3f7'
  inverse-on-surface: '#273140'
  outline: '#86948a'
  outline-variant: '#3c4a42'
  surface-tint: '#4edea3'
  primary: '#4edea3'
  on-primary: '#003824'
  primary-container: '#10b981'
  on-primary-container: '#00422b'
  inverse-primary: '#006c49'
  secondary: '#c0c1ff'
  on-secondary: '#1000a9'
  secondary-container: '#3131c0'
  on-secondary-container: '#b0b2ff'
  tertiary: '#ffb95f'
  on-tertiary: '#472a00'
  tertiary-container: '#e29100'
  on-tertiary-container: '#523200'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#6ffbbe'
  primary-fixed-dim: '#4edea3'
  on-primary-fixed: '#002113'
  on-primary-fixed-variant: '#005236'
  secondary-fixed: '#e1e0ff'
  secondary-fixed-dim: '#c0c1ff'
  on-secondary-fixed: '#07006c'
  on-secondary-fixed-variant: '#2f2ebe'
  tertiary-fixed: '#ffddb8'
  tertiary-fixed-dim: '#ffb95f'
  on-tertiary-fixed: '#2a1700'
  on-tertiary-fixed-variant: '#653e00'
  background: '#091422'
  on-background: '#d9e3f7'
  surface-variant: '#2b3545'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  display-lg-mobile:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  body-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '400'
    lineHeight: 28px
    letterSpacing: '0'
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
    letterSpacing: '0'
  label-caps:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.1em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  unit: 8px
  container-padding-mobile: 20px
  container-padding-desktop: 40px
  gutter: 16px
  stack-sm: 8px
  stack-md: 24px
  stack-lg: 48px
---

## Brand & Style

This design system targets high-achieving individuals seeking a focused, premium environment for personal growth. The brand personality is calm, sophisticated, and quietly authoritative—moving away from the "gamified" clutter of traditional habit trackers toward a focused, editorial experience.

The design style is **Minimalist Glassmorphism**. It utilizes a deep, atmospheric base to create a sense of infinite depth, while UI elements appear as semi-translucent "shards" of glass. This aesthetic prioritizes high-quality negative space and subtle light refraction to evoke a sense of digital luxury and mental clarity.

## Colors

The palette is anchored by **Emerald Charcoal (#06101E)**, a deep, desaturated navy-green that provides more warmth and depth than pure black. 

- **Primary (Emerald Green):** Used for "Success" states and health-related habits.
- **Secondary (Electric Indigo):** Used for cognitive or work-related habits.
- **Tertiary (Warm Amber):** Used for mindfulness or evening-routine habits.
- **Surfaces:** UI containers use a custom RGBA stack to achieve the frosted effect, allowing the background color to bleed through slightly.
- **Overlays:** Use a 10-20% white tint with a heavy (32px+) backdrop blur.

## Typography

The system utilizes **Inter** exclusively to maintain a functional, systematic appearance. The hierarchy is driven by significant variations in weight and letter spacing rather than just size.

- **Headlines:** Should be tightly tracked (-0.01em to -0.02em) to feel "locked-in" and premium.
- **Labels:** Use uppercase with increased tracking (0.1em) for category headers and small metadata to ensure legibility against the dark background.
- **Body:** Maintain a generous line height (1.5x) to prevent text from feeling cramped within glass containers.

## Layout & Spacing

The layout follows a **Fluid-Fixed Hybrid** model. On mobile, content is fluid with 20px side margins. On desktop, content is constrained to a 1200px max-width central column.

The spacing rhythm is based on an **8px linear scale**. 
- **Vertical Rhythm:** Use 48px (stack-lg) to separate major sections like "Today's Habits" and "Weekly Progress."
- **Internal Padding:** Cards should never have less than 24px of internal padding to maintain the "luxurious" feel of the negative space.

## Elevation & Depth

Depth is not communicated through traditional shadows, but through **Backdrop Blur** and **Inner Glows**.

1.  **Level 0 (Base):** The Emerald Charcoal background.
2.  **Level 1 (Cards):** 32px Backdrop Blur, 3% White fill, 1px solid border at 12% opacity.
3.  **Level 2 (Modals/Popovers):** 64px Backdrop Blur, 6% White fill, 1px solid border at 20% opacity.

A subtle "Top-Down" lighting effect is achieved by using a linear gradient for the border (Top-Left: 20% white to Bottom-Right: 0% white). Avoid drop shadows unless used to separate high-level floating buttons from the glass panes.

## Shapes

The shape language is consistently **Rounded**. The standard radius for all habit cards, input fields, and containers is **16px**. 

- **Small elements (Chips/Checkboxes):** 8px radius.
- **Buttons:** 12px radius or full-pill depending on the context of the action.
- **Consistency:** Avoid mixing sharp corners with rounded corners; all interactive surfaces must share the 16px corner language to feel part of the same premium ecosystem.

## Components

### Habits & Cards
Habit cards are the primary interface. They feature a glass background with a subtle "shimmer" border. The progress state is indicated by a glowing neon bar (Primary Green) that appears to sit *inside* the glass pane.

### Buttons
- **Primary:** Solid Emerald Green with white text. No glass effect.
- **Secondary:** Glass background with a 1px white border.
- **Action:** Circular buttons for "Add Habit" should use a subtle glow filter (`filter: drop-shadow(0 0 8px rgba(16, 185, 129, 0.4))`).

### Inputs
Input fields should be semi-transparent with a 1px bottom border that highlights to the accent color on focus. Text should be high-contrast white.

### Habit Identity Chips
Small, 8px rounded labels that use desaturated versions of the accent colors for categorization (e.g., "Health," "Mind," "Career"). Text within chips should use the `label-caps` typography style.

### Progress Rings
For the dashboard, use thin-stroke (2px) SVG circles with a neon glow to represent daily completion percentages.