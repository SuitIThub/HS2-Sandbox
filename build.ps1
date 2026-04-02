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

function Read-Choice {
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
        Write-Host "  [C] Cancel"
        $response = Read-Host "Choose"
        if ($null -eq $response) { Stop-BuildFlow "Cancelled." }
        $trimmed = $response.Trim()
        if ($trimmed -eq "" -or $trimmed.ToLowerInvariant() -eq "c" -or $trimmed.ToLowerInvariant() -eq "cancel") {
            Stop-BuildFlow "Cancelled."
        }
        $idx = 0
        if ([int]::TryParse($trimmed, [ref]$idx)) {
            if ($idx -ge 1 -and $idx -le $Options.Count) {
                return ($idx - 1)
            }
        }
        Write-Host "Please enter 1-$($Options.Count) or C." -ForegroundColor Yellow
    }
}

$targets = @(
    @{
        Key = "AllInOne"
        DisplayName = "All-in-one (HS2SandboxPlugin.dll)"
        BuildPath = "HS2-Sandbox.sln"
        BuiltDllRelPath = "bin\Release\HS2SandboxPlugin.dll"
        DeployFileName = "HS2SandboxPlugin.dll"
        DeactivatedFileName = "HS2SandboxPlugin.dl_"
    },
    @{
        Key = "CopyScript"
        DisplayName = "CopyScript module (HS2Sandbox.CopyScript.dll)"
        BuildPath = "Modules\CopyScript\HS2Sandbox.CopyScript.csproj"
        BuiltDllRelPath = "Modules\CopyScript\bin\Release\HS2Sandbox.CopyScript.dll"
        DeployFileName = "HS2Sandbox.CopyScript.dll"
        DeactivatedFileName = "HS2Sandbox.CopyScript.dl_"
    },
    @{
        Key = "Timeline"
        DisplayName = "Timeline module (HS2Sandbox.Timeline.dll)"
        BuildPath = "Modules\Timeline\HS2Sandbox.Timeline.csproj"
        BuiltDllRelPath = "Modules\Timeline\bin\Release\HS2Sandbox.Timeline.dll"
        DeployFileName = "HS2Sandbox.Timeline.dll"
        DeactivatedFileName = "HS2Sandbox.Timeline.dl_"
    },
    @{
        Key = "SearchBarManager"
        DisplayName = "SearchBarManager module (HS2Sandbox.SearchBarManager.dll)"
        BuildPath = "Modules\SearchBarManager\HS2Sandbox.SearchBarManager.csproj"
        BuiltDllRelPath = "Modules\SearchBarManager\bin\Release\HS2Sandbox.SearchBarManager.dll"
        DeployFileName = "HS2Sandbox.SearchBarManager.dll"
        DeactivatedFileName = "HS2Sandbox.SearchBarManager.dl_"
    }
)

$choiceIdx = Read-Choice "What do you want to build/deploy?" ($targets | ForEach-Object { $_.DisplayName })
$target = $targets[$choiceIdx]
$builtDllPath = Join-Path $repoRoot $target.BuiltDllRelPath
$deployDllPath = Join-Path $deployDir $target.DeployFileName
$deactivatedDllPath = Join-Path $deployDir $target.DeactivatedFileName

Write-Host "Building: $($target.DisplayName)" -ForegroundColor Cyan
Push-Location $repoRoot

try {
    dotnet build $target.BuildPath -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path $builtDllPath)) {
        throw "Built DLL was not found at $builtDllPath."
    }

    Write-Host ""
    Write-Host "Build successful." -ForegroundColor Green
    Write-Host "Built DLL: $builtDllPath" -ForegroundColor Yellow

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

    $copyConfirmed = Read-YesNo "Copy the newly built DLL to $deployDir ?"
    if (-not $copyConfirmed) {
        Stop-BuildFlow "Build flow stopped before deployment."
    }

    New-Item -ItemType Directory -Path $deployDir -Force | Out-Null

    if (Test-Path $deployDllPath) {
        $existingAction = Read-OverwriteOrDeactivate "An existing deployed DLL was found. What should happen before copying?"
        if ($existingAction -eq "deactivate") {
            if (Test-Path $deactivatedDllPath) {
                Remove-Item $deactivatedDllPath -Force
            }

            Move-Item $deployDllPath $deactivatedDllPath -Force
            Write-Host "Deactivated existing DLL to $deactivatedDllPath" -ForegroundColor Yellow
        } else {
            Write-Host "Existing DLL will be overwritten." -ForegroundColor Yellow
        }
    }

    Copy-Item $builtDllPath $deployDllPath -Force
    Write-Host ""
    Write-Host "Deployment completed." -ForegroundColor Green
    Write-Host "Copied to: $deployDllPath" -ForegroundColor Yellow

    $launchStudioConfirmed = Read-YesNo "Open StudioNeoV2 now?"
    if ($launchStudioConfirmed) {
        if (-not (Test-Path $studioExePath)) {
            throw "StudioNeoV2 executable was not found at $studioExePath."
        }

        Start-Process -FilePath $studioExePath
        Write-Host "Launched: $studioExePath" -ForegroundColor Yellow
    }

    Read-Host "Press Enter to continue"
}
catch {
    Write-Host ""
    Write-Host "Build flow failed: $($_.Exception.Message)" -ForegroundColor Red
    Read-Host "Press Enter to continue"
    exit 1
}
finally {
    Pop-Location
}

