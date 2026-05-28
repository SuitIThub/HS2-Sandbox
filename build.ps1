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
$deployDir = "D:\Honey Select\BepInEx\plugins\HS2-Sandbox"
$studioExePath = "D:\Honey Select\StudioNEOV2.exe"

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

$targets = @(
    @{
        Key = "CopyScript"
        DisplayName = "CopyScript module (HS2Sandbox.CopyScript.dll)"
        BuildPath = "targets\HS2\CopyScript\HS2Sandbox.CopyScript.csproj"
        BuiltDllRelPath = "targets\HS2\CopyScript\bin\Release\HS2Sandbox.CopyScript.dll"
        DeployFileName = "HS2Sandbox.CopyScript.dll"
        DeactivatedFileName = "HS2Sandbox.CopyScript.dl_"
    },
    @{
        Key = "Timeline"
        DisplayName = "Timeline module (HS2Sandbox.Timeline.dll)"
        BuildPath = "targets\HS2\Timeline\HS2Sandbox.Timeline.csproj"
        BuiltDllRelPath = "targets\HS2\Timeline\bin\Release\HS2Sandbox.Timeline.dll"
        DeployFileName = "HS2Sandbox.Timeline.dll"
        DeactivatedFileName = "HS2Sandbox.Timeline.dl_"
    },
    @{
        Key = "SearchBarManager"
        DisplayName = "SearchBarManager module (HS2Sandbox.SearchBarManager.dll)"
        BuildPath = "targets\HS2\SearchBarManager\HS2Sandbox.SearchBarManager.csproj"
        BuiltDllRelPath = "targets\HS2\SearchBarManager\bin\Release\HS2Sandbox.SearchBarManager.dll"
        DeployFileName = "HS2Sandbox.SearchBarManager.dll"
        DeactivatedFileName = "HS2Sandbox.SearchBarManager.dl_"
    },
    @{
        Key = "SonScale"
        DisplayName = "Son scale module (HS2Sandbox.SonScale.dll)"
        BuildPath = "targets\HS2\SonScale\HS2Sandbox.SonScale.csproj"
        BuiltDllRelPath = "targets\HS2\SonScale\bin\Release\HS2Sandbox.SonScale.dll"
        DeployFileName = "HS2Sandbox.SonScale.dll"
        DeactivatedFileName = "HS2Sandbox.SonScale.dl_"
    },
    @{
        Key = "WorkspaceTreeLock"
        DisplayName = "Workspace tree lock module (HS2Sandbox.WorkspaceTreeLock.dll)"
        BuildPath = "targets\HS2\WorkspaceTreeLock\HS2Sandbox.WorkspaceTreeLock.csproj"
        BuiltDllRelPath = "targets\HS2\WorkspaceTreeLock\bin\Release\HS2Sandbox.WorkspaceTreeLock.dll"
        DeployFileName = "HS2Sandbox.WorkspaceTreeLock.dll"
        DeactivatedFileName = "HS2Sandbox.WorkspaceTreeLock.dl_"
    },
    @{
        Key = "Notebook"
        DisplayName = "Notebook module (HS2Sandbox.Notebook.dll)"
        BuildPath = "targets\HS2\Notebook\HS2Sandbox.Notebook.csproj"
        BuiltDllRelPath = "targets\HS2\Notebook\bin\Release\HS2Sandbox.Notebook.dll"
        DeployFileName = "HS2Sandbox.Notebook.dll"
        DeactivatedFileName = "HS2Sandbox.Notebook.dl_"
    },
    @{
        Key = "PoseBrowser"
        DisplayName = "PoseBrowser module (HS2Sandbox.PoseBrowser.dll)"
        BuildPath = "targets\HS2\PoseBrowser\HS2Sandbox.PoseBrowser.csproj"
        BuiltDllRelPath = "targets\HS2\PoseBrowser\bin\Release\HS2Sandbox.PoseBrowser.dll"
        DeployFileName = "HS2Sandbox.PoseBrowser.dll"
        DeactivatedFileName = "HS2Sandbox.PoseBrowser.dl_"
    }
)

$choiceIndices = Read-MultiChoice "What do you want to build/deploy?" ($targets | ForEach-Object { $_.DisplayName })
$selectedTargets = @($choiceIndices | ForEach-Object { $targets[$_] })

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
    $copyConfirmed = Read-YesNo "Deploy $($selectedTargets.Count) DLL(s) to $deployDir ?"
    if (-not $copyConfirmed) {
        Stop-BuildFlow "Build flow stopped before deployment."
    }

    $runningProcesses = @(
        @("StudioNeoV2", "HoneySelect2") |
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

    New-Item -ItemType Directory -Path $deployDir -Force | Out-Null

    $existingAction = $null
    foreach ($target in $selectedTargets) {
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
        Write-Host "  Deployed: $($target.DeployFileName)" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Deployment completed ($($selectedTargets.Count) module(s))." -ForegroundColor Green

    $launchStudioConfirmed = Read-YesNo "Open StudioNeoV2 now?"
    if ($launchStudioConfirmed) {
        if (-not (Test-Path $studioExePath)) {
            throw "StudioNeoV2 executable was not found at $studioExePath."
        }

        Start-Process -FilePath $studioExePath
        Write-Host "Launched: $studioExePath" -ForegroundColor Yellow
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
