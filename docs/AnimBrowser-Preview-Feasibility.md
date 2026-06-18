# Machbarkeitsanalyse: Animations-Vorschau im AnimBrowser

> Status: Analyse / noch nicht umgesetzt
> Betrifft: `src/AnimBrowser/`, Targets HS2 / KK / KKS

## Ziel (Feature-Wunsch)

Beim Hovern über eine Animation soll ein kleines Fenster aufploppen, das eine
kleine Low-Poly-/Strichfigur zeigt, die die Animation abspielt — als Vorschau.

- Optional (perf-bedingt opt-in, in den Einstellungen umschaltbar): die Animation
  direkt **im Thumbnail** statt nur im Popup — und auch dann am besten nur beim Hovern.
- **Gruppierte Animationen**: alle beteiligten Animationen **kombiniert** sehen.
  Eine Gruppe kann aus **mehr als zwei** Nicht-Phasen-Animationen bestehen
  (z. B. `m`, `f`, `f2`, …), also N Figuren, nicht fix zwei.
- **Phasen innerhalb von Gruppen**: die Vorschau läuft die Phasen durch —
  `In → Loop → Out` und dann wieder von vorn.

## Kurzfazit

**Machbar, aber kein triviales Feature.** Der Hover-Vorschau-Teil mit Strichmännchen
ist mit vertretbarem Performance-Budget realisierbar. Das eigentliche Risiko steckt
nicht in der UI, sondern in einer einzigen technischen Frage:

> Kann ich einen HS2-Animationsclip **isoliert** auf ein eigenes, minimales Skelett abspielen?

Das muss zuerst in einem **Spike** geklärt werden — alles andere hängt daran.

---

## Ausgangslage im aktuellen Code

- **Animationen werden nur auf echte Szenen-Charaktere angewandt.**
  `AnimPlaybackService.ApplyAnimation` ruft `oci.LoadAnime(group, category, no, 0f)`
  auf einem `OCIChar` (`src/AnimBrowser/AnimPlaybackService.cs:16`). Es gibt aktuell
  **keinen** isolierten Clip-Abspielpfad.
- **Der Thumbnail-Service erzeugt nur einfarbige Platzhalter**
  (`src/AnimBrowser/AnimThumbnailService.cs:69`) — es existiert noch *kein* echtes
  Rendering. Gut: die Schnittstelle (`item.Thumbnail` als `Texture2D`, LRU-Cache,
  `MaxResident = 96`) ist bereits da und wiederverwendbar.
- **Render-to-Texture-Pattern ist im Projekt schon erprobt.**
  `PoseDataService.CaptureScreenArea` nutzt `Camera` + `RenderTexture` + `ReadPixels`
  (`src/PoseBrowser/PoseDataService.cs:1226`). Dieselbe Technik trägt die Vorschau.
- **Phasen-/Gruppen-Datenmodelle existieren bereits vollständig.**
  `AnimPhase` (In/Loop/Out), `AnimDisplayGroup.Phases`, `AnimDisplayGroup.GenderParticipants`,
  `AnimGroupSlot.Phase/Gender/GenderOrdinal` (`src/AnimBrowser/AnimDisplayModels.cs`).
  Die Slot-Auflösung in `ApplyGroupPhase` (`src/AnimBrowser/AnimBrowserWindow.Grouping.cs:1307`)
  ist die Blaupause für "welche Clips gehören zu welcher Figur/Phase".
- **Katalog-Quelle.** `Info.dicAnimeLoadInfo[group][category][no] → AnimeLoadInfo`
  (`src/AnimBrowser/AnimCatalogService.cs:145`). Aktuell werden daraus nur `name` und
  `sort` gelesen — die für das Clip-Laden nötigen Bundle-/Asset-Felder noch **nicht**.

---

## Render-Strategien

### A — Echtes Mini-Studio (Off-Screen-Charakter + eigene Kamera → RenderTexture)
Einen versteckten echten Charakter instanziieren, `LoadAnime` exakt wie bisher aufrufen,
eine dedizierte Kamera rendert ihn in eine RenderTexture.

- ✅ Höchste Originaltreue, nutzt den bestehenden Pfad 1:1, geringstes "geht das
  überhaupt"-Risiko.
- ❌ Schwer (volles Body-Mesh, Skinning, Cloth, Physik) — widerspricht dem
  "low performance impact"-Wunsch. Bei Gruppen N Bodies. Instanziierung kostet
  hunderte ms + Speicher.

### B — Strichmännchen (empfohlenes Ziel)
Einmalig nur die **Knochen-Transform-Hierarchie** eines echten Charakters klonen
(ohne Meshes/Cloth/Physik), den Clip darauf abspielen, und pro Frame die
Weltpositionen einer Joint-Auswahl per `GL`-Linien in eine kleine RenderTexture zeichnen.

- ✅ Genau das gewünschte Strichmännchen, sehr günstig pro Frame (kein Skinning,
  kein Mesh, keine Physik).
- ✅ Indem man das Skelett aus einem echten Charakter *klont*, stimmen die
  Transform-Pfade exakt → der Clip "bindet" automatisch (kein Hardcoding von Bone-Namen).
- ❌ Setzt voraus, dass der Clip auf einem reinen Knochen-Klon abspielbar ist (→ Spike).
  Braucht mind. einmal einen Charakter als Skelett-Vorlage (oder das Basis-Body-Prefab).

### C — Hybrid (Fallback)
A's Rig (Animator + Avatar eines echten Bodys), aber nur Skelett-Linien rendern
(Mesh ausgeblendet). Greift, falls B's Klon-Skelett die Clips nicht annimmt
(z. B. weil sie Humanoid/Muscle-Space sind).

---

## Kritischer Spike (Gate — zuerst, ~1–2 Tage)

Drei Dinge müssen bewiesen werden, bevor sich der Rest lohnt:

1. **Clip beschaffen** — aus `AnimeLoadInfo` Bundle-Pfad + Asset-Name auslesen und den
   `AnimationClip` laden (das macht `LoadAnime` intern; den Lookup nachbauen; die Felder
   sind noch nicht im Code).
2. **Clip-Typ bestimmen** — *Generic* (an Transform-Pfade gebunden) oder *Humanoid*
   (Muscle-Space)? Das entscheidet, ob ein geklontes Skelett reicht (Generic → Strategie B)
   oder ob ein Animator+Avatar nötig ist (Humanoid → Strategie A/C).
3. **Abspielen + Joints auslesen** — Clip auf dem Klon abspielen und Joint-Weltpositionen
   pro Frame lesen.

Werkzeuge im Repo: `scripts/game-assembly-inspector/` und `docs/HS2-Studio-API-Analysis.md`
(letztere dokumentiert bereits `LoadAnime` und den RenderTexture-Workflow).

**Entscheidungspunkt:** Spike positiv → Strategie B. Spike negativ → Strategie C.
Die UI-, Hover-, Phasen- und Gruppen-Logik bleibt von dieser Wahl unberührt.

---

## Vorgeschlagene Architektur

Ein persistenter `AnimPreviewStage` (MonoBehaviour, dauerhaft, wiederverwendet):

- dedizierte `Camera` + `RenderTexture` (niedrig aufgelöst, z. B. 128–256 px),
- ein **Pool von N Strichmännchen-Rigs** — eines pro darzustellender Figur. N ergibt
  sich aus der Anzahl der Slots/Gender-Teilnehmer der gehoverten Gruppe (`m`, `f`, `f2`, …),
  nicht fix zwei. Bei einer Einzel-Animation N = 1.
- alle Rigs teilen sich **denselben Root-Transform** (identische Position, Rotation **und**
  Scale). Gruppierte Animationen sind in der Regel genau so gebaut: die Charaktere beider
  Clips erwarten einen exakt gleichen Wurzel-Transform, und die co-authored Clips bewegen die
  Knochen relativ dazu so, dass sie zusammenpassen (deshalb funktioniert das Kombinieren
  überhaupt). In der Vorschau heißt das: ein gemeinsamer Root (z. B. Identity), alle Rigs als
  Kinder davon — **keine** manuellen Offsets je Figur. Der Pool wächst bei Bedarf und wird
  wiederverwendet.
- ein **Phasen-Sequencer**: `In`-Clip einmal (Clip-Länge) → `Loop`-Clip → `Out`-Clip einmal
  → wieder von vorn (per Update/Coroutine, gespeist aus `group.Phases`). Für jede Figur wird
  pro aktiver Phase ihr passender Slot-Clip gewählt (analog `AnimDisplayGroup.FindSlot` /
  der Verteil-Logik in `ApplyGroupPhase`).
- **render-on-demand**: Kamera nur aktiv, wenn etwas gehovert wird.

**Slot → Figur-Zuordnung (Gruppen).** Die Verteilung der Slots auf die Figuren spiegelt
`ApplyGroupPhase` wider: nach Gender gruppieren, nach `GenderOrdinal` sortieren, je
Teilnehmer der passende Slot der aktiven Phase. Mehr als zwei Nicht-Phasen-Slots ⇒
einfach mehr Figuren im Pool; keine Sonderbehandlung für genau zwei.

### IMGUI-Seite (Hover)

- Hover-Erkennung im Grid: das Karten-Rechteck steht bereits zur Verfügung
  (`thumbRect.Contains(...)`, vgl. `src/AnimBrowser/AnimBrowserWindow.cs:475`).
- Gehoverte Ref + Rect **nur im Repaint-Event** speichern und **entprellen** (Debounce),
  sonst flackert es (OnGUI läuft mehrfach pro Frame).
- Die RenderTexture in einer schwebenden Box nahe dem Cursor per `GUI.DrawTexture` zeichnen.
- Settings-Toggle: Popup an/aus; separater opt-in-Toggle für animierte Grid-Thumbnails.

---

## Performance-Bewertung

- **Hover-Popup, wiederverwendete Strichfiguren, render-on-demand, kleine RT** →
  tatsächlich low-impact. ✅
- **Animierte Thumbnails *im Grid* für alle Karten gleichzeitig** → *nicht* low-impact
  (viele Animatoren/Renderpässe). Sollte ein **opt-in**-Modus bleiben und auch dann
  nur die gehoverte Karte animieren.

---

## Hauptrisiken

1. **Clip-Binding (Generic vs. Humanoid)** — das Make-or-break, daher der Spike zuerst.
2. **AssetBundle-Lebenszyklus/Speicher** — Clips cachen + sauber entladen (kleiner LRU).
3. **IK-Differenz** — die Szene wendet zusätzlich IK an (`docs/HS2-Studio-API-Analysis.md`,
   `FileInfo.Apply`); die reine Basis-Anim-Vorschau sieht minimal anders aus. Für eine
   Vorschau akzeptabel.
4. **IMGUI-Hover-Flackern** — Hover nur im Repaint erfassen + Debounce.
5. **Multi-Game-Portierung** — Repo baut HS2/KK/KKS; KK ist älteres Unity / .NET 3.5.
   Clip-Laden und Skelett-Namen unterscheiden sich → Preview-Backend pro Target abstrahieren.

---

## Grobe Aufwandsschätzung (nach erfolgreichem Spike)

| Phase | Inhalt | Aufwand |
|------|--------|---------|
| 0 | Spike (Gate): Clip laden + auf Klon-Skelett abspielen | 1–2 T |
| 1 | Einzel-Anim Hover-Popup + Strichfigur + Settings-Toggle | 3–5 T |
| 2 | Phasen-Sequencer (In→Loop→Out→…) | 2–3 T |
| 3 | Gruppen kombiniert (N Figuren, nicht nur zwei) | 2–3 T |
| 4 | Optional: animierte Grid-Thumbnails (hover-bound) + Settings | ~2 T |
| — | Per-Game-Backend HS2/KK/KKS | querschnittlich |

**Empfehlung:** Mit Strategie B (Strichmännchen) als Ziel starten, aber **zuerst den Spike**
fahren. Fällt der Spike negativ aus, auf Strategie C ausweichen.
