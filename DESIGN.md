# Design System Strategy: The Architectural Precision

## 1. Overview & Creative North Star
**The Creative North Star: "The Orchestrated Workspace"**

This design system moves away from the "utility-app-as-a-spreadsheet" trope. Instead, it treats data-heavy environments with the reverence of a high-end architectural blueprint. While the requirements demand a functional, high-density Windows desktop utility, our execution will focus on **Architectural Precision**. 

We achieve this by breaking the rigid, "boxed-in" grid prevalent in legacy software. By utilizing intentional asymmetry, tonal layering instead of borders, and sophisticated editorial-grade typography, we transform a utility tool into a high-performance workspace that feels expansive yet authoritative. We are not just building a tool; we are building a professional environment.

---

## 2. Colors: Tonal Depth & The "No-Line" Rule
The palette is rooted in a sophisticated range of architectural grays and a high-performance primary blue (`#004f96`). 

### The "No-Line" Rule
**Explicit Instruction:** Designers are prohibited from using 1px solid borders to section off major UI areas. Boundaries must be defined solely through background color shifts. For example, a `surface-container-low` side panel sitting against a `surface` main content area creates a natural, sophisticated break without the visual "noise" of a line.

### Surface Hierarchy & Nesting
Treat the UI as a series of physical layers. Use the `surface-container` tiers to create "nested" depth:
- **Base Layer:** `surface` (#f8f9fb) for the primary application background.
- **Structural Shifts:** `surface-container-low` (#f3f4f6) for global navigation or sidebars.
- **Interactive Planes:** `surface-container-lowest` (#ffffff) for primary content cards or data tables to make them "pop" against the gray base.
- **Detail Tiers:** `surface-container-high` (#e7e8ea) for utility panels or inset search bars.

### The Glass & Gradient Rule
To move beyond a "standard" feel, use **Glassmorphism** for floating elements like context menus or tooltips. Utilize `surface-container-lowest` at 85% opacity with a `20px` backdrop blur. 
**Signature Textures:** Apply a subtle linear gradient to primary CTAs, transitioning from `primary` (#004f96) to `primary_container` (#0067c0) at a 135-degree angle. This provides a "soul" and depth that flat hex codes cannot achieve.

---

## 3. Typography: The Editorial Scale
We use **Inter** not just for legibility, but as a structural element. 

- **Display & Headline:** Use `headline-lg` and `headline-md` with tight tracking (-0.02em) to create an authoritative, editorial feel for dashboard overviews.
- **Data Densities:** For the core utility experience, `body-md` (0.875rem) is your workhorse. Pair it with `label-md` for metadata to create a clear hierarchy within dense tables.
- **Hierarchy via Tonal Weight:** Instead of just varying font sizes, use color. Use `on-surface` (#191c1e) for primary data and `on-surface-variant` (#414752) for secondary labels to create depth without increasing font size.

---

## 4. Elevation & Depth: Tonal Layering
Traditional shadows are often a crutch for poor layout. In this system, depth is earned through tone.

- **The Layering Principle:** Place a `surface-container-lowest` card on a `surface-container-low` section. The slight delta in brightness creates a soft, natural lift.
- **Ambient Shadows:** For floating modals, use an extra-diffused shadow: `box-shadow: 0 12px 40px rgba(25, 28, 30, 0.06);`. The shadow color is a low-opacity version of `on-surface`, mimicking natural light.
- **The "Ghost Border" Fallback:** If a boundary is required for accessibility in high-density forms, use a "Ghost Border": the `outline-variant` token at 20% opacity. **Forbid 100% opaque borders.**

---

## 5. Components: Style & Execution

### Primary & Secondary Buttons
*   **Primary:** Gradient from `primary` to `primary_container`. Corner radius: `md` (0.375rem).
*   **Secondary:** `surface-container-high` background with `on-secondary-container` text. No border.
*   **Interaction:** On hover, the primary button should shift 1px upward with a subtle ambient shadow.

### Data Tables (High Density)
*   **Structure:** Forbid divider lines between rows. Use a subtle background shift (`surface-container-low`) on `:hover` to indicate row selection.
*   **Header:** Use `label-sm` in all-caps with `0.05em` letter spacing for an "architectural" header look.
*   **Cell Spacing:** High density requires tight vertical padding (8px) but generous horizontal padding (16px) to maintain readability.

### Form Inputs
*   **Field Style:** Use `surface-container-highest` for the input track. No bottom line or full border. 
*   **States:** On focus, the `outline` color (#717783) appears as a 2px "inner" glow rather than an outer stroke.

### Status Badges
*   Do not use heavy, solid-colored blocks. Use a "Soft Fill" approach: `error_container` background with `on_error_container` text. This ensures the UI remains professional and doesn't look like a warning light dashboard.

### Navigation Sidebar
*   Use `surface-container-low`. Active states should not use a box; use a "pill" indicator that uses the `primary_fixed` color with `on_primary_fixed` text, providing a clear but soft focus.

---

## 6. Do’s and Don’ts

### Do
*   **Do** use white space as a structural tool. If elements feel cluttered, increase the gap before you add a line.
*   **Do** use `9999px` (full) roundedness for status chips and toggle switches to contrast with the `md` (0.375rem) radius of structural cards.
*   **Do** prioritize `surface-container-lowest` for areas where the user needs to input or edit data.

### Don't
*   **Don't** use pure black (#000000) for text. Always use `on-surface`.
*   **Don't** use standard 1px borders to separate table columns. Use alignment and negative space.
*   **Don't** use "Drop Shadows" on buttons; use tonal shifts or 4% opacity ambient glows.
*   **Don't** ever mix roundedness scales within a single component (e.g., a square button inside a rounded card). Use the scale tokens strictly.

---

## 7. Technical Tokens

### Color Palette Reference

| Token Name | Hex Value |
| :--- | :--- |
| `background` | `#f8f9fb` |
| `surface` | `#f8f9fb` |
| `surface_container_low` | `#f3f4f6` |
| `surface_container` | `#edeef0` |
| `surface_container_high` | `#e7e8ea` |
| `surface_container_highest` | `#e1e2e4` |
| `surface_container_lowest` | `#ffffff` |
| `primary` | `#004f96` |
| `primary_container` | `#0067c0` |
| `on_primary` | `#ffffff` |
| `on_primary_container` | `#dbe7ff` |
| `secondary` | `#52606e` |
| `secondary_container` | `#d5e4f6` |
| `on_surface` | `#191c1e` |
| `on_surface_variant` | `#414752` |
| `outline` | `#717783` |
| `outline_variant` | `#c1c6d4` |

### Typography Options

*   **Font Family:** `INTER`
*   **Headline Font:** `INTER`
*   **Body Font:** `INTER`
*   **Label Font:** `INTER`

### Structural
*   **Border Radius Base:** `ROUND_FOUR`
