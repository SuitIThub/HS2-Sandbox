# Sync wiki/ sources to GitHub Wiki repository.
# Usage: .\scripts\sync-github-wiki.ps1 [-RemoteUrl <url>] [-Message <commit message>]

param(
    [string]$RemoteUrl = "https://github.com/SuitIThub/HS2-Sandbox.wiki.git",
    [string]$Message = "Sync wiki from main repository"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$wikiSource = Join-Path $repoRoot "wiki"
$wikiClone = Join-Path $repoRoot ".wiki-clone"

if (-not (Test-Path $wikiSource)) {
    Write-Error "Wiki source folder not found: $wikiSource"
}

$mdFiles = Get-ChildItem -Path $wikiSource -Filter "*.md" -File
if ($mdFiles.Count -eq 0) {
    Write-Error "No markdown files in $wikiSource"
}

if (-not (Test-Path $wikiClone)) {
    Write-Host "Cloning wiki repository..."
    git clone $RemoteUrl $wikiClone
} else {
    Write-Host "Updating wiki clone..."
    Push-Location $wikiClone
    git pull --rebase
    Pop-Location
}

Write-Host "Copying wiki pages..."
foreach ($file in $mdFiles) {
    Copy-Item -Path $file.FullName -Destination (Join-Path $wikiClone $file.Name) -Force
}

Push-Location $wikiClone
git add -A
$status = git status --porcelain
if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host "Wiki is already up to date."
} else {
    git commit -m $Message
    git push
    Write-Host "Wiki pushed successfully."
}
Pop-Location
