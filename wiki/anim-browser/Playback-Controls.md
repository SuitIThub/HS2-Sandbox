# Anim Browser — playback controls

Open **Controls** from top bar. Shows content when selected character(s) have a loaded animation.

![Docked Controls pane with speed, scrub, and loop toggles](images/anim-browser/ab-09-docked-controls.png)

> **Group H → Cowgirl**, **Fast Loop** on **Luna Clark**, **Seraphina Clark**, and **John**: **Controls** docked beside the main window — **Time** ~0.16 s / 1.33 s, **Speed** 1.00×, **Motion** 0.99, per-character **Height** / **Breast** sliders, **Show items**, **Force loop**, **Restart animation**.

![Float Controls window and scrub playback](images/anim-browser/ab-10-floating-controls.gif)

> Undocked **Controls** with the main Anim Browser closed: **Fast Loop** at **2.13×** speed, timeline scrubbed (~0.07 s / 1.35 s) — cowgirl pose updates live in the viewport.

## Per-group controls

Each control block corresponds to one **animation group** (characters sharing the same loaded clip, optionally merged when **Group controls by proximity** is on).

| Control | Effect |
|---------|--------|
| **Metadata** | Catalog path, ID, clip name, source, sideloader asset path when available |
| **Length** | Clip duration; shows effective duration at current speed when speed ≠ 1× |
| **Speed** | Slider 0–3×; **0 = paused** (same as pause button). Values at or below **0.01×** are ignored (Studio breaks below that) |
| **Pause / Play** | Freeze / resume at current scrub position |
| **Time** | Scrub normalized timeline; while paused, position is held every frame (no flicker) |
| **Motion** | Pattern slider — only when the loaded animation exposes pattern control |
| **Extra 1 / Extra 2** | Per-character optional sliders when the clip exposes `animeOptionParam1/2` |
| **Show items** | Toggle animation accessory visibility (`animeOptionVisible`) |
| **Force loop** | Repeat one-shot clips |
| **Restart animation** | Replay this group's clip from the start |
| **Restart all in scene** | Replay every character's animation in the scene |

## Docked vs floating

| Mode | Behavior |
|------|----------|
| **Docked** | Beside main window; closes with Anim Browser |
| **Floating** | Independent resizable window — usable when main window is closed (`ShouldDrawWhileHidden`) |

Hotkey: **Toggle undocked controls** (Configuration Manager → Anim Browser · Keyboard shortcuts).

Preferred mode (docked vs floating) is remembered in `anim_browser_options.json`.

## Group by proximity

**Group controls by proximity** (Options): characters in the same animation group share one control box only if they are within **3.5** world units. Otherwise each character gets a separate block.

---

**Navigation:** [← Applying animations](Applying-Animations) · [Anim Browser](Anim-Browser) · [Next: Grouping →](Grouping)
