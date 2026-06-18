Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Stop-BuildFlow {
    param(
        [string]$Message = "Build flow stopped."
    )

    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
    Read-Host "Press Enter to continue"
    exit 1
}

function Read-YesNo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt
    )

    while ($true) {
        $response = Read-Host "$Prompt [Y/N]"
        if ($null -eq $response) {
            Stop-BuildFlow "Cancelled."
        }

        switch ($response.Trim().ToLowerInvariant()) {
            "y" { return $true }
            "yes" { return $true }
            "n" { return $false }
            "no" { return $false }
            "" { Stop-BuildFlow "Cancelled." }
            default {
                Write-Host "Please enter Y or N." -ForegroundColor Yellow
            }
        }
    }
}

function Read-OverwriteOrDeactivate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt
    )

    while ($true) {
        $response = Read-Host "$Prompt [O]verwrite / [D]eactivate / [C]ancel"
        if ($null -eq $response) {
            Stop-BuildFlow "Cancelled."
        }

        switch ($response.Trim().ToLowerInvariant()) {
            "o" { return "overwrite" }
            "overwrite" { return "overwrite" }
            "d" { return "deactivate" }
            "deactivate" { return "deactivate" }
            "c" { Stop-BuildFlow "Cancelled." }
            "cancel" { Stop-BuildFlow "Cancelled." }
            "" { Stop-BuildFlow "Cancelled." }
            default {
                Write-Host "Please enter O, D, or C." -ForegroundColor Yellow
            }
        }
    }
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

$gameConfig = @{
    HS2 = @{
        DeployDir = "D:\Honey Select\BepInEx\plugins\HS2-Sandbox"
        StudioExe = "D:\Honey Select\StudioNEOV2.exe"
        StudioProcessNames = @("StudioNeoV2", "HoneySelect2")
        StudioLabel = "StudioNEOV2"
    }
    KKS = @{
        DeployDir = "D:\Games\Koikatsu Sunshine EX BetterRepack R12\BepInEx\plugins\KKS-Sandbox"
        StudioExe = "D:\Games\Koikatsu Sunshine EX BetterRepack R12\CharaStudio.exe"
        StudioProcessNames = @("CharaStudio", "KoikatsuSunshine")
        StudioLabel = "CharaStudio"
    }
    KK = @{
        DeployDir = "D:\Games\Koikatsu BetterRepack RX21\BepInEx\plugins\KK-Sandbox"
        StudioExe = "D:\Games\Koikatsu BetterRepack RX21\CharaStudio.exe"
        StudioProcessNames = @("CharaStudio", "Koikatsu")
        StudioLabel = "CharaStudio"
    }
}

function Read-MultiChoice {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt,
        [Parameter(Mandatory = $true)]
        [string[]]$Options
    )

    while ($true) {
        Write-Host ""
        Write-Host $Prompt -ForegroundColor Cyan
        for ($i = 0; $i -lt $Options.Count; $i++) {
            $n = $i + 1
            Write-Host "  [$n] $($Options[$i])"
        }
        Write-Host "  [A] All"
        Write-Host "  [C] Cancel"
        $response = Read-Host "Choose (comma-separated, e.g. 1,3,5)"
        if ($null -eq $response) { Stop-BuildFlow "Cancelled." }
        $trimmed = $response.Trim()
        if ($trimmed -eq "" -or $trimmed.ToLowerInvariant() -eq "c" -or $trimmed.ToLowerInvariant() -eq "cancel") {
            Stop-BuildFlow "Cancelled."
        }
        if ($trimmed.ToLowerInvariant() -eq "a" -or $trimmed.ToLowerInvariant() -eq "all") {
            return @(0..($Options.Count - 1))
        }

        $parts = $trimmed -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
        $indices = @()
        $valid = $true
        foreach ($part in $parts) {
            $idx = 0
            if ([int]::TryParse($part, [ref]$idx)) {
                if ($idx -ge 1 -and $idx -le $Options.Count) {
                    $indices += ($idx - 1)
                } else {
                    $valid = $false
                    break
                }
            } else {
                $valid = $false
                break
            }
        }

        if ($valid -and $indices.Count -gt 0) {
            return @($indices | Select-Object -Unique)
        }
        Write-Host "Please enter 1-$($Options.Count) (comma-separated), A for all, or C to cancel." -ForegroundColor Yellow
    }
}

$gameLabels = @{
    HS2 = "Honey Select 2 (HS2)"
    KKS = "Koikatsu Sunshine (KKS)"
    KK  = "Koikatsu (KK)"
}

$moduleCatalog = @(
    @{
        Key = "CopyScript"
        DisplayName = "CopyScript (HS2Sandbox.CopyScript.dll)"
        Games = @("HS2")
        Targets = @{
            HS2 = @{
                BuildPath = "targets\HS2\CopyScript\HS2Sandbox.CopyScript.csproj"
                BuiltDllRelPath = "targets\HS2\CopyScript\bin\Release\HS2Sandbox.CopyScript.dll"
                DeployFileName = "HS2Sandbox.CopyScript.dll"
                DeactivatedFileName = "HS2Sandbox.CopyScript.dl_"
            }
        }
    },
    @{
        Key = "Timeline"
        DisplayName = "Timeline (HS2Sandbox.Timeline.dll)"
        Games = @("HS2")
        Targets = @{
            HS2 = @{
                BuildPath = "targets\HS2\Timeline\HS2Sandbox.Timeline.csproj"
                BuiltDllRelPath = "targets\HS2\Timeline\bin\Release\HS2Sandbox.Timeline.dll"
                DeployFileName = "HS2Sandbox.Timeline.dll"
                DeactivatedFileName = "HS2Sandbox.Timeline.dl_"
            }
        }
    },
    @{
        Key = "SearchBarManager"
        DisplayName = "SearchBarManager (HS2Sandbox.SearchBarManager.dll)"
        Games = @("HS2")
        Targets = @{
            HS2 = @{
                BuildPath = "targets\HS2\SearchBarManager\HS2Sandbox.SearchBarManager.csproj"
                BuiltDllRelPath = "targets\HS2\SearchBarManager\bin\Release\HS2Sandbox.SearchBarManager.dll"
                DeployFileName = "HS2Sandbox.SearchBarManager.dll"
                DeactivatedFileName = "HS2Sandbox.SearchBarManager.dl_"
            }
        }
    },
    @{
        Key = "SonScale"
        DisplayName = "SonScale (HS2Sandbox.SonScale.dll)"
        Games = @("HS2")
        Targets = @{
            HS2 = @{
                BuildPath = "targets\HS2\SonScale\HS2Sandbox.SonScale.csproj"
                BuiltDllRelPath = "targets\HS2\SonScale\bin\Release\HS2Sandbox.SonScale.dll"
                DeployFileName = "HS2Sandbox.SonScale.dll"
                DeactivatedFileName = "HS2Sandbox.SonScale.dl_"
            }
        }
    },
    @{
        Key = "WorkspaceTreeLock"
        DisplayName = "WorkspaceTreeLock (HS2Sandbox.WorkspaceTreeLock.dll)"
        Games = @("HS2")
        Targets = @{
            HS2 = @{
                BuildPath = "targets\HS2\WorkspaceTreeLock\HS2Sandbox.WorkspaceTreeLock.csproj"
                BuiltDllRelPath = "targets\HS2\WorkspaceTreeLock\bin\Release\HS2Sandbox.WorkspaceTreeLock.dll"
                DeployFileName = "HS2Sandbox.WorkspaceTreeLock.dll"
                DeactivatedFileName = "HS2Sandbox.WorkspaceTreeLock.dl_"
            }
        }
    },
    @{
        Key = "Notebook"
        DisplayName = "Notebook (HS2Sandbox.Notebook.dll)"
        Games = @("HS2")
        Targets = @{
            HS2 = @{
                BuildPath = "targets\HS2\Notebook\HS2Sandbox.Notebook.csproj"
                BuiltDllRelPath = "targets\HS2\Notebook\bin\Release\HS2Sandbox.Notebook.dll"
                DeployFileName = "HS2Sandbox.Notebook.dll"
                DeactivatedFileName = "HS2Sandbox.Notebook.dl_"
            }
        }
    },
    @{
        Key = "PoseBrowser"
        DisplayName = "PoseBrowser"
        Games = @("HS2", "KKS", "KK")
        Targets = @{
            HS2 = @{
                BuildPath = "targets\HS2\PoseBrowser\HS2Sandbox.PoseBrowser.csproj"
                BuiltDllRelPath = "targets\HS2\PoseBrowser\bin\Release\HS2Sandbox.PoseBrowser.dll"
                DeployFileName = "HS2Sandbox.PoseBrowser.dll"
                DeactivatedFileName = "HS2Sandbox.PoseBrowser.dl_"
            }
            KKS = @{
                BuildPath = "targets\KKS\PoseBrowser\KKSSandbox.PoseBrowser.csproj"
                BuiltDllRelPath = "targets\KKS\PoseBrowser\bin\Release\KKSSandbox.PoseBrowser.dll"
                DeployFileName = "KKSSandbox.PoseBrowser.dll"
                DeactivatedFileName = "KKSSandbox.PoseBrowser.dl_"
            }
            KK = @{
                BuildPath = "targets\KK\PoseBrowser\KKSandbox.PoseBrowser.csproj"
                BuiltDllRelPath = "targets\KK\PoseBrowser\bin\Release\KKSandbox.PoseBrowser.dll"
                DeployFileName = "KKSandbox.PoseBrowser.dll"
                DeactivatedFileName = "KKSandbox.PoseBrowser.dl_"
            }
        }
    },
    @{
        Key = "AnimBrowser"
        DisplayName = "AnimBrowser"
        Games = @("HS2", "KKS", "KK")
        Targets = @{
            HS2 = @{
                BuildPath = "targets\HS2\AnimBrowser\HS2Sandbox.AnimBrowser.csproj"
                BuiltDllRelPath = "targets\HS2\AnimBrowser\bin\Release\HS2Sandbox.AnimBrowser.dll"
                DeployFileName = "HS2Sandbox.AnimBrowser.dll"
                DeactivatedFileName = "HS2Sandbox.AnimBrowser.dl_"
            }
            KKS = @{
                BuildPath = "targets\KKS\AnimBrowser\KKSSandbox.AnimBrowser.csproj"
                BuiltDllRelPath = "targets\KKS\AnimBrowser\bin\Release\KKSSandbox.AnimBrowser.dll"
                DeployFileName = "KKSSandbox.AnimBrowser.dll"
                DeactivatedFileName = "KKSSandbox.AnimBrowser.dl_"
            }
            KK = @{
                BuildPath = "targets\KK\AnimBrowser\KKSandbox.AnimBrowser.csproj"
                BuiltDllRelPath = "targets\KK\AnimBrowser\bin\Release\KKSandbox.AnimBrowser.dll"
                DeployFileName = "KKSandbox.AnimBrowser.dll"
                DeactivatedFileName = "KKSandbox.AnimBrowser.dl_"
            }
        }
    }
)

function Get-ModuleMenuLabel {
    param($Module)

    if ($Module.Games.Count -eq 1) {
        return "$($Module.DisplayName) [$($gameLabels[$Module.Games[0]])]"
    }

    $gameList = ($Module.Games | ForEach-Object { $gameLabels[$_] }) -join ", "
    return "$($Module.DisplayName) ($gameList)"
}

function Expand-SelectedTargets {
    param(
        [array]$SelectedModules,
        [string[]]$SelectedGameKeys
    )

    $result = @()
    foreach ($module in $SelectedModules) {
        $gamesToBuild = if ($module.Games.Count -eq 1) {
            @($module.Games[0])
        } else {
            @($SelectedGameKeys | Where-Object { $module.Games -contains $_ })
        }

        foreach ($game in $gamesToBuild) {
            $targetInfo = $module.Targets[$game]
            $result += @{
                Key = if ($module.Games.Count -eq 1) { $module.Key } else { "$($module.Key)-$game" }
                ModuleKey = $module.Key
                Game = $game
                DisplayName = "[$game] $($module.DisplayName) ($($targetInfo.DeployFileName))"
                BuildPath = $targetInfo.BuildPath
                BuiltDllRelPath = $targetInfo.BuiltDllRelPath
                DeployFileName = $targetInfo.DeployFileName
                DeactivatedFileName = $targetInfo.DeactivatedFileName
            }
        }
    }

    return $result
}

$moduleMenuLabels = $moduleCatalog | ForEach-Object { Get-ModuleMenuLabel $_ }
$moduleChoiceIndices = Read-MultiChoice "Which module(s) do you want to build/deploy?" $moduleMenuLabels
$selectedModules = @($moduleChoiceIndices | ForEach-Object { $moduleCatalog[$_] })

$multiGameModules = @($selectedModules | Where-Object { $_.Games.Count -gt 1 })
$selectedGameKeys = @()

if ($multiGameModules.Count -gt 0) {
    $availableGameKeys = @(
        $multiGameModules |
            ForEach-Object { $_.Games } |
            Select-Object -Unique
    )
    $availableGameKeys = @(
        @("HS2", "KKS", "KK") | Where-Object { $availableGameKeys -contains $_ }
    )

    $moduleNames = ($multiGameModules | ForEach-Object { $_.DisplayName }) -join ", "
    $gameMenuLabels = $availableGameKeys | ForEach-Object { $gameLabels[$_] }
    $gameChoiceIndices = Read-MultiChoice "Which game(s) for: $moduleNames ?" $gameMenuLabels
    $selectedGameKeys = @($gameChoiceIndices | ForEach-Object { $availableGameKeys[$_] })

    if ($selectedGameKeys.Count -eq 0) {
        Stop-BuildFlow "No games selected for multi-game module(s)."
    }
}

$selectedTargets = Expand-SelectedTargets -SelectedModules $selectedModules -SelectedGameKeys $selectedGameKeys

if ($selectedTargets.Count -eq 0) {
    Stop-BuildFlow "No build targets resolved from your selection."
}

Write-Host ""
Write-Host "Selected build target(s):" -ForegroundColor Cyan
foreach ($target in $selectedTargets) {
    Write-Host "  - $($target.DisplayName)"
}

Write-Host ""
Write-Host "Building $($selectedTargets.Count) module(s)..." -ForegroundColor Cyan
Push-Location $repoRoot

try {
    # --- Build all selected modules ---
    $builtDlls = @()
    foreach ($target in $selectedTargets) {
        Write-Host ""
        Write-Host "Building: $($target.DisplayName)" -ForegroundColor Cyan
        dotnet build $target.BuildPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for $($target.Key) with exit code $LASTEXITCODE."
        }

        $builtDllPath = Join-Path $repoRoot $target.BuiltDllRelPath
        if (-not (Test-Path $builtDllPath)) {
            throw "Built DLL was not found at $builtDllPath."
        }
        $builtDlls += $builtDllPath
        Write-Host "  OK: $builtDllPath" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "All $($selectedTargets.Count) module(s) built successfully." -ForegroundColor Green

    # --- Deployment ---
    $involvedGames = @($selectedTargets | ForEach-Object { $_.Game } | Select-Object -Unique)
    $deployDirs = ($involvedGames | ForEach-Object { $gameConfig[$_].DeployDir }) -join ", "
    $copyConfirmed = Read-YesNo "Deploy $($selectedTargets.Count) DLL(s) to $deployDirs ?"
    if (-not $copyConfirmed) {
        Stop-BuildFlow "Build flow stopped before deployment."
    }

    $processNames = @($involvedGames | ForEach-Object { $gameConfig[$_].StudioProcessNames } | ForEach-Object { $_ })
    $runningProcesses = @(
        $processNames |
            ForEach-Object { Get-Process -Name $_ -ErrorAction SilentlyContinue } |
            Where-Object { $null -ne $_ }
    )

    if ($runningProcesses.Count -gt 0) {
        $processList = ($runningProcesses | Select-Object -ExpandProperty ProcessName -Unique) -join ", "
        $killConfirmed = Read-YesNo "The following process(es) are running: $processList. Kill them and continue?"
        if (-not $killConfirmed) {
            Stop-BuildFlow "Build flow stopped because the game process(es) are still running."
        }

        $runningProcesses | Stop-Process -Force
        Write-Host "Stopped: $processList" -ForegroundColor Yellow
    }

    foreach ($game in $involvedGames) {
        New-Item -ItemType Directory -Path $gameConfig[$game].DeployDir -Force | Out-Null
    }

    $existingAction = $null
    foreach ($target in $selectedTargets) {
        $deployDir = $gameConfig[$target.Game].DeployDir
        $builtDllPath = Join-Path $repoRoot $target.BuiltDllRelPath
        $deployDllPath = Join-Path $deployDir $target.DeployFileName
        $deactivatedDllPath = Join-Path $deployDir $target.DeactivatedFileName

        if (Test-Path $deployDllPath) {
            if ($null -eq $existingAction) {
                $existingAction = Read-OverwriteOrDeactivate "Existing deployed DLL(s) found. What should happen before copying?"
            }
            if ($existingAction -eq "deactivate") {
                if (Test-Path $deactivatedDllPath) {
                    Remove-Item $deactivatedDllPath -Force
                }
                Move-Item $deployDllPath $deactivatedDllPath -Force
                Write-Host "  Deactivated: $($target.DeployFileName) -> $($target.DeactivatedFileName)" -ForegroundColor Yellow
            }
        }

        Copy-Item $builtDllPath $deployDllPath -Force
        Write-Host "  Deployed: $($target.DeployFileName) -> $deployDir" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Deployment completed ($($selectedTargets.Count) module(s))." -ForegroundColor Green

    foreach ($game in $involvedGames) {
        $cfg = $gameConfig[$game]
        $launchConfirmed = Read-YesNo "Open $($cfg.StudioLabel) now?"
        if ($launchConfirmed) {
            if (-not (Test-Path $cfg.StudioExe)) {
                Write-Host "$($cfg.StudioLabel) not found at $($cfg.StudioExe)" -ForegroundColor Red
            } else {
                Start-Process -FilePath $cfg.StudioExe
                Write-Host "Launched: $($cfg.StudioExe)" -ForegroundColor Yellow
            }
        }
    }

}
catch {
    Write-Host ""
    Write-Host "Build flow failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
