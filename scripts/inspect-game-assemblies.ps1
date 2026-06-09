# Inspect Illusion game Assembly-CSharp types from NuGet refs (HS2 / KK / KKS).
# Usage:
#   .\scripts\inspect-game-assemblies.ps1
#   .\scripts\inspect-game-assemblies.ps1 -Game KK
#   .\scripts\inspect-game-assemblies.ps1 -Game All -Keywords "simple,color,visible"

param(
    [ValidateSet('HS2', 'KK', 'KKS', 'All')]
    [string] $Game = 'All',

    [string] $Keywords = 'simple,color,visible,draw,mono,silhouette',

    [switch] $SimpleColor,

    [string[]] $Types = @(),

    [switch] $Verbose
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'scripts\game-assembly-inspector\GameAssemblyInspector.csproj'

if (-not (Test-Path $project)) {
    throw "Inspector project not found: $project"
}

function Invoke-GameInspection {
    param(
        [string] $TargetGame
    )

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " $TargetGame " -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    if ($SimpleColor) {
        $typeList = @(
            'Studio.OCIChar',
            'Studio.OCICharFemale',
            'Studio.OCICharMale',
            'Studio.OICharInfo',
            'ChaControl'
        )
        $keywordList = 'simple,color,visible,draw,mono,silhouette,material,shader'
    }
    elseif ($Types.Count -gt 0) {
        $typeList = $Types
        $keywordList = $Keywords
    }
    else {
        $typeList = @('Studio.OCIChar', 'Studio.OICharInfo', 'Studio.ObjectCtrlInfo')
        $keywordList = $Keywords
    }

    $args = @(
        'run',
        '--project', $project,
        '-c', 'Release',
        '--',
        '--game', $TargetGame,
        '--keywords', $keywordList
    )

    foreach ($typeName in $typeList) {
        $args += @('--type', $typeName)
    }

    if ($Verbose) {
        $args += '--verbose'
    }

    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "Inspection failed for $TargetGame (exit $LASTEXITCODE)"
    }
}

Push-Location $repoRoot
try {
    if ($Game -eq 'All') {
        foreach ($g in @('HS2', 'KK', 'KKS')) {
            Invoke-GameInspection -TargetGame $g
        }
    }
    else {
        Invoke-GameInspection -TargetGame $Game
    }

    Write-Host ""
    Write-Host "Done." -ForegroundColor Green
}
finally {
    Pop-Location
}
