# Fix GitHub Wiki links: use basename-only slugs (GitHub ignores subdirs in URLs).
# See: https://github.com/tajmone/github-tests/wiki/subfolders

$ErrorActionPreference = "Stop"
$wikiRoot = Join-Path (Split-Path -Parent $PSScriptRoot) "wiki"

$renames = @{
    "pose-browser/Home.md" = "pose-browser/Pose-Browser.md"
    "anim-browser/Home.md" = "anim-browser/Anim-Browser.md"
}

foreach ($entry in $renames.GetEnumerator()) {
    $src = Join-Path $wikiRoot $entry.Key
    $dest = Join-Path $wikiRoot $entry.Value
    if (Test-Path $src) {
        Move-Item $src $dest -Force
        Write-Host "Renamed $($entry.Key) -> $($entry.Value)"
    }
}

# Longest paths first — replace (folder/page) with (Page-Basename)
$linkMap = [ordered]@{
    "getting-started/All-in-One-vs-Split-Modules" = "All-in-One-vs-Split-Modules"
    "getting-started/Supported-Games-and-Modules" = "Supported-Games-and-Modules"
    "pose-browser/Search-Filters-and-Sort"        = "Search-Filters-and-Sort"
    "pose-browser/Folders-and-Library"              = "Folders-and-Library"
    "pose-browser/Multi-Character-Apply"          = "Multi-Character-Apply"
    "pose-browser/Import-Export-ZIP"                = "Import-Export-ZIP"
    "pose-browser/Grid-and-Selection"               = "Grid-and-Selection"
    "anim-browser/Characters-and-Options"           = "Characters-and-Options"
    "anim-browser/Merging-Categories"               = "Merging-Categories"
    "anim-browser/Applying-Animations"              = "Applying-Animations"
    "anim-browser/Browsing-and-Search"              = "Browsing-and-Search"
    "anim-browser/Playback-Controls"                = "Playback-Controls"
    "anim-browser/Getting-Started"                  = "Getting-Started"
    "anim-browser/Review-Panel"                     = "Review-Panel"
    "reference/Configuration-and-Data-Files"        = "Configuration-and-Data-Files"
    "developers/Contributing-and-CI"                = "Contributing-and-CI"
    "developers/Building-from-Source"               = "Building-from-Source"
    "reference/Plugin-Compatibility"                = "Plugin-Compatibility"
    "reference/Keyboard-Shortcuts"                  = "Keyboard-Shortcuts"
    "reference/Pose-ZIP-Format"                     = "Pose-ZIP-Format"
    "hs2/Timeline-Commands-Reference"               = "Timeline-Commands-Reference"
    "getting-started/Troubleshooting"               = "Troubleshooting"
    "getting-started/Requirements"                  = "Requirements"
    "getting-started/Installation"                  = "Installation"
    "pose-browser/Options-and-Data"                 = "Options-and-Data"
    "pose-browser/Thumbnails"                       = "Thumbnails"
    "hs2/SearchBarManager"                          = "SearchBarManager"
    "hs2/Workspace-Tree-Lock"                       = "Workspace-Tree-Lock"
    "pose-browser/Home"                             = "Pose-Browser"
    "anim-browser/Home"                             = "Anim-Browser"
    "anim-browser/Grouping"                         = "Grouping"
    "pose-browser/Groups"                           = "Groups"
    "pose-browser/Stash"                            = "Stash"
    "pose-browser/Items"                            = "Items"
    "hs2/CopyScript"                                = "CopyScript"
    "developers/Architecture"                       = "Architecture"
    "hs2/Son-Scale"                                 = "Son-Scale"
    "hs2/Notebook"                                  = "Notebook"
    "hs2/Timeline"                                  = "Timeline"
}

$mdFiles = Get-ChildItem -Path $wikiRoot -Filter "*.md" -Recurse -File
foreach ($file in $mdFiles) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $original = $content
    foreach ($entry in $linkMap.GetEnumerator()) {
        $content = $content -replace "\($([regex]::Escape($entry.Key))\)", "($($entry.Value))"
    }
    if ($content -ne $original) {
        Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
        Write-Host "Links: $($file.FullName.Substring($wikiRoot.Length + 1))"
    }
}

Write-Host "Done. GitHub Wiki links must use basenames only, e.g. [Pose Browser](Pose-Browser)"
