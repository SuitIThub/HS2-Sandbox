# Plugin compatibility

HS2-Sandbox integrates with other BepInEx plugins where useful. Integrations are **soft dependencies** — Sandbox loads without them; extra UI appears when detected.

## HS2Wiki

| | |
|---|---|
| **Repo** | [HS2Wiki](https://github.com/SuitIThub/HS2Wiki) |
| **Purpose** | In-game wiki overlay (default **F3**) |
| **Sandbox integration** | Pose Browser & Anim Browser register help pages via reflection |

Registration files:

- `src/Core/PoseBrowserWikiRegistration.cs`
- `src/Core/AnimBrowserWikiRegistration.cs`

No compile-time reference to HS2Wiki. Log line on success: *"Registered … pages with HS2Wiki"*.

If pages are missing after installing HS2Wiki, restart Studio.

## Studio Better Penetration

| | |
|---|---|
| **Used by** | Son Scale |
| **GUID** | `com.animal42069.studiobetterpenetration` (soft) |
| **Behavior** | Harmony patch on `DanAgent.SetDanTarget` for length/overall scaling |

Without BP: legacy chain scaling (localPosition / axis + root XY girth).

With BP: length/overall via `m_baseDanLength`; girth once on dan root for uniform thickness.

## HS2Heelz (HS2 Pose Browser only)

| | |
|---|---|
| **Used by** | Pose Browser → Heelz Control |
| **Features** | Tag rules (heels on/off) when poses load; per-character overrides |
| **UI** | Options → Plugin compatibility; Heelz Control window (top bar) |
| **Harmony** | `com.hs2.sandbox.posebrowser.heelzcontrol` |

Configure comma-separated tags in Options → **Heelz OFF tags** / **Heelz ON tags**.

## HS2PE / KKPE (PE compat)

| | |
|---|---|
| **Used by** | Pose Browser (**HS2**, **KKS**, **KK**) |
| **Service** | `PePoseCompatService` |
| **Feature** | Embed Advanced Mode breast/butt gravity & force in pose files (ExtendedSave block) |
| **UI** | Options → Plugin compatibility → toggle when the game’s PE plugin is detected |

| Game | Plugin | BepInEx GUID |
|------|--------|--------------|
| **HS2** | **HS2PE** | `com.joan6694.illusionplugins.poseeditor` |
| **KK**, **KKS** | **KKPE** | `com.joan6694.kkplugins.kkpe` |

Options and Configuration Manager labels use **HS2PE** or **KKPE** depending on the build. No compile-time dependency — the block is written only when the matching plugin is installed and the toggle is on.

## XUnity Auto Translator

| | |
|---|---|
| **Used by** | Anim Browser (optional) |
| **Config** | BepInEx → Anim Browser → Auto translate |
| **Behavior** | Translates animation display names via `StudioAutoTranslation` |

## Timeline external plugins

Many Timeline commands call into **other plugins** (VNGE, FashionLine, screenshot tools, etc.). Those commands expect **modified builds** with extra hooks — not shipped in this repository.

See [Timeline](Timeline) and [Timeline commands](Timeline-Commands-Reference).

## CopyScript server

CopyScript module requires a **local HTTP CopyScript API** — not a BepInEx plugin in the game folder, but a separate service on your PC.

→ [CopyScript](CopyScript) · [Installation](Installation)
