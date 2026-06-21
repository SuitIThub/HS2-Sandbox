# One-time helper: reorganize wiki/ into module folders and rewrite links.
# Safe to re-run only on a clean flat layout; already migrated trees are skipped for moves.

$ErrorActionPreference = "Stop"
$wikiRoot = Join-Path (Split-Path -Parent $PSScriptRoot) "wiki"

$dirs = @(
    "getting-started", "hs2", "pose-browser", "anim-browser", "reference", "developers"
)
foreach ($d in $dirs) {
    New-Item -ItemType Directory -Path (Join-Path $wikiRoot $d) -Force | Out-Null
}

$moves = @{
    "Requirements.md"                        = "getting-started/Requirements.md"
    "Installation.md"                        = "getting-started/Installation.md"
    "All-in-One-vs-Split-Modules.md"         = "getting-started/All-in-One-vs-Split-Modules.md"
    "Supported-Games-and-Modules.md"         = "getting-started/Supported-Games-and-Modules.md"
    "Troubleshooting.md"                       = "getting-started/Troubleshooting.md"
    "CopyScript.md"                          = "hs2/CopyScript.md"
    "Timeline.md"                            = "hs2/Timeline.md"
    "Timeline-Commands-Reference.md"         = "hs2/Timeline-Commands-Reference.md"
    "SearchBarManager.md"                    = "hs2/SearchBarManager.md"
    "Son-Scale.md"                           = "hs2/Son-Scale.md"
    "Workspace-Tree-Lock.md"                 = "hs2/Workspace-Tree-Lock.md"
    "Notebook.md"                            = "hs2/Notebook.md"
    "Pose-Browser.md"                        = "pose-browser/Home.md"
    "Pose-Browser-Folders-and-Library.md"    = "pose-browser/Folders-and-Library.md"
    "Pose-Browser-Search-Filters-and-Sort.md" = "pose-browser/Search-Filters-and-Sort.md"
    "Pose-Browser-Grid-and-Selection.md"     = "pose-browser/Grid-and-Selection.md"
    "Pose-Browser-Groups.md"                 = "pose-browser/Groups.md"
    "Pose-Browser-Multi-Character-Apply.md"  = "pose-browser/Multi-Character-Apply.md"
    "Pose-Browser-Stash.md"                  = "pose-browser/Stash.md"
    "Pose-Browser-Items.md"                  = "pose-browser/Items.md"
    "Pose-Browser-Import-Export-ZIP.md"      = "pose-browser/Import-Export-ZIP.md"
    "Pose-Browser-Thumbnails.md"             = "pose-browser/Thumbnails.md"
    "Pose-Browser-Options-and-Data.md"       = "pose-browser/Options-and-Data.md"
    "Anim-Browser.md"                        = "anim-browser/Home.md"
    "Anim-Browser-Getting-Started.md"        = "anim-browser/Getting-Started.md"
    "Anim-Browser-Browsing-and-Search.md"    = "anim-browser/Browsing-and-Search.md"
    "Anim-Browser-Applying-Animations.md"    = "anim-browser/Applying-Animations.md"
    "Anim-Browser-Playback-Controls.md"      = "anim-browser/Playback-Controls.md"
    "Anim-Browser-Grouping.md"               = "anim-browser/Grouping.md"
    "Anim-Browser-Merging-Categories.md"     = "anim-browser/Merging-Categories.md"
    "Anim-Browser-Review-Panel.md"           = "anim-browser/Review-Panel.md"
    "Anim-Browser-Characters-and-Options.md" = "anim-browser/Characters-and-Options.md"
    "Configuration-and-Data-Files.md"        = "reference/Configuration-and-Data-Files.md"
    "Keyboard-Shortcuts.md"                  = "reference/Keyboard-Shortcuts.md"
    "Plugin-Compatibility.md"                = "reference/Plugin-Compatibility.md"
    "Pose-ZIP-Format.md"                     = "reference/Pose-ZIP-Format.md"
    "Architecture.md"                        = "developers/Architecture.md"
    "Building-from-Source.md"                = "developers/Building-from-Source.md"
    "Contributing-and-CI.md"                 = "developers/Contributing-and-CI.md"
}

foreach ($entry in $moves.GetEnumerator()) {
    $src = Join-Path $wikiRoot $entry.Key
    $dest = Join-Path $wikiRoot $entry.Value
    if (Test-Path $src) {
        Move-Item -Path $src -Destination $dest -Force
        Write-Host "Moved $($entry.Key) -> $($entry.Value)"
    }
}

# Longest-first link rewrites (markdown wiki links only)
$linkMap = [ordered]@{
    "Pose-Browser-Options-and-Data"       = "pose-browser/Options-and-Data"
    "Pose-Browser-Import-Export-ZIP"      = "pose-browser/Import-Export-ZIP"
    "Pose-Browser-Multi-Character-Apply"  = "pose-browser/Multi-Character-Apply"
    "Pose-Browser-Search-Filters-and-Sort" = "pose-browser/Search-Filters-and-Sort"
    "Pose-Browser-Folders-and-Library"    = "pose-browser/Folders-and-Library"
    "Pose-Browser-Grid-and-Selection"     = "pose-browser/Grid-and-Selection"
    "Pose-Browser-Thumbnails"             = "pose-browser/Thumbnails"
    "Pose-Browser-Groups"                 = "pose-browser/Groups"
    "Pose-Browser-Stash"                  = "pose-browser/Stash"
    "Pose-Browser-Items"                  = "pose-browser/Items"
    "Pose-Browser"                        = "pose-browser/Home"
    "Anim-Browser-Characters-and-Options" = "anim-browser/Characters-and-Options"
    "Anim-Browser-Merging-Categories"     = "anim-browser/Merging-Categories"
    "Anim-Browser-Applying-Animations"    = "anim-browser/Applying-Animations"
    "Anim-Browser-Browsing-and-Search"    = "anim-browser/Browsing-and-Search"
    "Anim-Browser-Playback-Controls"      = "anim-browser/Playback-Controls"
    "Anim-Browser-Getting-Started"        = "anim-browser/Getting-Started"
    "Anim-Browser-Review-Panel"           = "anim-browser/Review-Panel"
    "Anim-Browser-Grouping"               = "anim-browser/Grouping"
    "Anim-Browser"                        = "anim-browser/Home"
    "All-in-One-vs-Split-Modules"         = "getting-started/All-in-One-vs-Split-Modules"
    "Supported-Games-and-Modules"         = "getting-started/Supported-Games-and-Modules"
    "Timeline-Commands-Reference"         = "hs2/Timeline-Commands-Reference"
    "Workspace-Tree-Lock"                 = "hs2/Workspace-Tree-Lock"
    "Configuration-and-Data-Files"        = "reference/Configuration-and-Data-Files"
    "Contributing-and-CI"                 = "developers/Contributing-and-CI"
    "Building-from-Source"                = "developers/Building-from-Source"
    "Keyboard-Shortcuts"                  = "reference/Keyboard-Shortcuts"
    "Plugin-Compatibility"                = "reference/Plugin-Compatibility"
    "SearchBarManager"                    = "hs2/SearchBarManager"
    "Pose-ZIP-Format"                     = "reference/Pose-ZIP-Format"
    "Troubleshooting"                     = "getting-started/Troubleshooting"
    "Requirements"                        = "getting-started/Requirements"
    "Installation"                        = "getting-started/Installation"
    "CopyScript"                          = "hs2/CopyScript"
    "Architecture"                        = "developers/Architecture"
    "Son-Scale"                           = "hs2/Son-Scale"
    "Notebook"                            = "hs2/Notebook"
    "Timeline"                            = "hs2/Timeline"
}

$mdFiles = Get-ChildItem -Path $wikiRoot -Filter "*.md" -Recurse -File
foreach ($file in $mdFiles) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $original = $content
    foreach ($entry in $linkMap.GetEnumerator()) {
        $old = $entry.Key
        $new = $entry.Value
        $content = $content -replace "\($([regex]::Escape($old))\)", "($new)"
    }
    if ($content -ne $original) {
        Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
        Write-Host "Updated links in $($file.FullName.Substring($wikiRoot.Length + 1))"
    }
}

Write-Host "Done."
