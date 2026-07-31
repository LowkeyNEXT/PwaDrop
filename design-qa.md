# PWADrop design QA

## Evidence

- Source visual truth: `/Users/brenrid/.codex/generated_images/019fae1c-ba14-74a2-8c35-1a397abec051/exec-9dd2a048-0784-4cf0-a1b5-402cd94ea1dc.png`
- Normalized source crop: `artifacts/ci/design-qa/reference-window.png`
- Rendered implementation: `docs/images/PWADrop-settings.png`
- Side-by-side comparison input: `artifacts/ci/design-qa/compare-final-30593442057.png`
- Windows CI evidence: run `30593442057`, artifact `PWADrop-settings-preview`
- Viewport/state: 1034 × 782 physical pixels, Windows 11 dark theme, Overview selected, bridge enabled, startup disabled, notifications enabled
- Density normalization: source application window was cropped from the 1488 × 1058 mock to 1034 × 782; the native implementation was rendered at 1034 × 782 with the CI runner at 1× capture density. No resampling was applied to either comparison half.

## Full-view comparison

The final side-by-side input shows the same major-region proportions: 258 px navigation pane, 54 px title bar, 280 px overview hero, three setting rows, and the open lower region. Typography, row baselines, dividers, toggles, navigation selection, brand palette, and requested copy align without an actionable P0, P1, or P2 mismatch.

## Focused-region comparison

A separate crop was not needed because the equal-size 2068 × 782 side-by-side input keeps the navigation labels, hero copy, setting descriptions, Fluent icons, and toggle states readable at original pixel density. The bridge asset was also opened independently at native resolution to verify its alpha edge and color treatment.

## Required fidelity surfaces

- Fonts and typography: both use Segoe UI Variable Display/Text. Display size, setting-title weight, line wrapping, and hierarchy align; no clipping or truncation is visible.
- Spacing and layout rhythm: navigation, hero, dividers, row heights, text insets, icon columns, and toggle alignment match the selected composition.
- Colors and visual tokens: midnight surfaces, blue-violet brand treatment, white/secondary text, green active state, and subdued strokes are consistent. CI `DrawToBitmap` captures the solid dark fallback rather than the live DWM backdrop.
- Image quality and asset fidelity: the embedded high-resolution bridge bitmap has a validated alpha channel and is rendered without stretching or a chroma fringe. The application and package marks use the existing source-of-truth brand asset.
- Copy and content: the product name is exactly `PWADrop`; all overview copy is source-neutral; the two removed reassurance blocks do not appear.

## Comparison history

1. Initial Windows capture — blocked
   - Finding: the off-screen form rendered only its background, so there was no valid implementation evidence.
   - Fix: show and lay out the native form off-screen before `DrawToBitmap`.
   - Post-fix evidence: CI run `30592818383` produced the complete window and controls.
2. First visible comparison — blocked
   - P1: transparent toggle controls captured as black rectangles.
   - P2: window commands appeared in reverse order, the square app mark replaced the selected bridge hero, and the percentage-based hero pushed setting rows too low.
   - Fixes: paint toggle backgrounds with the canvas token, correct the command docking order, embed a dedicated bridge asset, and use measured absolute hero/row tracks.
   - Post-fix evidence: CI run `30593006705` removed the rectangles and corrected the chrome and hero asset.
3. Density comparison — blocked
   - P2: navigation and setting typography were too small, rows lacked the selected horizontal inset, the bridge had excess transparent padding, and generic component/diagnostic icons differed from the reference.
   - Fixes: match Segoe sizes and weights, add measured content padding, crop the generated alpha asset to its visual bounds, and use the official Fluent `Puzzle`, `Health`, and `Completed` glyphs.
   - Post-fix evidence: CI runs `30593188623` and `30593330103` align the layout and icon language.
4. Copy comparison — blocked
   - P2: the notification description did not match the approved source-neutral sentence.
   - Fix: use `Get notified when the bridge is active or has issues.` verbatim.
   - Post-fix evidence: CI run `30593442057` shows the corrected final copy.

## Findings

No actionable P0, P1, or P2 findings remain.

## Follow-up polish

- P3: the generated bridge asset has slightly stronger dimensional shading than the flatter reference illustration.
- P3: the official Fluent completed glyph is an outlined status circle; the concept image depicts a filled green circle.
- P3: the CI bitmap shows the solid dark fallback rather than the wallpaper tint that DWM can provide in the live window.

## Implementation checklist

- [x] Exact `PWADrop` display name
- [x] Source-neutral overview copy
- [x] Removed bottom reassurance blocks
- [x] Functional navigation and keyboard-focusable toggles
- [x] Correct minimize, maximize/restore, and close-to-notification-area behavior
- [x] Equal-size native screenshot comparison
- [x] Windows build, unit tests, and primed original-drag self-test

final result: passed
