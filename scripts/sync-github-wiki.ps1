# Sync wiki/ sources to GitHub Wiki repository.
#
# Usage:
#   .\scripts\sync-github-wiki.ps1
#   .\scripts\sync-github-wiki.ps1 -RemoteUrl https://github.com/OWNER/REPO.wiki.git
#   .\scripts\sync-github-wiki.ps1 -Init    # first-time setup when wiki git repo does not exist yet
#
# Prerequisites:
#   1. Repo Settings -> Features -> Wikis = enabled
#   2. Push access to the wiki repository
#   For -Init on a brand-new wiki: enable Wikis first, then run -Init (creates local clone + first push).

param(
    [string]$RemoteUrl = "",
    [string]$Message = "Sync wiki from main repository",
    [switch]$Init
)

$ErrorActionPreference = "Stop"

function Get-WikiRemoteFromOrigin {
    param([string]$RepoRoot)
    Push-Location $RepoRoot
    try {
        $origin = git remote get-url origin 2>$null
        if (-not $origin) { return $null }

        if ($origin -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+)(?:\.git)?/?$') {
            $owner = $Matches['owner']
            $repo = $Matches['repo'] -replace '\.git$', ''
            return "https://github.com/$owner/$repo.wiki.git"
        }
        return $null
    } finally {
        Pop-Location
    }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$GitArgs,
        [string]$WorkingDirectory = $null
    )

    if ($WorkingDirectory) { Push-Location $WorkingDirectory }
    try {
        & git @GitArgs
        if ($LASTEXITCODE -ne 0) {
            throw "git $($GitArgs -join ' ') failed with exit code $LASTEXITCODE"
        }
    } finally {
        if ($WorkingDirectory) { Pop-Location }
    }
}

function Remove-WikiCloneIfBroken {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return }
    $gitDir = Join-Path $Path ".git"
    if (-not (Test-Path $gitDir)) {
        Remove-Item -Path $Path -Recurse -Force
    }
}

function Write-WikiSetupHelp {
    param([string]$Url)
    Write-Host ""
    Write-Host "The GitHub Wiki git repository was not found:" -ForegroundColor Yellow
    Write-Host "  $Url" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Do this once on GitHub:" -ForegroundColor Cyan
    Write-Host "  1. Open repo Settings -> General -> Features"
    Write-Host "  2. Enable Wikis"
    Write-Host '  3. Run:  .\scripts\sync-github-wiki.ps1 -Init'
    Write-Host ""
    Write-Host "If Wikis are already enabled but you never pushed, -Init creates the wiki repo on first push."
    Write-Host "If the repo is private, authenticate git (gh auth login / credential manager) before syncing."
    Write-Host ""
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$wikiSource = Join-Path $repoRoot "wiki"
$wikiClone = Join-Path $repoRoot ".wiki-clone"

if (-not (Test-Path $wikiSource)) {
    Write-Error "Wiki source folder not found: $wikiSource"
}

$wikiFiles = Get-ChildItem -Path $wikiSource -Recurse -File -Force |
    Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]?' }
if ($wikiFiles.Count -eq 0) {
    Write-Error "No files found under $wikiSource"
}

if ([string]::IsNullOrWhiteSpace($RemoteUrl)) {
    $RemoteUrl = Get-WikiRemoteFromOrigin -RepoRoot $repoRoot
    if (-not $RemoteUrl) {
        Write-Error "Could not derive wiki URL from git origin. Pass -RemoteUrl explicitly."
    }
    Write-Host "Wiki remote: $RemoteUrl"
}

$cloneOk = $false

if (Test-Path (Join-Path $wikiClone ".git")) {
    Write-Host "Updating wiki clone..."
    try {
        Invoke-Git -GitArgs @("pull", "--rebase") -WorkingDirectory $wikiClone
        $cloneOk = $true
    } catch {
        Write-Warning "Pull failed; will try fresh clone."
        Remove-Item -Path $wikiClone -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if (-not $cloneOk) {
    Remove-WikiCloneIfBroken -Path $wikiClone
    Write-Host "Cloning wiki repository..."
    try {
        Invoke-Git -GitArgs @("clone", $RemoteUrl, $wikiClone)
        $cloneOk = $true
    } catch {
        Remove-WikiCloneIfBroken -Path $wikiClone
        if ($Init) {
            Write-Host 'Clone failed - initializing new local wiki clone (-Init)...'
            New-Item -ItemType Directory -Path $wikiClone -Force | Out-Null
            Invoke-Git -GitArgs @("init") -WorkingDirectory $wikiClone
            Invoke-Git -GitArgs @("branch", "-M", "master") -WorkingDirectory $wikiClone
            Invoke-Git -GitArgs @("remote", "add", "origin", $RemoteUrl) -WorkingDirectory $wikiClone
            $cloneOk = $true
        } else {
            Write-WikiSetupHelp -Url $RemoteUrl
            Write-Error 'Wiki clone failed. Enable Wikis on GitHub, then re-run with -Init.'
        }
    }
}

if (-not $cloneOk -or -not (Test-Path $wikiClone)) {
    Write-Error "Wiki clone directory is missing after setup."
}

Write-Host "Syncing wiki tree..."
Get-ChildItem -Path $wikiClone -Force |
    Where-Object { $_.Name -ne '.git' } |
    Remove-Item -Recurse -Force

foreach ($file in $wikiFiles) {
    $relative = $file.FullName.Substring($wikiSource.Length).TrimStart('\', '/')
    $dest = Join-Path $wikiClone $relative
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item -Path $file.FullName -Destination $dest -Force
}

Push-Location $wikiClone
try {
    Invoke-Git -GitArgs @("add", "-A")
    $status = git status --porcelain
    if ([string]::IsNullOrWhiteSpace($status)) {
        Write-Host "Wiki is already up to date."
    } else {
        Invoke-Git -GitArgs @("commit", "-m", $Message)
        try {
            Invoke-Git -GitArgs @("push", "-u", "origin", "master")
            Write-Host "Wiki pushed successfully."
        } catch {
            Write-WikiSetupHelp -Url $RemoteUrl
            Write-Error 'Push failed. See steps above (enable Wikis, authenticate, try -Init).'
        }
    }
} finally {
    Pop-Location
}
