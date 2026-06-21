# Troubleshooting

## General

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Duplicate windows / double actions | All-in-one **and** split module for same feature | Remove duplicate DLL — see [All-in-one vs split](All-in-One-vs-Split-Modules) |
| No sidebar icon | Missing PNG next to DLL | Copy `*-icon.png` beside the plugin DLL |
| Plugin not in log at all | Wrong game DLL or BepInEx not installed | Check `BepInEx/LogOutput.log`; verify BepInEx 5 |
| Build restore fails | Missing `nuget.config` or IllusionLibs access | Restore from repo root; check NuGet feeds |

## CopyScript

| Symptom | Fix |
|---------|-----|
| Always "offline" | Start CopyScript server; check host/port; firewall |
| Wrong files tracked | Verify source/destination paths in CopyScript window |

## Timeline

| Symptom | Fix |
|---------|-----|
| Step does nothing | Target plugin missing or not the modified build Timeline expects |
| Variables not saved | Check `timeline.json` / `persistent_vars.json` permissions |
| VNGE commands fail | IronPython/VNGE paths; modified VNGE build required |

## SearchBarManager

| Symptom | Fix |
|---------|-----|
| No search bar on panel | Studio UI path changed; add path under **Additional Parent Paths** |
| Bar on wrong panel | Remove incorrect paths from config |

## Son Scale

| Symptom | Fix |
|---------|-----|
| Sliders do nothing | Select a character; enable Son scale; wrong object selected |
| BP length silent | BP missing or API changed — search log for `Son scale:` |

## Workspace Tree Lock

| Symptom | Fix |
|---------|-----|
| Pin does not stick | Use **middle-click** on nested row; game update may need Harmony fix |
| Odd visibility after update | `TreeNodeObject` internals changed — report issue |

## Notebook

| Symptom | Fix |
|---------|-----|
| Notes lost | Check `BepInEx/config/com.hs2.sandbox/notebook.json`; disk permissions |
| Save delay | Normal — debounced ~0.5s; flushed on close/quit |

## Pose Browser

| Symptom | Fix |
|---------|-----|
| Empty grid | Check folder mode (All poses / folder / Favorites); refresh ↻; relax filters |
| Pose won't apply | Select characters in Studio; click thumbnail |
| Multi-apply wrong pairing | Set Male/Female tags; configure **Chars** lists |
| ZIP import fails | Pack must use **stored** (uncompressed) ZIP — see [Pose ZIP format](Pose-ZIP-Format) |
| Tags lost | Back up `pose_tags.tsv`; don't edit TSV while game runs |
| Wiki pages missing | Install HS2Wiki; restart Studio |
| Stash closes with browser | Use **Float** or undocked stash hotkey for persistent window |

Full Pose Browser guide: [Pose Browser](Pose-Browser)

## Anim Browser

| Symptom | Fix |
|---------|-----|
| Click does nothing | Select a **character** in Studio (not prop/accessory) |
| Empty list | Clear search; check **Hide non-Studio lists** in Options; press ↻ |
| Merge button greyed | Hover for reason; cross-group merge needs group merge first |
| Controls vanish when window closed | Use floating controls or hotkey |
| Preview missing (KKS/KK) | Hover preview is **HS2 only** |

Full Anim Browser guide: [Anim Browser](Anim-Browser)

## Log file

Always check:

```
<GameRoot>/BepInEx/LogOutput.log
```

Search for your plugin GUID or module name (e.g. `com.hs2.sandbox.posebrowser`).

→ [Installation](Installation) · [Requirements](Requirements)
