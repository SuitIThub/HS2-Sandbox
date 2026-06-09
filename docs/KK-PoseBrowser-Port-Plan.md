# Plan: PoseBrowser-Modul nach Koikatsu (KK) portieren

> Status: Planung · Zielmodul: **PoseBrowser** (Cross-Game-Pilot) · Zielspiel: **Koikatsu (KK)**
> Grundlage: DLL-Analyse der installierten Spiele vom 2026-06-08.

---

## 1. Ausgangslage & Diagnose

### 1.1 Runtime-Fakten (verifiziert an den echten DLLs)

| | HS2 | KKS | **KK** |
|---|---|---|---|
| Unity | 2018.4 | 2019.4 | **5.6.2f1** |
| mscorlib ImageRuntime | 4.0.0.0 | 4.0.0.0 | **2.0.0.0** |
| Effektives Framework | net4x | net4x | **.NET 3.5 (Unity-Mono legacy)** |
| Studio-Prozess | `StudioNEOV2.exe` | `CharaStudio.exe` | `CharaStudio.exe` |
| KKAPI installiert | HS2API | KKSAPI | **KKAPI 1.40** |
| BepInEx | 5.4.x | 5.4.x | **5.4.23.1** / Harmony 2.9 |

KK läuft auf stock Unity-Mono mit .NET-3.5-Profil (bestätigt: `doorstop_config.ini` → `[UnityMono]`, keine `MonoBleedingEdge`, kein Runtime-Upgrade).

**Managed-Ordner (Quelle der Referenz-DLLs / Analyse):**

| Spiel | Managed-Pfad |
|---|---|
| HS2 | `D:\Honey Select\StudioNEOV2_Data\Managed` |
| KKS | `D:\Games\Koikatsu Sunshine EX BetterRepack R12\CharaStudio_Data\Managed` |
| KK | `D:\Games\Koikatsu BetterRepack RX21\CharaStudio_Data\Managed` |

### 1.2 Fehlende BCL-APIs in KK (in HS2/KKS alle vorhanden)

| API | In KK? | Seit | Im geteilten Code genutzt |
|---|---|---|---|
| `Array.Empty<T>()` | **Nein** | net4.6 | ~70–80 explizit + compiler-erzeugt |
| `string.IsNullOrWhiteSpace` | **Nein** | net4.0 | ~59× |
| `Enum.HasFlag` | **Nein** | net4.0 | vereinzelt |
| `IReadOnlyList<T>` | **Nein** | net4.5 | ~50 Signaturen |
| `IReadOnlyCollection<T>` | **Nein** | net4.5 | mehrere |
| `IReadOnlyDictionary<K,V>` | **Nein** | net4.5 | mehrere |

### 1.3 Warum `Array.Empty` bisher unlösbar war

- Kompiliert man gegen **net46/net472-Referenz-Assemblies**, ersetzt Roslyn **automatisch** jedes leere Array (`new T[0]`, leere `params`) durch `call System.Array::Empty<T>()`. Diese Aufrufe stehen nicht im Quelltext.
- Der Aufruf ist fest an `System.Array` aus **mscorlib** gebunden → auf KK `MissingMethodException`.
- **Ein C#-Polyfill kann compiler-erzeugte Aufrufe nicht abfangen.** Deshalb hat nichts funktioniert.
- **Korrektur**: gegen **net35-Referenz-Assemblies** kompilieren → Roslyn erzeugt wieder `newarr` (`new T[0]`). Explizite `Array.Empty<T>()`-Aufrufe werden zu Compile-Fehlern und werden ersetzt.

### 1.4 Build-Architektur (Ist-Zustand)

- Geteilter Code: [src/PoseBrowser/](../src/PoseBrowser/) (39 Dateien) + [src/Core/](../src/Core/).
- Per-Game-Targets: `targets/<GAME>/<Module>/` mit nur `*.csproj` + `Plugin.cs`, eingebunden via `$(SrcRoot)`.
- Game-Refs: `targets/<GAME>/Directory.Build.props`.
- Geteilte Settings inkl. `TargetFramework=net472`, IronPython, System.Memory: Root-[Directory.Build.props](../Directory.Build.props).
- Stand: HS2 = 7 Module, KKS = nur PoseBrowser, **KK = nur Props-Skelett** (auskommentiert) + veraltete `bin/obj`-Artefakte.

---

## 2. Strategie

**Leitprinzip:** Den geteilten Code **net35-kompatibel** machen. Alle Ersetzungen sind auf **allen drei Spielen** semantisch gültig → einmal im geteilten Code, **ohne** `#if`. KK-Spezifika (Framework, Unity, KKAPI, Pakete) bleiben im KK-Target isoliert.

| Fehlende API | Ersetzung (alle Targets gültig) | Mechanisch? |
|---|---|---|
| `Array.Empty<T>()` | `new T[0]` | ja |
| `string.IsNullOrWhiteSpace(s)` | `StringEx.IsNullOrWhiteSpace(s)` (neuer Helper) | ja |
| `x.HasFlag(y)` | `(x & y) == y` | ja (Bedacht bei Enum-Typ) |
| `IReadOnlyList<T>` | `IList<T>` | halb (Typänderung) |
| `IReadOnlyCollection<T>` | `ICollection<T>` | halb |
| `IReadOnlyDictionary<K,V>` | `IDictionary<K,V>` | halb |

---

## 3. Arbeitspakete

### Phase 0 — Vorbereitung & Baseline

- [ ] **0.1** Sicherstellen, dass HS2- und KKS-PoseBrowser aktuell **grün bauen** (Referenz-Baseline vor jeder Änderung).
- [ ] **0.2** Branch `feature/kk-posebrowser` anlegen.
- [ ] **0.3** Veraltete KK-Artefakte aufräumen (`targets/KK/PoseBrowser/bin`, `obj`).

### Phase 1 — BCL-Cleanup im geteilten Code (gilt für alle Targets)

> Ziel: `src/PoseBrowser/` + `src/Core/` net35-tauglich. Nach jeder Teilaufgabe HS2+KKS bauen.

- [ ] **1.1** Helper-Klasse `src/Core/Net35Compat.cs` anlegen:
  - `StringEx.IsNullOrWhiteSpace(string?)`
  - optional `EnumEx`/Extension für HasFlag-Ersatz (oder inline `(x & y) == y`)
- [ ] **1.2** `Array.Empty<T>()` → `new T[0]` in allen geteilten Dateien ersetzen (~70–80 Stellen; u. a. [PosePackExchange.cs](../src/PoseBrowser/PosePackExchange.cs) 24×, [PoseTagDatabase.cs](../src/PoseBrowser/PoseTagDatabase.cs), [PoseBrowserFilterPresets.cs](../src/PoseBrowser/PoseBrowserFilterPresets.cs), [PoseDataService.cs](../src/PoseBrowser/PoseDataService.cs)).
- [ ] **1.3** `string.IsNullOrWhiteSpace(...)` → `StringEx.IsNullOrWhiteSpace(...)` (~59 Stellen).
- [ ] **1.4** `.HasFlag(...)` → Bitmaske ersetzen.
- [ ] **1.5** `IReadOnlyList<T>` → `IList<T>`, `IReadOnlyCollection<T>` → `ICollection<T>`, `IReadOnlyDictionary<K,V>` → `IDictionary<K,V>` in Signaturen/Properties (Schwerpunkt [PoseBrowserCharacterApply.cs](../src/PoseBrowser/PoseBrowserCharacterApply.cs), [PoseBrowserGridLayout.cs](../src/PoseBrowser/PoseBrowserGridLayout.cs), [PoseBrowserCharacterSimpleColor.cs](../src/PoseBrowser/PoseBrowserCharacterSimpleColor.cs), [PoseBrowserStash.cs](../src/PoseBrowser/PoseBrowserStash.cs), [PoseBrowserHistory.cs](../src/PoseBrowser/PoseBrowserHistory.cs), [PoseBrowserCharacterConfig.cs](../src/PoseBrowser/PoseBrowserCharacterConfig.cs)).
  - Achtung: Aufrufer prüfen, die auf Immutability bauten; `ReadOnlyCollection<T>` erfüllt `IList<T>`.
- [ ] **1.6** Weitere net40+/net45+-APIs aufspüren (nach den ersten Builds erscheinen sie als Compile-Fehler gegen net35): u. a. `Directory.EnumerateFiles`→`GetFiles`, `File.ReadLines`→`ReadAllLines`, `string.Join`-Generic-Overload, `Path.Combine`-params-Overload, `Lazy<T>`, ValueTuple.
- [ ] **1.7** **Gegencheck**: HS2- und KKS-PoseBrowser bauen + kurz in Studio laden → keine Regression.

### Phase 2 — `#if`-Restrukturierung (binär → 3 Spiele)

- [ ] **2.1** Game-Symbole einführen: `HS2`, `KKS`, `KK` via `DefineConstants` in den jeweiligen `targets/<GAME>/Directory.Build.props` (KKS hat bereits `;KKS`).
- [ ] **2.2** `#if !KKS using AIChara;` → `#if HS2` ([PoseDataService.cs:8](../src/PoseBrowser/PoseDataService.cs#L8)). KK/KKS haben kein `AIChara`.
- [ ] **2.3** Heelz Control (HS2-exklusiv, braucht HS2Heelz): alle `#if !KKS` → `#if HS2` in [HeelzControlService.cs](../src/PoseBrowser/HeelzControlService.cs), [HeelzControlWindow.cs](../src/PoseBrowser/HeelzControlWindow.cs), [PoseBrowserWindow.cs](../src/PoseBrowser/PoseBrowserWindow.cs), [PoseBrowserWindow.History.cs](../src/PoseBrowser/PoseBrowserWindow.History.cs), [PoseBrowserWindow.Characters.cs](../src/PoseBrowser/PoseBrowserWindow.Characters.cs), [src/Core/PoseBrowserWikiRegistration.cs](../src/Core/PoseBrowserWikiRegistration.cs).
- [ ] **2.4** KKS-Guide-Workaround `#if KKS` → `#if KKS || KK` (vorläufig; in Phase 5 testen) in [PoseDataService.cs](../src/PoseBrowser/PoseDataService.cs) (Blöcke um Zeilen 920, 949, 980, 1149, 1167, 1553).
- [ ] **2.5** [PoseBrowserVersionInfo.cs](../src/PoseBrowser/PoseBrowserVersionInfo.cs): `#if KK`-Zweig mit Version, `StandaloneDllAssetName="KKSandbox.PoseBrowser.dll"`, Keys `poseBrowserKk`/`poseBrowserKkDownload`, eigener UserAgent.
- [ ] **2.6** Gegencheck HS2+KKS bauen.

### Phase 3 — Root-Props entkoppeln

- [ ] **3.1** `TargetFramework` aus dem Root-[Directory.Build.props](../Directory.Build.props) herauslösen: net472 in HS2/KKS-Props setzen, KK separat auf net35.
- [ ] **3.2** `IronPython` und `System.Memory` aus dem globalen `ItemGroup` entfernen und nur in die Module/Targets hängen, die sie wirklich nutzen (CopyScript, Notebook). PoseBrowser nutzt beide **nicht**.
- [ ] **3.3** Gegencheck: alle bestehenden HS2-Module + KKS bauen weiterhin.

### Phase 4 — KK-Target anlegen

- [ ] **4.1** `targets/KK/Directory.Build.props` (auskommentierten Block ersetzen):
  - `<TargetFramework>net35</TargetFramework>`
  - `<DefineConstants>$(DefineConstants);KK</DefineConstants>`
  - `PackageReference Microsoft.NETFramework.ReferenceAssemblies.net35` (PrivateAssets=all)
  - `IllusionLibs.Koikatsu.UnityEngine` (monolithisch — **nicht** `UnityEngine.Modules`, Unity 5.6 hat keine Module)
  - `IllusionLibs.Koikatsu.Assembly-CSharp` (passende Version)
  - `IllusionModdingAPI.KKAPI` (net35-Variante, ~1.40)
  - **ohne** IronPython/System.Memory
  - BepInEx-Pakete net35-tauglich sicherstellen
- [ ] **4.2** `targets/KK/PoseBrowser/KKSandbox.PoseBrowser.csproj` (Kopie der KKS-Variante; `AssemblyName=KKSandbox.PoseBrowser`).
- [ ] **4.3** `targets/KK/PoseBrowser/Plugin.cs` (Kopie der KKS-Plugin.cs):
  - `PluginGuid="com.kk.sandbox.posebrowser"`, eigener `PluginName`
  - `[BepInProcess(PluginProcessTargets.CharaStudio)]`
- [ ] **4.4** `pose-icon.png`-Content/EmbeddedResource wie in den anderen Targets.
- [ ] **4.5** Erst **kompilieren** (noch nicht laden): NuGet-Versionen iterativ festnageln, bis `dotnet build` grün ist.

### Phase 5 — Laufzeittests in KK-Studio

- [ ] **5.1** DLL nach `BepInEx/plugins` deployen, CharaStudio starten, Plugin lädt ohne Exception (LogOutput.log prüfen).
- [ ] **5.2** Kernpfade testen:
  - Pose speichern/laden/anwenden ([PoseDataService.ApplyPose](../src/PoseBrowser/PoseDataService.cs#L1129), `PauseCtrl.FileInfo`)
  - Charakter-Auswahl, Multi-Apply, Gruppen, History/Undo, Stash
  - Pose-Items (Item-Katalog `Info.dicItemLoadInfo`, Tree-Attach)
  - Thumbnail-Capture
- [ ] **5.3** Guide-Position nach Pose-Apply prüfen → entscheiden, ob `#if KKS || KK` (Schritt 2.4) bleibt oder angepasst wird.
- [ ] **5.4** Bekannte Erwartung: KK-Posen sind **nicht** mit HS2/KKS austauschbar (Studio-Binärformat unterscheidet sich) — kein Bug.

### Phase 6 — Integration & Infrastruktur

- [ ] **6.1** [build.ps1](../build.ps1): KK in `$gameConfig` (DeployDir, StudioExe `D:\Games\Koikatsu BetterRepack RX21\CharaStudio.exe`, Prozessnamen `CharaStudio`/`Koikatsu`) + Target `PoseBrowser-KK` in `$targets`.
- [ ] **6.2** [HS2-Sandbox.sln](../HS2-Sandbox.sln): KK-csproj aufnehmen.
- [ ] **6.3** `versions.json` + Update-Check-Keys + README/Manual um KK ergänzen.
- [ ] **6.4** CI-Workflow ([.github/WORKFLOW.md](../.github/WORKFLOW.md)) um KK-Build/Release erweitern.

---

## 4. Risiken & offene Punkte

| Risiko | Auswirkung | Gegenmaßnahme |
|---|---|---|
| Weitere net4x-APIs im geteilten Code | Compile-Fehler in Phase 1/4 | Iterativ gegen net35 bauen; Fehler abarbeiten |
| `IReadOnly*`→`IList` ändert Aufrufer-Annahmen | Subtile Bugs (Mutation) | Aufrufstellen prüfen; wo nötig defensiv kopieren |
| `IllusionLibs.Koikatsu.*` / KKAPI net35-Versionen | Restore/Build scheitert | Versionen an installiertem KKAPI 1.40 / Unity 5.6 ausrichten |
| KKAPI/BepInEx ziehen selbst System.Memory | net35-Konflikt | Transitive Refs prüfen, ggf. net35-kompatible Pins |
| Studio-API-Feinheiten (Guide, Thumbnail) auf Unity 5.6 | Laufzeitfehler | Phase 5 Tests; `#if KK`-Sonderfälle nur wo nötig |
| BCL-Cleanup berührt HS2/KKS | Regression | Nach jeder Phase HS2+KKS bauen+laden |

---

## 5. Aufwand (grob)

| Phase | Aufwand | Risiko |
|---|---|---|
| 1 — BCL-Cleanup | mittel (viel mechanisch) | niedrig–mittel |
| 2 — `#if`-Umbau | niedrig–mittel | niedrig |
| 3 — Root-Props | niedrig | niedrig |
| 4 — KK-Target | niedrig–mittel | mittel (NuGet-Versionen) |
| 5 — Laufzeittests | mittel | **mittel** |
| 6 — Infrastruktur | niedrig | niedrig |

**Heelz Control entfällt auf KK** (wie auf KKS) — Designentscheidung. Der eigentliche Studio-Funktionscode bleibt weitgehend unverändert; der Aufwand liegt im Framework-/BCL-Mismatch.

---

## 6. Empfohlene Reihenfolge

1. Phase 0 (Baseline) → 1 (BCL-Cleanup, risikoentkoppelt, hilft allen Targets)
2. Phase 2 + 3 (Strukturumbau)
3. Phase 4 (KK-Target bauen)
4. Phase 5 (testen) → 6 (Integration)
