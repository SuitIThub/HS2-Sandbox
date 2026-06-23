# Architecture

## Monorepo layout

```
HS2-Sandbox/
├── src/                    # Shared implementation
│   ├── Core/               # GUI shell, toolbar, character priority, wiki hooks
│   ├── PoseBrowser/
│   ├── AnimBrowser/
│   ├── Timeline/
│   ├── CopyScript/
│   ├── Notebook/
│   ├── SonScale/
│   ├── SearchBarManager/
│   └── WorkspaceTreeLock/
├── targets/
│   ├── HS2/<Module>/       # Thin Plugin.cs + .csproj
│   ├── KKS/PoseBrowser|AnimBrowser/
│   └── KK/PoseBrowser|AnimBrowser/
├── assets/                 # Toolbar PNGs
└── wiki/                   # GitHub Wiki sources (this wiki)
```

## Build pattern

Each `targets/.../*.csproj`:

1. Sets `EnableDefaultCompileItems=false`
2. Includes `$(SrcRoot)Core/**` + module-specific `src/<Module>/**`
3. Compiles a thin `Plugin.cs` as the BepInEx entry point

**Game define:** `HS2`, `KKS`, or `KK` from `targets/<Game>/Directory.Build.props`

| Game | TFM | API package |
|------|-----|-------------|
| HS2 | net472 | IllusionModdingAPI.HS2API |
| KKS | net472 | KKSAPI |
| KK | net35 | KKAPI + Net35Compat |

## Runtime: plugin bootstrap

Typical pattern (window modules):

```csharp
// Awake
SandboxServices.Initialize(Logger, Config);
ModuleConfig.Register(Config);

// Start
var gui = gameObject.AddComponent<SandboxGUI>();
gui.RegisterWindow(SandboxWindowKeys.X, AddComponent<XWindow>(), initialVisible: false);
_icon = ToolbarIconLoader.LoadPng("x-icon.png");
_toolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(_icon, v => gui.SetXVisible(v));
```

### UI stack

```
BepInEx Plugin (Awake/Start)
    └── SandboxGUI (OnGUI loop)
            └── SubWindow derivatives (DrawWindow)
                    └── IMGUI layouts (virtualized, cached)
```

**Performance rule:** Draw code must not scale with library size every frame — virtualization and caches (see project rules).

## Harmony modules

| Module | Harmony ID | Target |
|--------|------------|--------|
| Son Scale + BP | `com.hs2.sandbox.sonscale.betterpenetration` | `DanAgent.SetDanTarget` |
| Workspace Tree Lock | `com.hs2.sandbox.workspacetreelock` | `TreeNodeObject` visibility |
| Heelz Control | `com.hs2.sandbox.posebrowser.heelzcontrol` | HS2Heelz interop |

## Shared Core (`src/Core/`)

| Component | Role |
|-----------|------|
| `SandboxGUI` | Window registry, visibility, draw loop |
| `SubWindow` | Base for draggable IMGUI windows |
| `SandboxServices` | Logger + ConfigFile |
| `ToolbarIconLoader` | Toolbar PNG — file next to the DLL first, then embedded resource |
| `StudioCharacterSelection` | Cached Studio selection |
| `CharacterPriority/` | Priority lists for multi-character apply |
| `PoseBrowserWikiRegistration` | HS2Wiki pages (reflection) |
| `AnimBrowserWikiRegistration` | HS2Wiki pages (reflection) |

## Persistence philosophy

- **No JsonUtility** for new save formats
- Hand-written JSON (`StringBuilder` + escape) or **TSV** for tabular data
- Config under **`BepInEx/config/com.hs2.sandbox/`** (same folder name for all games)

→ [Configuration & data files](Configuration-and-Data-Files) · [Building from source](Building-from-Source)
