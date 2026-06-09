# Testplan: PoseBrowser (alle Spiele, Fokus Koikatsu-Port)

> Zweck: Vollständige funktionale Abnahme des PoseBrowser-Moduls über **HS2**, **KKS** und den neuen **KK**-Port (v1.0.0).
> Schwerpunkt: KK ist neu portiert (.NET 3.5 / Unity 5.6.2f1) — die net35-/Unity-5.6-sensiblen Pfade sind markiert mit **⚠KK**.
> Format: Jede Sektion = Feature-Bereich. Die drei Abhak-Spalten **HS2 / KKS / KK** stehen **vorne**; rechts eine **Kommentar**-Spalte für Notizen.

---

## 0. Testumgebung & Vorbereitung

### 0.1 Spiele / Pfade

| Spiel | Studio | Managed | Deploy-Ziel |
|---|---|---|---|
| HS2 | StudioNEOV2 | `D:\Honey Select\StudioNEOV2_Data\Managed` | `…\BepInEx\plugins\HS2-Sandbox` |
| KKS | CharaStudio | `D:\Games\Koikatsu Sunshine EX BetterRepack R12\CharaStudio_Data\Managed` | `…\BepInEx\plugins\KKS-Sandbox` |
| **KK** | CharaStudio | `D:\Games\Koikatsu BetterRepack RX21\CharaStudio_Data\Managed` | `…\BepInEx\plugins\KK-Sandbox` |

### 0.2 Gemeinsame Vorbedingungen

- [x] Build je Spiel erfolgreich (`build.ps1`), DLL deployed: `HS2Sandbox.PoseBrowser.dll` / `KKSSandbox.PoseBrowser.dll` / **`KKSandbox.PoseBrowser.dll`**.
- [x] KKAPI/HS2API/KKSAPI + BepInEx geladen (Studio startet).
- [x] Config-Verzeichnis für **alle** Spiele: `BepInEx/config/com.hs2.sandbox/`.
- [x] Testdaten vorhanden: ≥ 20 Pose-Dateien in `UserData/studio/pose`, davon einige in Unterordnern, ein paar mit Thumbnails, ein paar ohne.
- [x] Vor Beginn: bestehende `pose_*.tsv` / `pose_*.json` sichern (Tests verändern sie).
- [x] Mindestens 3 ladbare Charaktere (≥1 männlich, ≥1 weiblich) für Multi-Apply / Gruppen.

### 0.3 Legende

`⚠KK` = port-kritischer Pfad (net35 / Unity 5.6). `[B]` = Baseline auf HS2/KKS zum Vergleich empfohlen. `[3×]` = auf allen drei Spielen ausführen.
Die drei Spalten **HS2 / KKS / KK** je Testzeile: `[ ]` = offen · `[x]` = bestanden · `[!]` = **nicht bestanden** · `—` = für dieses Spiel nicht anwendbar.

---

## A. Smoke-Tests (Laden & Grundzustand) — `[3×]`

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [x] | [x] | [x] | A1 | Frische Studio-Session | Studio starten, `BepInEx/LogOutput.log` prüfen | Zeile „… Pose Browser v… loaded", **keine** Exception (insb. **⚠KK** keine `MissingMethodException`/`TypeLoadException`) | |
| [x] | [x] | [x] | A2 | Studio offen | Linke Toolbar: Pose-Icon vorhanden | Icon wird angezeigt (PNG geladen, **⚠KK** `ToolbarIconLoader`) | |
| [x] | [x] | [x] | A3 | — | Pose-Icon klicken | PoseBrowser-Fenster öffnet, Toggle-Status synchron | |
| [x] | [x] | [x] | A4 | Fenster offen | Erneut klicken | Fenster schließt, Toggle aus | |
| [x] | [x] | [x] | A5 | **⚠KK** | Version im Fenster/Log | Zeigt **1.0.0** (KK), 2.3.1 (KKS), 5.4.1 (HS2) | |
| [x] | [x] | [x] | A6 | Erststart ohne Configs | Fenster öffnen | Configs werden ohne Fehler angelegt; Grid lädt aus `studio/pose` | |

---

## B. Fenster, Layout & Toolbar

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [x] | [x] | [x] | B1 | Full-Layout | **View** klicken → Full/List/Mini durchschalten | Wechselt zyklisch; je Modus eigene gespeicherte Größe/Position | |
| [x] | [x] | [x] | B2 | Full | Fenster am Grip (unten rechts) skalieren | Größe ändert sich; bleibt nach Re-Open erhalten | |
| [x] | [x] | [x] | B3 | Full | **Help**, **Options**, **Tag filter**, **Chars**, **Sort**, **History**, **Stash** öffnen | Docken als Kette rechts, **überlappen nicht** | |
| [x] | [x] | [x] | B4 | List | Layout = List | Ordnerbaum + Textliste der Posen (keine Thumbnails); Seitenpanels verborgen | |
| [x] | [x] | [x] | B5 | Mini | Layout = Mini | Minimal-Strip mit Folder-/Pose-Pfeilen + Reapply | |
| [x] | [x] | [x] | B6 | Mini | Folder-/Pose-Pfeile + Reapply benutzen | Schrittweises Navigieren & erneutes Anwenden funktioniert | |
| [x] | [x] | [x] | B7 | — | Layout wechseln, Studio neu starten | `pose_browser_options.json` merkt Layout-Tier + je-Layout-Rect | |
| [x] | [x] | [x] | B8 | Fenster offen | Studio-Kamera bewegen, GUI-Interaktion | IMGUI bleibt nutzbar; **⚠KK** keine Skin-/GUIStyle-Fehler (`GuiSkinHelper`) | |

---

## C. Ordner & Bibliotheks-Scope

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [x] | [ ] | [ ] | C1 | Posen in Unterordnern | **All poses** wählen | Rekursive Anzeige aller Posen | |
| [x] | [ ] | [ ] | C2 | — | **Root only** | Nur Dateien direkt im Pose-Root | |
| [x] | [ ] | [ ] | C3 | — | Ordnernamen klicken | Nicht-rekursive Ansicht nur dieses Ordners | |
| [x] | [ ] | [ ] | C4 | Favoriten gesetzt | **★ Favorites** | Virtuelle Ansicht aller Favoriten (bibliotheksweit) | |
| [x] | [ ] | [ ] | C5 | — | **↻** Refresh nach externem Datei-Add | Baum/Grid aktualisiert | |
| [x] | [ ] | [ ] | C6 | Ordner mit Kindern | **► / ▼** | Expand/Collapse rein kosmetisch (lädt nicht neu) | |
| [x] | [ ] | [ ] | C7 | Kein Ordner gewählt | Footer: **New folder…** | Neuer Ordner unter Root | |
| [x] | [ ] | [ ] | C8 | Ordner gewählt | **Rename…** | Ordner umbenannt, Baum aktualisiert | |
| [x] | [ ] | [ ] | C9 | Leerer Ordner | **Delete folder…** | Löscht; bei **nicht-leerem** Ordner roter Fehler im Footer | |
| [x] | [ ] | [ ] | C10 | Full-Layout, Root | **Export library tree…** | v2-Branch-ZIP der ganzen Bibliothek | |
| [x] | [ ] | [ ] | C11 | Full-Layout, Ordner | **Export branch…** | v2-Tree-Branch-ZIP des Subtrees | |

---

## D. Suche & Filter

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [x] | [ ] | [ ] | D1 | Grid gefüllt | Text in Suche eingeben | Filtert nach Name/Pfadkontext live | |
| [x] | [ ] | [ ] | D2 | **⚠KK** Regex | **.\*** aktivieren, gültiges Muster | Case-insensitive Regex filtert; **KK darf nicht crashen** (RegexEx nutzt `RegexOptions.None` statt `Compiled`) | |
| [x] | [ ] | [ ] | D3 | Regex an | Ungültiges Muster (z. B. `[`) | Rote Fehlerzeile unter Suchleiste, kein Crash | |
| [x] | [ ] | [ ] | D4 | Favoriten vorhanden | **★**-Toggle | Grid auf Favoriten beschränkt | |
| [x] | [ ] | [ ] | D5 | Tags vergeben | **Tags** öffnen, Tag klicken | Zyklus neutral → **+** → **−** → neutral; Label `Tags (+x −y)` | |
| [x] | [ ] | [ ] | D6 | Include-Tags | **AND/OR** umschalten | AND = alle +Tags, OR = ein +Tag | |
| [x] | [ ] | [ ] | D7 | Exclude-Tag, ungruppiert | — | Pose **versteckt** | |
| [x] | [ ] | [ ] | D8 | Exclude-Tag, in Gruppe | — | Mitglieder bleiben, betroffene **gedimmt**, Tag-Name **rot** | |
| [x] | [ ] | [ ] | D9 | Aktive Filter | **Clear active filters** | Setzt nur Include/Exclude zurück (nicht Disk-Tags) | |
| [x] | [ ] | [ ] | D10 | Sort offen | **Last used / Last updated / Last created / Name** | Sortierkriterium wählbar; 2. Klick toggelt ↑/↓ | |
| [x] | [ ] | [ ] | D11 | Pose aus Browser anwenden | Sort = Last used | „Last used" aktualisiert nach Apply | |
| [x] | [ ] | [ ] | D12 | Suche + Sort + Tags | Kombiniert | Filter verengen, Sort ordnet das Ergebnis | |

---

## E. Grid, Thumbnails, Pagination, Auswahl

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [x] | [ ] | [ ] | E1 | Posen mit/ohne Thumb | Grid betrachten | Karte: Thumbnail/Platzhalter, Name, ★, Tag-Zeile | |
| [x] | [ ] | [ ] | E2 | Options: Card width | Slider ändern | Spalten/Streckung passen sich an (min/max) | |
| [x] | [ ] | [ ] | E3 | Options: items/page > 0 | Blättern | **◀ / ▶**, `Page x/y · n poses` | works but changing and applying the pagination value in options doesn't update the grid by itself |
| [x] | [ ] | [ ] | E4 | items/page = 0 | — | Alles in einer Scroll-Ansicht | |
| [x] | [ ] | [ ] | E5 | — | **Checkbox** auf Karte | Auswahl toggelt **ohne** Apply | |
| [x] | [ ] | [ ] | E6 | Char selektiert | **Linksklick** Thumbnail | Andere abgewählt, diese gewählt, **Pose angewandt** | |
| [x] | [ ] | [ ] | E7 | — | **Ctrl+Klick** | Toggelt Karte in Auswahl | |
| [x] | [ ] | [ ] | E8 | — | **Shift+Klick** | Range-Select im gefilterten Listenindex | |
| [!] | [ ] | [ ] | E9 | Char selektiert | **Rechtsklick** Thumbnail | **Nur Apply**, Auswahl unverändert | |
| [x] | [ ] | [ ] | E10 | Prop/Accessory selektiert | Apply | Nicht-Charaktere ignoriert | |

---

## F. Pose anwenden (Einzel)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [x] | [ ] | [ ] | F1 | 1 Charakter | Linksklick Pose | FK/IK korrekt angewandt; **⚠KK** `PauseCtrl.FileInfo.Load/Apply` ohne Fehler | |
| [x] | [ ] | [ ] | F2 | Mehrere Charaktere | Linksklick Pose | Pose auf **alle** selektierten angewandt | |
| [x] | [ ] | [ ] | F3 | **⚠KK** Guide-Position | Charakter verschoben, Pose anwenden | Welt-Position bleibt stabil (changeAmount-Workaround `#if KKS \|\| KK`) — Charakter springt nicht | |
| [x] | [ ] | [ ] | F4 | Pose mit Animation | Apply, Option „Freeze anim speed" an | `animeSpeed=0` nach Apply | |
| [x] | [ ] | [ ] | F5 | Char-Zeile | none/one/n selektiert | Label/Tooltip zeigt none / Name / `n selected` + Namensliste | |

---

## G. Multi-Character-Apply & Chars-Listen

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [x] | [ ] | [ ] | G1 | 2+ Posen selektiert | **Apply to characters…** sichtbar | Button erscheint (bei 1 Pose nicht) | |
| [x] | [ ] | [ ] | G2 | 1 Gruppen-Entity selektiert | — | Button erscheint | |
| [!] | [ ] | [ ] | G3 | Import-Preview offen | — | Button **nicht** sichtbar | when importing a branch: "[Warning:HS2 Sandbox - Pose Browser] PoseBrowser: Could not read pack: Expected ']' for vec3." |
| [x] | [ ] | [ ] | G4 | Chars-Pane | **Load characters from scene** | Male/Female-Spalten gefüllt | Chars-Pane hat keine zwei spalten mehr, sondern nur noch eine liste mit gender selectors für jeden char (intended) |
| [x] | [ ] | [ ] | G5 | — | **↑/↓**, **⇄**, **✕** | Priorität ändern, Spalte wechseln, entfernen | |
|  -  |  -  |  -  | G6 | Untagged Posen | **Male/Female before** umschalten | Reihenfolge bei gleichem Rang ändert sich (Default male first) | |
| [x] | [ ] | [ ] | G7 | Male+Female-Pose, 1m+1w | Apply to characters… | Jede Pose → passender Listen-Charakter | |
| [x] | [ ] | [ ] | G8 | 5 untagged, 3 Chars | Apply | Posen 1–3 zugeordnet, 4–5 übersprungen | |
| [x] | [ ] | [ ] | G9 | 2 untagged, 4 Chars | Apply | Pass 1: 2 posiert; Pass 2: restliche 2 erhalten Pose 1–2 | |
| [x] | [ ] | [ ] | G10 | 2 Male-Posen, 1 Mann | Apply | 1. Pose angewandt, 2. übersprungen (kein Overwrite) | |
| [x] | [ ] | [ ] | G11 | — | Chars neu laden, Studio neu starten | `pose_browser_character_config.json` persistiert | |

---

## H. Pose-Gruppen

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | H1 | 2+ ungruppierte Posen | **Group…** + Name | Gruppe als bordierter Segment-Block mit Header `▦` | |
| [ ] | [ ] | [ ] | H2 | Gruppenmitglieder | **Ungroup** | Mitgliedschaft entfernt, Dateien bleiben | |
| [ ] | [ ] | [ ] | H3 | Gruppen-Header klicken | Entity-Modus | Gruppen-Aktionsleiste: Rename/Tags/Export/Apply/Thumbs/Positions | |
| [ ] | [ ] | [ ] | H4 | Header | **Rename…** / **Tags…** | Name/Group-Tags geändert, persistiert in `pose_groups.tsv` | |
| [ ] | [ ] | [ ] | H5 | Header Ctrl/Shift+Klick | — | Mehrfach-/Range-Auswahl wie bei Posen | |
| [ ] | [ ] | [ ] | H6 | Gruppe vorhanden | Studio neu starten | Gruppe bleibt (Membership, Namen, Tags, Offsets, Heights) | |
| [ ] | [ ] | [ ] | H7 | Group-Tag-Filter | Tag exclude | Segment-Filter greift auf Gruppenebene (§D7/D8) | |
| [ ] | [ ] | [ ] | H8 | Große Gruppe | Sort | Gruppe als Block; Fortsetzungs-Header über mehrere Reihen | |

### H.b Relative Positionen (Save/Apply)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | H10 | Chars konfiguriert, #Posen=#Chars | Gruppe via **Apply to characters…** | Erste Pose = Anker-Charakter | |
| [ ] | [ ] | [ ] | H11 | Nach Apply, Charaktere verschoben | Header → **Save positions…** | Speichert lokale Offsets+Rotation vs Anker + Body-Height pro Pose | |
| [ ] | [ ] | [ ] | H12 | Falsche Char-Anzahl | Save positions… | Button **disabled** (Hover erklärt Grund) | |
| [ ] | [ ] | [ ] | H13 | Gespeicherte Positionen | **Apply relative positions** (global) an, Gruppe anwenden | Nicht-Anker orbiten Anker (Anker + Rotation × Offset) | |
| [ ] | [ ] | [ ] | H14 | + **Adjust for body height** | Apply | `offset.y` aus Höhen-Verhältnis skaliert | |
| [ ] | [ ] | [ ] | H15 | — | **Apply relative positions** aus | Nur Posen, Layout bleibt gespeichert | |
| [ ] | [ ] | [ ] | H16 | — | **Clear positions** | Offsets **und** Heights entfernt | |

---

## I. Gruppen-Thumbnails (Monocolor)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | I1 | Gruppen-Entity, #Chars=#Posen, Gender passt | **Group thumbnails…** | Alle Posen angewandt, Capture-Overlay öffnet | |
| [ ] | [ ] | [ ] | I2 | Overlay | Grünen Rahmen ziehen/skalieren | Bildausschnitt komponierbar | |
| [ ] | [ ] | [ ] | I3 | Capture-Lauf | Pro Pose in Display-Reihenfolge | Andere Charaktere in **Studio Simple Color** (monocolor); **⚠KK** `PoseBrowserCharacterSimpleColor` auf Unity 5.6 prüfen | |
| [ ] | [ ] | [ ] | I4 | — | **Capture** | PNG pro Mitglied geschrieben | |
| [ ] | [ ] | [ ] | I5 | — | **Skip** / **Auto-capture** / **Cancel** | Skip lässt unverändert; Auto kettet (Delay aus Options); Cancel stellt Rendering wieder her | |
| [ ] | [ ] | [ ] | I6 | Import-Preview offen | Group thumbnails… | **Deaktiviert** | |
| [ ] | [ ] | [ ] | I7 | Falsche Char-Anzahl/Gender | — | Button disabled | |

---

## J. Pose-Stash (FK/IK-Clipboard)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | J1 | 1 Charakter | **Stash selected character** | Eintrag `Name  yyyy-MM-dd HH:mm:ss` (neueste oben) | |
| [ ] | [ ] | [ ] | J2 | 1+ Charaktere | Eintrag anklicken | Pose auf **alle** angewandt (nur FK/IK, keine Welt-Pos) | |
| [ ] | [ ] | [ ] | J3 | **Auto-delete after apply** an | Apply | Eintrag nach Erfolg entfernt | |
| [ ] | [ ] | [ ] | J4 | Eintrag | **x** → Yes/No | Bestätigung, dann gelöscht | |
| [ ] | [ ] | [ ] | J5 | — | **Clear entire stash** | Bestätigung nötig, leert alles | |
| [ ] | [ ] | [ ] | J6 | Docked Stash | **Float** | Undockt zu eigenem Fenster | |
| [ ] | [ ] | [ ] | J7 | Floating Stash | **Dock** | Re-attach neben Browser; bei verstecktem Browser schließt Float | |
| [ ] | [ ] | [ ] | J8 | Floating | Titelleiste ziehen, ◢ skalieren | Move/Resize; Rect persistiert | |
| [ ] | [ ] | [ ] | J9 | Hotkey „Toggle undocked pose stash" | Drücken | Floating-Stash toggelt, auch bei geschlossenem Hauptfenster | |
| [ ] | [ ] | [ ] | J10 | Docked Stash offen | Hauptfenster schließen | Docked-Stash schließt mit; Floating bleibt offen | |
| [ ] | [ ] | [ ] | J11 | — | Studio neu starten | `pose_stash.json` (base64 Blobs, auto-delete-Flag) persistiert | |

---

## K. Auswahlleiste (Bibliotheks-Aktionen)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | K1 | 1 On-Disk-Pose | **Update Pose** | Datei aus Szene überschrieben; Thumbnail-Option | |
| [ ] | [ ] | [ ] | K2 | 1 Pose | **Rename…** | Anzeigename; optional Datei-Rename auf safe name | |
| [ ] | [ ] | [ ] | K3 | Mehrere | **Tag Selected** | Assign-Fenster: Massen-Add/Remove von Tags | |
| [ ] | [ ] | [ ] | K4 | Mehrere | **Fav Selected** | Favoriten-Flag toggelt je Item | |
| [ ] | [ ] | [ ] | K5 | Auswahl | **Thumbnails…** | Capture-Overlay (Einzel, §N) | |
| [ ] | [ ] | [ ] | K6 | Auswahl | **Export…** | v3/v5-ZIP (Tags, Favs, Gruppen wenn voll) | |
| [ ] | [ ] | [ ] | K7 | Ungruppiert / 1 volle Gruppe | **Move…** | Ziel im Baum wählen → **Apply/Cancel** im Footer | |
| [ ] | [ ] | [ ] | K8 | dito | **Copy…** | Kopiert ins Zielverzeichnis | |
| [ ] | [ ] | [ ] | K9 | Auswahl | **Delete…** | Bestätigung; Kopie nach `!_AutoBackup`, dann Löschen, Daten-Refresh | |
| [ ] | [ ] | [ ] | K10 | — | **Deselect** | Auswahl im gefilterten Listenset geleert | |
| [ ] | [ ] | [ ] | K11 | Move/Copy aktiv | Footer **Apply/Cancel** oben; **All poses** ausgegraut | Ziel-Pick ohne Grid-Reload; Cancel stellt Sync wieder her | |

---

## L. Pose-Items (Workspace-Items pro Pose)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | L1 | 1 On-Disk-Pose | **Items** | Items-Pane dockt in Seitenketten | |
| [ ] | [ ] | [ ] | L2 | 1 Charakter + 1+ OCIItem selektiert | **Add selected item(s)** | „Will add: …" listet; Eintrag registriert (Katalog-Slots, bundle/asset/manifest, Transform, Scale, Body-Height, ggf. Body-Part + attach changeAmount) | |
| [ ] | [ ] | [ ] | L3 | Pose nicht am Char angewandt | Add | **Gelber** Banner-Hinweis; Add/Load trotzdem möglich | |
| [ ] | [ ] | [ ] | L4 | Liste | **☑** + **Load Selection** | Geprüfte Zeilen geladen (1 Char nötig) | |
| [ ] | [ ] | [ ] | L5 | — | **Load All** | Alle Einträge geladen | |
| [ ] | [ ] | [ ] | L6 | Eintrag | **Name** (Button) | Sofort laden | |
| [ ] | [ ] | [ ] | L7 | — | **✎** / **X** | Rename / Entfernen | |
| [ ] | [ ] | [ ] | L8 | Gleiches Katalog-Item in Studio selektiert | — | Zeilenname **fett** | |
| [ ] | [ ] | [ ] | L9 | Load-Optionen | **Position/Rotation/Scale/Load as free** togglen | Jeweils angewandt/übersprungen; Scale für aktuellen Object-Scale/Body-Height korrigiert | |
| [ ] | [ ] | [ ] | L10 | Attach gespeichert | Normal laden vs **Load as free** | Normal: re-parent über Workspace-Tree; Free: Welt-Layout ohne Parent | |
| [ ] | [ ] | [ ] | L11 | Body-Part nicht gefunden | Load | Orange **⚠** auf Zeile (frei platziert) | |
| [ ] | [ ] | [ ] | L12 | — | Studio neu starten | `pose_items.tsv` (Header `HS2SANDBOX_POSE_ITEMS`, v5) persistiert; **⚠KK** Item-Respawn via `Info.dicItemLoadInfo` auf KK-Katalog | |
| [ ] | [ ] | [ ] | L13 | Pose via Browser verschoben/umbenannt | — | Item-Keys (relativer Pfad) mitgewandert | |

---

## M. Import / Export (ZIP v2/v3/v5)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | M1 | Kompatible .zip (stored) | **Import…** | Preview-Grid listet Pack-Einträge | |
| [ ] | [ ] | [ ] | M2 | Preview | Thumbnail-Klick / Ctrl/Shift | Toggelt Import-Häkchen wie normale Auswahl | |
| [ ] | [ ] | [ ] | M3 | Preview | Ziel im Baum wählen, Footer **Apply** | Dateien ins Zielverzeichnis geschrieben | |
| [ ] | [ ] | [ ] | M4 | Tree-Pack | Import | Erzeugt Unterordner (Name aus Manifest) | |
| [ ] | [ ] | [ ] | M5 | Preview | **Cancel import** | Zurück zum normalen Grid | |
| [ ] | [ ] | [ ] | M6 | Deflate-ZIP (nicht stored) | Import | **Schlägt fehl** mit Hinweis (nur method 0) | |
| [ ] | [ ] | [ ] | M7 | Volle Gruppe + Layout selektiert | **Export…** | v5 mit `memberRelativeOffsets`/`memberBodyHeights` | |
| [ ] | [ ] | [ ] | M8 | v2-Pack ohne Gruppen | Import | Importiert trotzdem | |
| — | — | [ ] | M9 | **⚠KK** | Export, dann auf KK re-importieren | `MinimalStoredZip` (net35) schreibt/liest korrekt; CRC ok | |
| [ ] | [ ] | [ ] | M10 | Cross-Game | KK-Export → auf HS2/KKS importieren | ZIP-Struktur lädt; **Posen-Binärdaten ggf. nicht 1:1 kompatibel** (dokumentierte Erwartung) | |

---

## N. Speichern, Aktualisieren, Thumbnails

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | N1 | Ordner gewählt | **Save Pose** + Name | Schreibt in gewählten Ordner | |
| [ ] | [ ] | [ ] | N2 | All poses / ★ aktiv | Save Pose | Schreibt in **Pose-Root** | |
| [ ] | [ ] | [ ] | N3 | 1 Pose | **Update Pose** | Aus Szene überschrieben; Thumb behalten/neu | |
| [ ] | [ ] | [ ] | N4 | Auswahl | **Thumbnails…** | Overlay: Capture/Skip/Auto/Cancel; **⚠KK** `Texture2D.EncodeToPNG`/`ReadPixels` Unity 5.6 | |
| [ ] | [ ] | [ ] | N5 | Auto-capture | Lauf | Kettet mit konfigurierbarem Delay | |
| [ ] | [ ] | [ ] | N6 | Capture mid-run Cancel | — | Bereits erfasste bleiben, Rest unverändert | |
| [ ] | [ ] | [ ] | N7 | Erfolg | — | Thumbnails refreshen im Grid | |

---

## O. History (Undo/Redo)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | O1 | Pose angewandt | **Undo** | Vorheriger Pose-Zustand des Charakters | |
| [ ] | [ ] | [ ] | O2 | — | **Redo** | Wiederhergestellt | |
| [ ] | [ ] | [ ] | O3 | History-Pane | öffnen | Per-Charakter-Timeline sichtbar | |
| [ ] | [ ] | [ ] | O4 | Hotkeys Undo/Redo | drücken | Wirkt wie Buttons | |
| [ ] | [ ] | [ ] | O5 | — | Studio neu starten | `pose_browser_history.json` persistiert | |

---

## P. Optionen & Tastenkürzel

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | P1 | Options-Pane | Card-Width / Items-per-page | Wirkt auf Grid; speichert | |
| [ ] | [ ] | [ ] | P2 | — | **Apply stored relative positions** global | Toggle wirkt auf Gruppen-Apply | |
| [ ] | [ ] | [ ] | P3 | — | **Adjust for body height** | Erfordert relative positions an | |
| [ ] | [ ] | [ ] | P4 | — | **Select all filtered** / **Deselect all** | Auswahl im gefilterten Set | |
| [ ] | [ ] | [ ] | P5 | ConfigManager | Hotkeys zuweisen: Next/Prev pose, Next/Prev browse target, Undo/Redo, Toggle stash | Aktiv solange Browser offen, außer IMGUI-Textfeld hat Fokus | |
| [ ] | [ ] | [ ] | P6 | Textfeld fokussiert | Hotkey-Taste tippen | Hotkey **nicht** ausgelöst (Eingabe geht ins Feld) | |
| [ ] | [ ] | [ ] | P7 | — | Options schließen | Speichert über denselben Persistenzpfad | |

---

## Q. Tags/Favoriten-Persistenz & Legacy

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | Q1 | Tags/Favs gesetzt | Studio neu starten | `pose_tags.tsv` lädt korrekt | |
| [ ] | [ ] | [ ] | Q2 | Altes `pose_tags.json` vorhanden | Erststart | Einmalige Migration importiert | |
| [ ] | [ ] | [ ] | Q3 | Move/Rename via Browser | — | Tag-/Group-/Item-Keys aktualisiert (stabile Relativpfade) | |

---

## R. Wiki-Integration (optional)

| HS2 | KKS | KK | ID | Vorbedingung | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | R1 | HS2Wiki installiert | Start | Log „Registered Pose Browser pages…" | |
| [ ] | [ ] | [ ] | R2 | — | Wiki-Taste (F3), Kategorie öffnen | Seiten + Navigation + `pose-icon.png` via OpenImage | |
| [ ] | [ ] | [ ] | R3 | HS2Wiki **nicht** installiert | Start | Kein Fehler, Browser lädt normal (Reflection-Guard) | |
| — | — | [ ] | R4 | **⚠KK** | R1–R3 auf KK | Reflektionsbasierte Registrierung läuft unter net35 ohne Fehler | |

---

## S. Heelz Control (nur HS2 — Abwesenheit verifizieren)

| HS2 | KKS | KK | ID | Schritte | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|
| [ ] | — | — | S1 | **Heelz**-Button / Hotkey | Heelz-Fenster, Tag-Regeln, On/Off/Auto | |
| — | [ ] | — | S2 | UI prüfen | **Kein** Heelz-Button/-Fenster (`#if HS2` aus); keine Fehler | |
| — | — | [ ] | S3 | UI prüfen | **Kein** Heelz; Wiki-Seite „Heelz" fehlt; keine Referenz auf HS2Heelz | |

---

## T. KK-Port-Risikomatrix (gezielte Regressionspunkte) — `[B]`

> Diese Fälle zielen direkt auf die net35-/Unity-5.6-Umbauten. KK = Pflicht; HS2/KKS = Baseline-Vergleich.

| HS2 | KKS | KK | ID | Bereich | Test | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|---|
| [ ] | [ ] | [ ] | T1 | `Array.Empty`→`new T[0]` | Leere Suchergebnisse, leere Gruppen, leere Item-Listen rendern | Kein `MissingMethodException`; leere Collections ok | |
| [ ] | [ ] | [ ] | T2 | `IReadOnly*`→`IList/...` | Multi-Apply, Gruppen-Apply, Grid-Layout (`PoseBrowserCharacterApply`, `PoseBrowserGridLayout`) | Funktional identisch zu HS2/KKS | |
| [ ] | [ ] | [ ] | T3 | `StringEx.IsNullOrWhiteSpace` | Leere/Whitespace-Namen bei Save/Rename/Tag/Suche | Korrekt als leer behandelt | |
| [ ] | [ ] | [ ] | T4 | `PathEx.Combine` (3-arg) | Config-/Pose-Pfade bauen | Pfade korrekt zusammengesetzt | |
| [ ] | [ ] | [ ] | T5 | `RegexEx` (kein `Compiled`) | Regex-Suche (D2) | Funktioniert, kein Mono-Crash | |
| [ ] | [ ] | [ ] | T6 | ValueTuple→`PoseBrowserPairs` | Multi-Apply-Zuordnung, ZIP-Parts, Item-Frames | Zuordnung korrekt (Struct-Pairs) | |
| [ ] | [ ] | [ ] | T7 | UnityWebRequest (alt) | Update-Check (`req.Send`/`req.error`) | Online-Check liefert Version/kein Crash | |
| [ ] | [ ] | [ ] | T8 | changeAmount-Guide | Pose-Apply Positionsstabilität (F3) | Charakter springt nicht | |
| [ ] | [ ] | [ ] | T9 | Simple Color | Gruppen-Thumbs monocolor (I3) | Monocolor-Render korrekt auf Unity 5.6 | |
| [ ] | [ ] | [ ] | T10 | PNG-Capture | Thumbnail schreiben (N4) | Gültige PNG in Pose-Datei | |
| [ ] | [ ] | [ ] | T11 | net35 Threads/Coroutines | Lange Capture-/Import-Läufe | Keine Hänger/Exceptions | |
| — | [ ] | [ ] | T12 | AIChara-Abwesenheit | Charakter-bezogene Pfade (Apply, Item body-height) | Kompiliert/läuft ohne `AIChara` (global ChaControl) | |

---

## U. Cross-Game-Regression (HS2 & KKS)

| HS2 | KKS | KK | ID | Test | Erwartet | Kommentar |
|:--:|:--:|:--:|---|---|---|---|
| [ ] | — | — | U1 | HS2 PoseBrowser: Smoke + Kernpfade (A,F,G,H,L,N) | Keine Regression durch geteilte net35-Refactors | |
| — | [ ] | — | U2 | KKS PoseBrowser: Smoke + Kernpfade | Keine Regression | |
| [ ] | — | — | U3 | HS2 weitere Module (Timeline, SonScale, CopyScript, Notebook, …) bauen+laden | Root-Props-Entkopplung (IronPython/System.Memory) brach nichts | |

---

## V. Abnahmekriterien

| HS2 | KKS | KK | ID | Kriterium | Kommentar |
|:--:|:--:|:--:|---|---|---|
| [ ] | [ ] | [ ] | V1 | Alle A–S-Fälle grün (oder dokumentierte Abweichung) | |
| [ ] | [ ] | [ ] | V2 | T-Risikomatrix vollständig, kein net35-/Unity-5.6-Regress | |
| [ ] | [ ] | — | V3 | U-Regression grün | |
| [ ] | [ ] | [ ] | V4 | Keine unbehandelten Exceptions in `LogOutput.log` über eine volle Test-Session | |
| [ ] | [ ] | [ ] | V5 | Persistenzdateien (`pose_*.tsv/json`) nach Neustart konsistent | |

---

### Anhang: Persistenzdateien (für Verifikation)

`BepInEx/config/com.hs2.sandbox/` — `pose_tags.tsv`, `pose_groups.tsv`, `pose_items.tsv`, `pose_browser_options.json`, `pose_browser_character_config.json`, `pose_stash.json`, `pose_browser_history.json`, `pose_browser_filter_presets.json`.

### Anhang: Bekannte erwartete Grenzen

- Pose-/Studio-**Binärformate** sind zwischen KK (Unity 5.6) und HS2/KKS nicht garantiert austauschbar — ZIP-Struktur ja, eingebettete Pose-Blobs ggf. nicht (M10).
- Heelz Control existiert nur auf HS2 (S2/S3).
