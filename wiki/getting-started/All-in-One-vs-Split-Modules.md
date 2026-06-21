# All-in-one vs split modules

HS2-Sandbox can be installed in two ways — **but not both at once** for the same feature.

## All-in-one: `HS2SandboxPlugin.dll`

| Property | Detail |
|----------|--------|
| **GUID** | `com.hs2.sandbox` |
| **Game** | **HS2** only |
| **Includes** | CopyScript, Timeline, Son Scale, Notebook, Pose Browser, SearchBarManager, Workspace Tree Lock |
| **Does not include** | Anim Browser (split module only) |
| **Status** | Legacy; CI no longer actively releases it (`releaseDetect: false`) |

**Pros:** One DLL, all HS2 features without managing multiple files.

**Cons:** No Anim Browser; updates only as a bundle; higher risk of duplicate installation.

## Split modules: `HS2Sandbox.*.dll`

Each feature is its **own BepInEx plugin** with its own GUID and icon.

| Pros | Cons |
|------|------|
| Install only what you need | More files (DLL + icon per module) |
| Independent updates per module | Multiple sidebar buttons |
| Anim Browser available | Slightly more setup |

## Rule: no duplicates

**Never load at the same time:**

```
❌ HS2SandboxPlugin.dll  +  HS2Sandbox.PoseBrowser.dll
❌ HS2SandboxPlugin.dll  +  HS2Sandbox.SonScale.dll
❌ HS2SandboxPlugin.dll  +  HS2Sandbox.Timeline.dll
   … (any feature already included in all-in-one)
```

**Symptoms when duplicated:**

- Duplicate windows / sidebar toggles
- Actions run twice
- Harmony patches applied twice (Son Scale, Workspace Tree Lock)
- Unpredictable errors in `LogOutput.log`

## Exception: SearchBarManager

SearchBarManager has **no** sidebar button. In theory you can add it alongside other split modules — **but not** alongside all-in-one (SearchBarManager is already built in there).

## Recommendation

| Situation | Recommendation |
|-----------|----------------|
| New setup, all HS2 features + Anim Browser | **Split modules** |
| Pose Browser only in KKS/KK | Matching `KKSSandbox.*` / `KKSandbox.*` DLL |
| Existing all-in-one setup | Keep all-in-one **or** migrate fully to split (remove old DLL!) |

## Migrating all-in-one → split

1. Close Studio.
2. Remove **`HS2SandboxPlugin.dll`** from `BepInEx/plugins/`.
3. Install desired **`HS2Sandbox.*.dll`** files + icons.
4. Config under `BepInEx/config/com.hs2.sandbox/` stays mostly compatible.
5. Launch Studio and check the log.

→ [Installation](Installation) · [Supported games](Supported-Games-and-Modules)
